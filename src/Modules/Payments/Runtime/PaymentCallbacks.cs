using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Payments.Domain;
using DynamicCube.Modules.Payments.Persistence;
using DynamicCube.Platform.Integration;
using DynamicCube.Platform.Persistence;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DynamicCube.Modules.Payments.Runtime;

public enum CallbackOutcome
{
    /// <summary>Recorded (paid, already paid, or a status that is not a payment).</summary>
    Accepted,

    /// <summary>The signature is missing or wrong, or the provider has no callback secret.</summary>
    Unauthorized,

    /// <summary>Unknown provider or bill.</summary>
    NotFound,

    /// <summary>Not JSON, or the amount does not match the bill.</summary>
    Refused,
}

/// <summary>
/// Payment notifications from providers. Each must be signed: an HMAC-SHA256 of the raw body with the provider's callback
/// secret, in hex, in the <c>X-Signature</c> header. A payment of the bill's exact total marks it paid; repeats are
/// accepted without effect; a payment for a bill that already expired or was cancelled is recorded for staff to settle.
/// </summary>
internal sealed partial class PaymentCallbacks(PaymentsDbContext context, PaymentService payments, ISecretStore secrets, IUnitOfWork unitOfWork, TimeProvider clock, ILogger<PaymentCallbacks> logger)
{
    public const string SignatureHeader = "X-Signature";

    public async Task<CallbackOutcome> HandleAsync(string providerCode, string body, string? signature, CancellationToken cancellationToken)
    {
        var provider = await context.Providers.AsNoTracking().SingleOrDefaultAsync(p => p.Code == providerCode && p.IsActive, cancellationToken);
        if (provider is null)
        {
            return CallbackOutcome.NotFound;
        }

        var key = provider.CallbackSecret is { } name ? secrets.Find(name, "key") : null;
        if (key is null || !Verify(key, body, signature))
        {
            Refused(providerCode, "a missing or wrong signature");
            return CallbackOutcome.Unauthorized;
        }

        JsonNode? json;
        try
        {
            json = JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return CallbackOutcome.Refused;
        }

        var fields = provider.Callback;
        var number = JsonPaths.ReadText(json, fields.BillNumber);
        await using var work = await unitOfWork.BeginAsync(cancellationToken);
        var bill = number is null ? null : await context.Bills.SingleOrDefaultAsync(b => b.ProviderId == provider.Id && b.ProviderBillNumber == number, cancellationToken);
        if (bill is null)
        {
            return CallbackOutcome.NotFound;
        }

        var actor = $"provider:{provider.Code}";
        var status = JsonPaths.ReadText(json, fields.Status);
        if (!string.Equals(status, fields.PaidValue, StringComparison.OrdinalIgnoreCase) || bill.Status == BillStatus.Paid)
        {
            if (bill.Status != BillStatus.Paid)
            {
                await NoteAsync(bill, BillEventTypes.NotificationIgnored, actor, $"status {status}", cancellationToken);
                await work.CommitAsync(cancellationToken);
            }

            return CallbackOutcome.Accepted;
        }

        if (!decimal.TryParse(JsonPaths.ReadText(json, fields.Amount), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount != bill.Total)
        {
            await NoteAsync(bill, BillEventTypes.NotificationRefused, actor, $"amount {JsonPaths.ReadText(json, fields.Amount)} is not the bill's {bill.Total.ToString("0.00", CultureInfo.InvariantCulture)}", cancellationToken);
            await work.CommitAsync(cancellationToken);
            return CallbackOutcome.Refused;
        }

        var reference = JsonPaths.ReadText(json, fields.Reference);
        if (bill.Status != BillStatus.Pending)
        {
            // Money was received for a bill that no longer counts: staff settle it (refund or a manual record).
            await NoteAsync(bill, BillEventTypes.NotificationRefused, actor, $"paid {reference} after the bill was {bill.Status.ToString().ToLowerInvariant()}", cancellationToken);
            await work.CommitAsync(cancellationToken);
            return CallbackOutcome.Accepted;
        }

        var paidAt = DateTimeOffset.TryParse(JsonPaths.ReadText(json, fields.PaidAt), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) ? at : clock.GetUtcNow();
        await payments.MarkPaidAsync(bill, amount, reference, paidAt, actor, BillEventTypes.Paid, null, cancellationToken);
        await work.CommitAsync(cancellationToken);
        return CallbackOutcome.Accepted;
    }

    /// <summary>Signs a body as providers must (also used by the development mock and tests).</summary>
    public static string Sign(string key, string body) => Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(body)));

    private static bool Verify(string key, string body, string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var given = signature.Trim();
        given = given.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase) ? given[7..] : given;
        var expected = Encoding.ASCII.GetBytes(Sign(key, body));
        return CryptographicOperations.FixedTimeEquals(expected, Encoding.ASCII.GetBytes(given.ToLowerInvariant()));
    }

    private async Task NoteAsync(Bill bill, string type, string actor, string comment, CancellationToken cancellationToken)
    {
        context.Events.Add(new BillEvent { BillId = bill.Id, At = clock.GetUtcNow(), Type = type, Actor = actor, Comment = comment });
        await context.SaveChangesAsync(cancellationToken);
    }

    private void Refused(string provider, string reason)
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            LogRefused(logger, provider, reason);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Payment notification for provider {Provider} refused: {Reason}.")]
    private static partial void LogRefused(ILogger logger, string provider, string reason);
}
