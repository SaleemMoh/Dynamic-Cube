using System.Text.Json.Nodes;
using DynamicCube.Modules.Visits.Domain;
using DynamicCube.Modules.Visits.Persistence;
using DynamicCube.Platform.Files;
using DynamicCube.Platform.Persistence;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Visits.Runtime;

public sealed record VisitSummary(
    Guid Id,
    string Number,
    string TypeCode,
    string TypeNameAr,
    string TypeNameEn,
    string Mode,
    string Status,
    string SubjectType,
    Guid SubjectId,
    string? SubjectNumber,
    string TitleAr,
    string TitleEn,
    string? PartyName,
    string? SiteAddress,
    string? Inspector,
    string? InspectorName,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ScheduledAt,
    DateTimeOffset? DueAt,
    DateTimeOffset? CompletedAt,
    string? Outcome,
    bool Overdue);

public sealed record VisitPage(IReadOnlyList<VisitSummary> Items, int Total);

/// <summary>A visit type as the register and the field app need it to show and record results.</summary>
public sealed record VisitTypeView(
    string Code,
    string NameAr,
    string NameEn,
    string Mode,
    string? DescriptionAr,
    string? DescriptionEn,
    IReadOnlyList<VisitOutcome> Outcomes,
    IReadOnlyList<VisitField> Fields,
    int PhotosRequired,
    bool RequireLocation,
    bool RequireSignature,
    string? InspectorRole,
    bool Polled);

public sealed record VisitPhotoView(Guid Id, string FileName, string ContentType, long Length, string? Caption, DateTimeOffset At);

public sealed record VisitEventView(DateTimeOffset At, string Type, string Actor, string? Comment);

public sealed record VisitDetail(
    VisitSummary Visit,
    VisitTypeView Type,
    double? Latitude,
    double? Longitude,
    string? Instructions,
    JsonObject Data,
    string? Report,
    double? CapturedLatitude,
    double? CapturedLongitude,
    double? CapturedAccuracy,
    bool HasSignature,
    string? SignedBy,
    string? CompletedBy,
    string? ExternalReference,
    string? ExternalStatus,
    DateTimeOffset? NextPollAt,
    string? CancelReason,
    IReadOnlyList<VisitPhotoView> Photos,
    IReadOnlyList<VisitEventView> Events);

public sealed record ScheduleVisitRequest(string? Inspector, DateTimeOffset? ScheduledAt, string? Comment);

public sealed record CancelVisitRequest(string? Reason);

public sealed record RecordOutcomeRequest(string? Outcome, JsonObject? Data, string? Report, string? Comment);

public sealed record InspectorView(string UserName, string NameAr, string NameEn, int OpenVisits);

/// <summary>Staff side of visits: search, details with history, scheduling and assigning, cancelling, recording a result.</summary>
internal sealed class VisitRegistry(VisitsDbContext context, VisitService visits, IStaffDirectory staff, IFileStorage files, IUnitOfWork unitOfWork, TimeProvider clock)
{
    private static readonly VisitStatus[] Open = [VisitStatus.Pending, VisitStatus.Scheduled, VisitStatus.InProgress, VisitStatus.Sent];

    public async Task<VisitPage> SearchAsync(string? search, VisitStatus? status, string? type, string? inspector, bool overdue, int skip, int take, CancellationToken cancellationToken)
    {
        var query = context.Visits.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(v => EF.Functions.ILike(v.Number, term) || EF.Functions.ILike(v.SubjectNumber ?? string.Empty, term)
                || EF.Functions.ILike(v.PartyName ?? string.Empty, term) || EF.Functions.ILike(v.SiteAddress ?? string.Empty, term)
                || EF.Functions.ILike(v.ExternalReference ?? string.Empty, term));
        }

        if (status is not null)
        {
            query = query.Where(v => v.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(v => v.TypeCode == type);
        }

        if (!string.IsNullOrWhiteSpace(inspector))
        {
            query = query.Where(v => v.Inspector == inspector);
        }

        if (overdue)
        {
            var now = clock.GetUtcNow();
            query = query.Where(v => Open.Contains(v.Status) && v.DueAt < now);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(v => v.RequestedAt).Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200)).ToListAsync(cancellationToken);
        var types = await TypesAsync(rows, cancellationToken);
        return new VisitPage([.. rows.Select(v => Summary(v, types[v.TypeId], clock.GetUtcNow()))], total);
    }

    public async Task<VisitDetail> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var visit = await context.Visits.AsNoTracking().SingleOrDefaultAsync(v => v.Id == id, cancellationToken) ?? throw new VisitNotFoundException($"Visit {id} does not exist.");
        return await DetailAsync(context, visit, clock.GetUtcNow(), cancellationToken);
    }

    /// <summary>Assigns the inspector and the date (again, to reschedule); the inspector and the applicant are told.</summary>
    public async Task<VisitDetail> ScheduleAsync(Guid id, ScheduleVisitRequest request, CancellationToken cancellationToken)
    {
        var userName = VisitService.Clip(request.Inspector, 256) ?? throw new VisitValidationException("inspector", "Choose the inspector.");
        var at = request.ScheduledAt ?? throw new VisitValidationException("scheduledAt", "Give the date and time of the visit.");
        if (at < clock.GetUtcNow().AddDays(-1))
        {
            throw new VisitValidationException("scheduledAt", "The date has passed.");
        }

        await using (var work = await unitOfWork.BeginAsync(cancellationToken))
        {
            var visit = await OpenAsync(id, cancellationToken);
            var type = await context.Types.AsNoTracking().SingleAsync(t => t.Id == visit.TypeId, cancellationToken);
            if (visit.Mode == VisitMode.External)
            {
                throw new VisitConflictException("External visits are scheduled by the external system.");
            }

            if (visit.Status == VisitStatus.InProgress)
            {
                throw new VisitConflictException($"Visit {visit.Number} is under way.");
            }

            var member = await staff.FindAsync(userName, cancellationToken) ?? throw new VisitValidationException("inspector", $"'{userName}' is not an active employee.");
            if (type.InspectorRole is { } role && !member.Roles.Contains(role))
            {
                throw new VisitValidationException("inspector", $"'{userName}' does not hold the role {role}.");
            }

            var reassigned = visit.Inspector != member.UserName;
            var rescheduled = visit.Status == VisitStatus.Scheduled;
            visit.Inspector = member.UserName;
            visit.InspectorName = member.NameAr;
            visit.ScheduledAt = at;
            visit.Status = VisitStatus.Scheduled;
            visits.Note(visit, rescheduled ? VisitEventTypes.Rescheduled : VisitEventTypes.Scheduled, visits.Actor, VisitService.Clip(request.Comment, 2000) ?? $"{member.UserName} · {at:yyyy-MM-dd HH:mm}");
            VisitNotifications.Raise(context, clock, VisitNotifications.Scheduled, visit, type);
            if (reassigned)
            {
                VisitNotifications.Raise(context, clock, VisitNotifications.Assigned, visit, type);
            }

            await context.SaveChangesAsync(cancellationToken);
            await work.CommitAsync(cancellationToken);
        }

        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Cancels a visit that will not take place; a request waiting for it takes its workflow's cancelled route.</summary>
    public async Task<VisitDetail> CancelAsync(Guid id, CancelVisitRequest request, CancellationToken cancellationToken)
    {
        var reason = VisitService.Clip(request.Reason, 2000) ?? throw new VisitValidationException("reason", "Give the reason.");
        await using (var work = await unitOfWork.BeginAsync(cancellationToken))
        {
            var visit = await OpenAsync(id, cancellationToken);
            var type = await context.Types.AsNoTracking().SingleAsync(t => t.Id == visit.TypeId, cancellationToken);
            await visits.CloseCancelledAsync(visit, type, reason, visits.Actor, tell: true, cancellationToken);
            await work.CommitAsync(cancellationToken);
        }

        return await GetAsync(id, cancellationToken);
    }

    /// <summary>A result recorded by staff: a manual visit, or a field or external visit reported another way.</summary>
    public async Task<VisitDetail> RecordAsync(Guid id, RecordOutcomeRequest request, CancellationToken cancellationToken)
    {
        await using (var work = await unitOfWork.BeginAsync(cancellationToken))
        {
            var visit = await OpenAsync(id, cancellationToken);
            var type = await context.Types.AsNoTracking().SingleAsync(t => t.Id == visit.TypeId, cancellationToken);
            var data = VisitForms.Check(type, request.Outcome, request.Data);
            await visits.CompleteAsync(visit, type, request.Outcome!, data, request.Report, visits.Actor, VisitEventTypes.Recorded, VisitService.Clip(request.Comment, 2000), cancellationToken);
            await work.CommitAsync(cancellationToken);
        }

        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Employees who can be assigned visits of the type, with how many open visits each has.</summary>
    public async Task<IReadOnlyList<InspectorView>> InspectorsAsync(string? typeCode, CancellationToken cancellationToken)
    {
        var role = typeCode is null ? null : await context.Types.AsNoTracking().Where(t => t.Code == typeCode).Select(t => t.InspectorRole).SingleOrDefaultAsync(cancellationToken);
        var members = await staff.SearchAsync(new StaffQuery(Role: role), cancellationToken);
        var names = members.Select(m => m.UserName).ToList();
        var load = await context.Visits.AsNoTracking().Where(v => v.Inspector != null && names.Contains(v.Inspector) && (v.Status == VisitStatus.Scheduled || v.Status == VisitStatus.InProgress))
            .GroupBy(v => v.Inspector!).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
        return [.. members.OrderBy(m => load.GetValueOrDefault(m.UserName)).ThenBy(m => m.NameAr, StringComparer.Ordinal)
            .Select(m => new InspectorView(m.UserName, m.NameAr, m.NameEn, load.GetValueOrDefault(m.UserName)))];
    }

    public async Task<(Stream Content, string ContentType, string FileName)> PhotoAsync(Guid id, Guid photoId, CancellationToken cancellationToken)
    {
        var photo = await context.Photos.AsNoTracking().SingleOrDefaultAsync(p => p.Id == photoId && p.VisitId == id, cancellationToken) ?? throw new VisitNotFoundException("The photo does not exist.");
        return (await files.OpenReadAsync(photo.FileKey, cancellationToken), photo.ContentType, photo.FileName);
    }

    public async Task<Stream> SignatureAsync(Guid id, CancellationToken cancellationToken)
    {
        var key = await context.Visits.AsNoTracking().Where(v => v.Id == id).Select(v => v.SignatureKey).SingleOrDefaultAsync(cancellationToken)
            ?? throw new VisitNotFoundException("The visit has no signature.");
        return await files.OpenReadAsync(key, cancellationToken);
    }

    internal static async Task<VisitDetail> DetailAsync(VisitsDbContext context, Visit visit, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var type = await context.Types.AsNoTracking().SingleAsync(t => t.Id == visit.TypeId, cancellationToken);
        var photos = await context.Photos.AsNoTracking().Where(p => p.VisitId == visit.Id).OrderBy(p => p.At)
            .Select(p => new VisitPhotoView(p.Id, p.FileName, p.ContentType, p.Length, p.Caption, p.At)).ToListAsync(cancellationToken);
        var events = await context.Events.AsNoTracking().Where(e => e.VisitId == visit.Id).OrderBy(e => e.At)
            .Select(e => new VisitEventView(e.At, e.Type, e.Actor, e.Comment)).ToListAsync(cancellationToken);
        return new VisitDetail(
            Summary(visit, type, now),
            TypeView(type),
            visit.Latitude,
            visit.Longitude,
            visit.Instructions,
            JsonNode.Parse(visit.Data) as JsonObject ?? [],
            visit.Report,
            visit.CapturedLatitude,
            visit.CapturedLongitude,
            visit.CapturedAccuracy,
            visit.SignatureKey is not null,
            visit.SignedBy,
            visit.CompletedBy,
            visit.ExternalReference,
            visit.ExternalStatus,
            visit.NextPollAt,
            visit.CancelReason,
            photos,
            events);
    }

    internal static VisitTypeView TypeView(VisitType type) => new(
        type.Code,
        type.NameAr,
        type.NameEn,
        VisitService.Upper(type.Mode),
        type.DescriptionAr,
        type.DescriptionEn,
        type.Outcomes,
        type.Fields,
        type.PhotosRequired,
        type.RequireLocation,
        type.RequireSignature,
        type.InspectorRole,
        type.External?.ResultOperation is { Length: > 0 });

    internal static VisitSummary Summary(Visit v, VisitType type, DateTimeOffset now) => new(
        v.Id,
        v.Number,
        v.TypeCode,
        type.NameAr,
        type.NameEn,
        VisitService.Upper(v.Mode),
        VisitService.Upper(v.Status),
        v.SubjectType,
        v.SubjectId,
        v.SubjectNumber,
        v.TitleAr,
        v.TitleEn,
        v.PartyName,
        v.SiteAddress,
        v.Inspector,
        v.InspectorName,
        v.RequestedAt,
        v.ScheduledAt,
        v.DueAt,
        v.CompletedAt,
        v.Outcome,
        Open.Contains(v.Status) && v.DueAt < now);

    private async Task<Dictionary<Guid, VisitType>> TypesAsync(List<Visit> rows, CancellationToken cancellationToken)
    {
        var ids = rows.Select(r => r.TypeId).Distinct().ToList();
        return await context.Types.AsNoTracking().Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, cancellationToken);
    }

    private async Task<Visit> OpenAsync(Guid id, CancellationToken cancellationToken)
    {
        var visit = await context.Visits.SingleOrDefaultAsync(v => v.Id == id, cancellationToken) ?? throw new VisitNotFoundException($"Visit {id} does not exist.");
        return Open.Contains(visit.Status) ? visit : throw new VisitConflictException($"Visit {visit.Number} is {visit.Status.ToString().ToLowerInvariant()}.");
    }
}
