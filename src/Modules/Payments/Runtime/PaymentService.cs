using System.Globalization;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Payments.Domain;
using DynamicCube.Modules.Payments.Persistence;
using DynamicCube.Platform.Integration;
using DynamicCube.Platform.Payments;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Payments.Runtime;

/// <summary>
/// Issues bills: a number (<c>BILL-YYYY-000001</c>), registration with the provider through its Integration Manager
/// operation (which answers with the number the payer pays with), the due date, and the notification with how to pay.
/// Closing a bill (paid, expired, cancelled) tells the <see cref="IPaymentListener"/>s in the same unit of work.
/// </summary>
internal sealed class PaymentService(PaymentsDbContext context, IServiceProvider services, ICurrentUser user, TimeProvider clock) : IPayments
{
    public async Task<BillInfo> CreateBillAsync(BillRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Lines.Count == 0 || request.Lines.Any(l => l.Total < 0) || request.Lines.Sum(l => l.Total) <= 0)
        {
            throw new PaymentException("A bill needs lines with a positive total.");
        }

        var provider = request.Provider is { } code
            ? await context.Providers.AsNoTracking().SingleOrDefaultAsync(p => p.Code == code && p.IsActive, cancellationToken)
                ?? throw new PaymentException($"Payment provider '{code}' does not exist or is inactive.")
            : await context.Providers.AsNoTracking().SingleOrDefaultAsync(p => p.IsDefault && p.IsActive, cancellationToken)
                ?? throw new PaymentException("No default payment provider is configured.");
        var now = clock.GetUtcNow();
        var bill = new Bill
        {
            Number = await NextNumberAsync(now, cancellationToken),
            ProviderId = provider.Id,
            ProviderCode = provider.Code,
            SubjectType = request.SubjectType,
            SubjectId = request.SubjectId,
            SubjectNumber = request.SubjectNumber,
            PayerPartyId = request.PayerPartyId,
            PayerName = request.PayerName,
            DescriptionAr = request.DescriptionAr,
            DescriptionEn = request.DescriptionEn,
            Lines = [.. request.Lines],
            Net = request.Lines.Sum(l => l.Net),
            Vat = request.Lines.Sum(l => l.Vat),
            Total = request.Lines.Sum(l => l.Total),
            Status = BillStatus.Pending,
            IssuedAt = now,
            DueAt = now.AddDays(request.DueDays ?? provider.DueDays),
            CreatedBy = user.UserName ?? user.UserId,
        };
        bill.ProviderBillNumber = provider.CreateOperation is null ? bill.Number : await RegisterAsync(provider, bill, cancellationToken);
        context.Bills.Add(bill);
        context.Events.Add(new BillEvent { BillId = bill.Id, At = now, Type = BillEventTypes.Issued, Actor = bill.CreatedBy, Comment = $"{bill.ProviderBillNumber} · {bill.Total.ToString("0.00", CultureInfo.InvariantCulture)}" });
        PaymentNotifications.Raise(context, clock, PaymentNotifications.Issued, bill, provider, PayUrl(bill, provider));
        await context.SaveChangesAsync(cancellationToken);
        return Info(bill, provider);
    }

    public async Task<IReadOnlyList<BillInfo>> OfSubjectAsync(string subjectType, Guid subjectId, CancellationToken cancellationToken)
    {
        var bills = await context.Bills.AsNoTracking().Where(b => b.SubjectType == subjectType && b.SubjectId == subjectId).OrderBy(b => b.IssuedAt).ToListAsync(cancellationToken);
        var ids = bills.Select(b => b.ProviderId).Distinct().ToList();
        var providers = await context.Providers.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);
        return [.. bills.Select(b => Info(b, providers[b.ProviderId]))];
    }

    /// <summary>Records a payment (from the provider's notification or by staff) and tells the listeners.</summary>
    internal async Task MarkPaidAsync(Bill bill, decimal amount, string? reference, DateTimeOffset paidAt, string actor, string eventType, string? comment, CancellationToken cancellationToken)
    {
        var provider = await context.Providers.AsNoTracking().SingleAsync(p => p.Id == bill.ProviderId, cancellationToken);
        var now = clock.GetUtcNow();
        bill.Status = BillStatus.Paid;
        bill.PaidAt = paidAt;
        bill.PaidAmount = amount;
        bill.PaymentReference = reference;
        bill.ClosedAt = now;
        context.Events.Add(new BillEvent { BillId = bill.Id, At = now, Type = eventType, Actor = actor, Comment = comment ?? reference });
        PaymentNotifications.Raise(context, clock, PaymentNotifications.Paid, bill, provider, null);
        await context.SaveChangesAsync(cancellationToken);
        await TellAsync(bill, cancellationToken);
    }

    /// <summary>Closes an unpaid bill (expired or cancelled) and tells the listeners.</summary>
    internal async Task CloseUnpaidAsync(Bill bill, BillStatus status, string actor, string? comment, CancellationToken cancellationToken)
    {
        var provider = await context.Providers.AsNoTracking().SingleAsync(p => p.Id == bill.ProviderId, cancellationToken);
        var now = clock.GetUtcNow();
        bill.Status = status;
        bill.ClosedAt = now;
        context.Events.Add(new BillEvent { BillId = bill.Id, At = now, Type = status == BillStatus.Expired ? BillEventTypes.Expired : BillEventTypes.Cancelled, Actor = actor, Comment = comment });
        PaymentNotifications.Raise(context, clock, status == BillStatus.Expired ? PaymentNotifications.Expired : PaymentNotifications.Cancelled, bill, provider, null);
        await context.SaveChangesAsync(cancellationToken);
        await TellAsync(bill, cancellationToken);
    }

    internal static BillInfo Info(Bill bill, PaymentProvider provider) => new(
        bill.Id,
        bill.Number,
        bill.ProviderCode,
        bill.ProviderBillNumber,
        provider.BillerCode,
        bill.Status.ToString().ToUpperInvariant(),
        bill.Lines,
        bill.Total,
        bill.IssuedAt,
        bill.DueAt,
        bill.PaidAt,
        bill.PaymentReference,
        bill.Status == BillStatus.Pending ? PayUrl(bill, provider) : null,
        provider.InstructionsAr,
        provider.InstructionsEn);

    private static string? PayUrl(Bill bill, PaymentProvider provider) =>
        provider.PayUrlTemplate?
            .Replace("{billNumber}", Uri.EscapeDataString(bill.ProviderBillNumber ?? bill.Number), StringComparison.Ordinal)
            .Replace("{amount}", bill.Total.ToString("0.00", CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private async Task TellAsync(Bill bill, CancellationToken cancellationToken)
    {
        var closed = new BillClosed(bill.Id, bill.Number, bill.SubjectType, bill.SubjectId, bill.Status == BillStatus.Paid, bill.Status.ToString().ToUpperInvariant(), bill.ClosedAt ?? clock.GetUtcNow(), bill.PaymentReference);
        foreach (var listener in services.GetServices<IPaymentListener>())
        {
            await listener.OnBillClosedAsync(closed, cancellationToken);
        }
    }

    /// <summary>Registers the bill with the provider; the operation answers with the number the payer pays with.</summary>
    private async Task<string> RegisterAsync(PaymentProvider provider, Bill bill, CancellationToken cancellationToken)
    {
        var gateway = services.GetService<IIntegrationGateway>() ?? throw new PaymentException("The Integration Manager is not installed.");
        IntegrationResult result;
        try
        {
            result = await gateway.InvokeAsync(provider.CreateOperation!, new Dictionary<string, string?>
            {
                ["billNumber"] = bill.Number,
                ["amount"] = bill.Total.ToString("0.00", CultureInfo.InvariantCulture),
                ["dueDate"] = bill.DueAt.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["payerId"] = bill.PayerPartyId.ToString(),
                ["payerName"] = bill.PayerName,
                ["description"] = bill.DescriptionAr,
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is IntegrationOperationNotFoundException or IntegrationInputException)
        {
            throw new PaymentException($"Payment provider '{provider.Code}' cannot register bills: {ex.Message}");
        }

        if (!result.IsSuccess)
        {
            throw new PaymentException($"Payment provider '{provider.Code}' did not register the bill: {result.MessageEn ?? result.Code}");
        }

        return result.Outputs.GetValueOrDefault(provider.BillNumberOutput) is JsonValue number && number.ToString() is { Length: > 0 } text
            ? text
            : throw new PaymentException($"Payment provider '{provider.Code}' answered without '{provider.BillNumberOutput}'.");
    }

    private async Task<string> NextNumberAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var key = $"BILL-{now.UtcDateTime.Year.ToString(CultureInfo.InvariantCulture)}";
        var value = await context.Database
            .SqlQuery<long>($"INSERT INTO pay.number_sequence (key, value) VALUES ({key}, 1) ON CONFLICT (key) DO UPDATE SET value = pay.number_sequence.value + 1 RETURNING value AS \"Value\"")
            .ToListAsync(cancellationToken);
        return $"{key}-{value[0].ToString("D6", CultureInfo.InvariantCulture)}";
    }
}
