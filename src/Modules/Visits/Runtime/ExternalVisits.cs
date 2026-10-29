using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Visits.Domain;
using DynamicCube.Modules.Visits.Persistence;
using DynamicCube.Platform.Integration;
using DynamicCube.Platform.Persistence;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DynamicCube.Modules.Visits.Runtime;

public enum CallbackOutcome
{
    /// <summary>Recorded (completed, progress, or a repeat).</summary>
    Accepted,

    /// <summary>The signature is missing or wrong, or the visit type has no callback secret.</summary>
    Unauthorized,

    /// <summary>Unknown visit type or reference.</summary>
    NotFound,

    /// <summary>Not JSON, or an outcome the visit type does not know.</summary>
    Refused,
}

/// <summary>
/// Results of external visits, notified or polled. A notification must be signed: an HMAC-SHA256 of the raw body with the
/// visit type's callback secret, in hex, in the <c>X-Signature</c> header. A finished status completes the visit with the
/// mapped outcome and the reported findings; other statuses are kept as progress.
/// </summary>
internal sealed partial class ExternalVisits(VisitsDbContext context, VisitService visits, IServiceProvider services, ISecretStore secrets, IUnitOfWork unitOfWork, TimeProvider clock, ILogger<ExternalVisits> logger)
{
    public const string SignatureHeader = "X-Signature";

    public async Task<CallbackOutcome> HandleAsync(string typeCode, string body, string? signature, CancellationToken cancellationToken)
    {
        var type = await context.Types.AsNoTracking().SingleOrDefaultAsync(t => t.Code == typeCode && t.IsActive && t.Mode == VisitMode.External, cancellationToken);
        if (type?.External is not { } external)
        {
            return CallbackOutcome.NotFound;
        }

        var key = external.CallbackSecret is { Length: > 0 } name ? secrets.Find(name, "key") : null;
        if (key is null || !Verify(key, body, signature))
        {
            LogRefused(logger, typeCode, "a missing or wrong signature");
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

        var reference = JsonPaths.ReadText(json, external.Result.Reference);
        await using var work = await unitOfWork.BeginAsync(cancellationToken);
        var visit = reference is null ? null : await context.Visits.SingleOrDefaultAsync(v => v.TypeId == type.Id && v.ExternalReference == reference, cancellationToken);
        if (visit is null)
        {
            return CallbackOutcome.NotFound;
        }

        var accepted = await ApplyAsync(visit, type, json, $"external:{type.Code}", cancellationToken);
        await work.CommitAsync(cancellationToken);
        return accepted ? CallbackOutcome.Accepted : CallbackOutcome.Refused;
    }

    /// <summary>Asks the external system for the results of visits due a check (run periodically by the worker); returns how many were checked.</summary>
    public async Task<int> PollAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var due = await context.Visits.Where(v => v.Status == VisitStatus.Sent && v.NextPollAt != null && v.NextPollAt <= now).OrderBy(v => v.NextPollAt).Select(v => v.Id).Take(50).ToListAsync(cancellationToken);
        foreach (var id in due)
        {
            try
            {
                await PollOneAsync(id, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One visit's failure leaves the others to be checked; it is tried again at its next poll.
                LogPollFailed(logger, id, ex);
            }
        }

        return due.Count;
    }

    /// <summary>Checks one sent visit now (also from the register); true when it completed.</summary>
    public async Task<bool> PollOneAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var work = await unitOfWork.BeginAsync(cancellationToken);
        var visit = await context.Visits.SingleOrDefaultAsync(v => v.Id == id, cancellationToken) ?? throw new VisitNotFoundException($"Visit {id} does not exist.");
        if (visit.Status != VisitStatus.Sent)
        {
            throw new VisitConflictException($"Visit {visit.Number} is not waiting for an external result.");
        }

        var type = await context.Types.AsNoTracking().SingleAsync(t => t.Id == visit.TypeId, cancellationToken);
        var external = type.External;
        if (external?.ResultOperation is not { Length: > 0 } operation)
        {
            throw new VisitConflictException($"Visit type '{type.Code}' has no result operation; results come by notification.");
        }

        // Pushed back first, so a failed call is not retried at once.
        visit.NextPollAt = clock.GetUtcNow().AddMinutes(external.PollMinutes);
        await context.SaveChangesAsync(cancellationToken);
        var gateway = services.GetService<IIntegrationGateway>() ?? throw new VisitConflictException("The Integration Manager is not installed.");
        var result = await gateway.InvokeAsync(operation, new Dictionary<string, string?> { ["reference"] = visit.ExternalReference }, cancellationToken);
        if (!result.IsSuccess)
        {
            await work.CommitAsync(cancellationToken);
            throw new VisitConflictException($"The external system did not answer: {result.MessageEn ?? result.Code}");
        }

        var outputs = new JsonObject();
        foreach (var (name, value) in result.Outputs)
        {
            outputs[name] = value?.DeepClone();
        }

        await ApplyAsync(visit, type, outputs, $"external:{type.Code}", cancellationToken);
        await work.CommitAsync(cancellationToken);
        return visit.Status == VisitStatus.Completed;
    }

    /// <summary>Signs a body as external systems must (also used by the development mock and tests).</summary>
    public static string Sign(string key, string body) => Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(body)));

    private async Task<bool> ApplyAsync(Visit visit, VisitType type, JsonNode? result, string actor, CancellationToken cancellationToken)
    {
        var fields = type.External!.Result;
        var status = JsonPaths.ReadText(result, fields.Status);
        if (visit.Status != VisitStatus.Sent)
        {
            if (visit.Status == VisitStatus.Cancelled)
            {
                // The external system finished a visit no longer needed: kept for staff.
                visits.Note(visit, VisitEventTypes.ExternalRefused, actor, $"status {status} after the visit was cancelled");
                await context.SaveChangesAsync(cancellationToken);
            }

            return true;
        }

        if (!string.Equals(status, fields.CompletedValue, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(status, visit.ExternalStatus, StringComparison.Ordinal))
            {
                visit.ExternalStatus = VisitService.Clip(status, 100);
                visits.Note(visit, VisitEventTypes.ExternalProgress, actor, status);
                await context.SaveChangesAsync(cancellationToken);
            }

            return true;
        }

        var raw = JsonPaths.ReadText(result, fields.Outcome);
        var outcome = raw is null ? null : type.External.OutcomeMap.GetValueOrDefault(raw, raw);
        if (outcome is null || type.Outcomes.All(o => o.Code != outcome))
        {
            visits.Note(visit, VisitEventTypes.ExternalRefused, actor, $"outcome {raw} is not an outcome of {type.Code}");
            await context.SaveChangesAsync(cancellationToken);
            return false;
        }

        var data = JsonPaths.Read(result, fields.Data) as JsonObject ?? [];
        visit.ExternalStatus = VisitService.Clip(status, 100);
        await visits.CompleteAsync(visit, type, outcome, data, JsonPaths.ReadText(result, fields.Report), actor, VisitEventTypes.Completed, raw, cancellationToken);
        return true;
    }

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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Visit result notification for type {VisitType} refused: {Reason}.")]
    private static partial void LogRefused(ILogger logger, string visitType, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Checking the external result of visit {VisitId} failed.")]
    private static partial void LogPollFailed(ILogger logger, Guid visitId, Exception exception);
}
