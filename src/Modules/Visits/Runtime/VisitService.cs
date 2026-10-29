using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Visits.Domain;
using DynamicCube.Modules.Visits.Persistence;
using DynamicCube.Platform.Expressions;
using DynamicCube.Platform.Integration;
using DynamicCube.Platform.Security;
using DynamicCube.Platform.Visits;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Visits.Runtime;

/// <summary>
/// Requests visits: a number (<c>VIS-YYYY-000001</c>), the due date, and for external visit types the booking with the
/// external system (which answers with its reference). Closing a visit (completed or cancelled by staff) tells the
/// <see cref="IVisitListener"/>s in the same unit of work.
/// </summary>
internal sealed class VisitService(VisitsDbContext context, IServiceProvider services, JsonLogicEvaluator evaluator, ICurrentUser user, TimeProvider clock) : IVisits
{
    public async Task<VisitInfo> CreateAsync(VisitRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var type = await context.Types.AsNoTracking().SingleOrDefaultAsync(t => t.Code == request.VisitType && t.IsActive, cancellationToken)
            ?? throw new VisitException($"Visit type '{request.VisitType}' does not exist or is inactive.");
        var now = clock.GetUtcNow();
        var visit = new Visit
        {
            Number = await NextNumberAsync(now, cancellationToken),
            TypeId = type.Id,
            TypeCode = type.Code,
            Mode = type.Mode,
            Status = VisitStatus.Pending,
            SubjectType = request.SubjectType,
            SubjectId = request.SubjectId,
            SubjectNumber = request.SubjectNumber,
            PartyId = request.PartyId,
            PartyName = Clip(request.PartyName, 500),
            TitleAr = Clip(request.TitleAr, 500) ?? string.Empty,
            TitleEn = Clip(request.TitleEn, 500) ?? string.Empty,
            SiteAddress = Clip(request.SiteAddress, 1000),
            Latitude = request.Latitude is >= -90 and <= 90 ? request.Latitude : null,
            Longitude = request.Longitude is >= -180 and <= 180 ? request.Longitude : null,
            Instructions = Clip(request.Instructions, 4000),
            RequestedAt = now,
            DueAt = now.AddDays(request.DueDays ?? type.DueDays),
            CreatedBy = Actor,
        };

        context.Visits.Add(visit);
        Note(visit, VisitEventTypes.Requested, Actor, type.Code);
        if (type.Mode == VisitMode.External)
        {
            await BookAsync(type, visit, request.Facts ?? [], cancellationToken);
        }
        else
        {
            VisitNotifications.Raise(context, clock, VisitNotifications.Requested, visit, type);
        }

        await context.SaveChangesAsync(cancellationToken);
        return Info(visit, type);
    }

    public async Task CancelAsync(Guid visitId, string reason, CancellationToken cancellationToken)
    {
        var visit = await context.Visits.SingleOrDefaultAsync(v => v.Id == visitId, cancellationToken);
        if (visit is null || visit.Status is VisitStatus.Completed or VisitStatus.Cancelled)
        {
            return;
        }

        var type = await context.Types.AsNoTracking().SingleAsync(t => t.Id == visit.TypeId, cancellationToken);
        await CloseCancelledAsync(visit, type, reason, Actor, tell: false, cancellationToken);
    }

    public async Task<IReadOnlyList<VisitInfo>> OfSubjectAsync(string subjectType, Guid subjectId, CancellationToken cancellationToken)
    {
        var visits = await context.Visits.AsNoTracking().Where(v => v.SubjectType == subjectType && v.SubjectId == subjectId).OrderBy(v => v.RequestedAt).ToListAsync(cancellationToken);
        var ids = visits.Select(v => v.TypeId).Distinct().ToList();
        var types = await context.Types.AsNoTracking().Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, cancellationToken);
        return [.. visits.Select(v => Info(v, types[v.TypeId]))];
    }

    /// <summary>Records the visit's result (from the field app, the external system or staff) and tells the listeners.</summary>
    internal async Task CompleteAsync(Visit visit, VisitType type, string outcome, JsonObject data, string? report, string actor, string eventType, string? comment, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        visit.Status = VisitStatus.Completed;
        visit.Outcome = outcome;
        visit.Data = data.ToJsonString();
        visit.Report = Clip(report, 8000);
        visit.CompletedAt ??= now;
        visit.CompletedBy = actor;
        visit.ClosedAt = now;
        visit.NextPollAt = null;
        Note(visit, eventType, actor, comment ?? outcome);
        VisitNotifications.Raise(context, clock, VisitNotifications.Completed, visit, type);
        await context.SaveChangesAsync(cancellationToken);
        await TellAsync(visit, type, cancellationToken);
    }

    /// <summary>Cancels an open visit; staff cancellations tell the listeners, the workflow's own do not.</summary>
    internal async Task CloseCancelledAsync(Visit visit, VisitType type, string reason, string actor, bool tell, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        visit.Status = VisitStatus.Cancelled;
        visit.CancelReason = Clip(reason, 2000);
        visit.ClosedAt = now;
        visit.NextPollAt = null;
        Note(visit, VisitEventTypes.Cancelled, actor, reason);
        VisitNotifications.Raise(context, clock, VisitNotifications.Cancelled, visit, type);
        await context.SaveChangesAsync(cancellationToken);
        if (tell)
        {
            await TellAsync(visit, type, cancellationToken);
        }
    }

    internal void Note(Visit visit, string type, string actor, string? comment) =>
        context.Events.Add(new VisitEvent { VisitId = visit.Id, At = clock.GetUtcNow(), Type = type, Actor = actor, Comment = Clip(comment, 4000) });

    internal string Actor => user.UserName ?? user.UserId;

    internal static VisitInfo Info(Visit visit, VisitType type)
    {
        var outcome = type.Outcomes.FirstOrDefault(o => o.Code == visit.Outcome);
        return new VisitInfo(
            visit.Id,
            visit.Number,
            type.Code,
            type.NameAr,
            type.NameEn,
            VisitService.Upper(visit.Mode),
            VisitService.Upper(visit.Status),
            visit.RequestedAt,
            visit.ScheduledAt,
            visit.DueAt,
            visit.CompletedAt,
            visit.Outcome,
            outcome?.NameAr,
            outcome?.NameEn,
            VisitService.Upper(outcome?.Kind),
            visit.SiteAddress);
    }

    /// <summary>An enum value as the API and notifications name it: <c>IN_PROGRESS</c>.</summary>
    internal static string Upper<T>(T value)
        where T : struct, Enum => JsonNamingPolicy.SnakeCaseUpper.ConvertName(value.ToString());

    internal static string? Upper<T>(T? value)
        where T : struct, Enum => value is { } v ? Upper(v) : null;

    internal static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        return text.Length <= max ? text : text[..max];
    }

    private async Task TellAsync(Visit visit, VisitType type, CancellationToken cancellationToken)
    {
        var outcome = type.Outcomes.FirstOrDefault(o => o.Code == visit.Outcome);
        var closed = new VisitClosed(
            visit.Id,
            visit.Number,
            visit.SubjectType,
            visit.SubjectId,
            visit.Status == VisitStatus.Completed,
            VisitService.Upper(visit.Status),
            visit.Outcome,
            VisitService.Upper(outcome?.Kind),
            JsonNode.Parse(visit.Data) as JsonObject ?? [],
            visit.ClosedAt ?? clock.GetUtcNow());
        foreach (var listener in services.GetServices<IVisitListener>())
        {
            await listener.OnVisitClosedAsync(closed, cancellationToken);
        }
    }

    /// <summary>Books the visit with the external system; its operation answers with the reference results will carry.</summary>
    private async Task BookAsync(VisitType type, Visit visit, JsonObject facts, CancellationToken cancellationToken)
    {
        var external = type.External ?? throw new VisitException($"Visit type '{type.Code}' has no external settings.");
        var gateway = services.GetService<IIntegrationGateway>() ?? throw new VisitException("The Integration Manager is not installed.");
        var scope = (JsonObject)facts.DeepClone();
        scope["visit"] = new JsonObject
        {
            ["number"] = visit.Number,
            ["type"] = type.Code,
            ["subjectNumber"] = visit.SubjectNumber,
            ["partyName"] = visit.PartyName,
            ["siteAddress"] = visit.SiteAddress,
            ["latitude"] = visit.Latitude,
            ["longitude"] = visit.Longitude,
            ["instructions"] = visit.Instructions,
            ["dueDate"] = visit.DueAt?.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };
        // The operation receives exactly the configured inputs (it refuses others); by default the visit number.
        var inputs = external.Inputs.Count == 0
            ? new Dictionary<string, string?>(StringComparer.Ordinal) { ["visitNumber"] = visit.Number }
            : external.Inputs.ToDictionary(i => i.Key, i => JsonPaths.Text(evaluator.Evaluate(i.Value, scope)), StringComparer.Ordinal);

        IntegrationResult result;
        try
        {
            result = await gateway.InvokeAsync(external.CreateOperation, inputs, cancellationToken);
        }
        catch (Exception ex) when (ex is IntegrationOperationNotFoundException or IntegrationInputException)
        {
            throw new VisitException($"Visit type '{type.Code}' cannot book visits: {ex.Message}");
        }

        if (!result.IsSuccess)
        {
            throw new VisitException($"The external system did not book the visit: {result.MessageEn ?? result.Code}");
        }

        visit.ExternalReference = result.Outputs.GetValueOrDefault(external.ReferenceOutput) is { } reference && JsonPaths.Text(reference) is { Length: > 0 } text
            ? Clip(text, 100)
            : throw new VisitException($"The external system answered without '{external.ReferenceOutput}'.");
        visit.Status = VisitStatus.Sent;
        visit.NextPollAt = external.ResultOperation is { Length: > 0 } ? clock.GetUtcNow().AddMinutes(external.PollMinutes) : null;
        Note(visit, VisitEventTypes.Sent, Actor, visit.ExternalReference);
    }

    private async Task<string> NextNumberAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var key = $"VIS-{now.UtcDateTime.Year.ToString(CultureInfo.InvariantCulture)}";
        var value = await context.Database
            .SqlQuery<long>($"INSERT INTO vis.number_sequence (key, value) VALUES ({key}, 1) ON CONFLICT (key) DO UPDATE SET value = vis.number_sequence.value + 1 RETURNING value AS \"Value\"")
            .ToListAsync(cancellationToken);
        return $"{key}-{value[0].ToString("D6", CultureInfo.InvariantCulture)}";
    }
}
