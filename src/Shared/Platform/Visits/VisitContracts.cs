using System.Text.Json.Nodes;

namespace DynamicCube.Platform.Visits;

/// <summary>
/// A visit to request for something that needs one (a request's site inspection): of which type, where, and what the
/// inspector should know. The visit type says who carries it out: the entity's inspectors in the field app, an external
/// inspection system, or staff recording the result by hand.
/// </summary>
/// <param name="Facts">What the visit type's external inputs are computed from (the request's rule context).</param>
/// <param name="DueDays">Days until the visit is due (default: the visit type's).</param>
public sealed record VisitRequest(
    string VisitType,
    string SubjectType,
    Guid SubjectId,
    string? SubjectNumber,
    Guid? PartyId,
    string? PartyName,
    string TitleAr,
    string TitleEn,
    string? SiteAddress,
    double? Latitude,
    double? Longitude,
    string? Instructions,
    int? DueDays = null,
    JsonObject? Facts = null);

/// <summary>A visit as other modules and the applicant see it (no inspector names).</summary>
public sealed record VisitInfo(
    Guid Id,
    string Number,
    string VisitType,
    string TypeNameAr,
    string TypeNameEn,
    string Mode,
    string Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ScheduledAt,
    DateTimeOffset? DueAt,
    DateTimeOffset? CompletedAt,
    string? Outcome,
    string? OutcomeNameAr,
    string? OutcomeNameEn,
    string? OutcomeKind,
    string? SiteAddress);

/// <summary>Requests, cancels and lists visits (implemented by the Visits module); runs inside the caller's unit of work.</summary>
public interface IVisits
{
    Task<VisitInfo> CreateAsync(VisitRequest request, CancellationToken cancellationToken);

    /// <summary>Cancels a visit that is no longer needed. The listeners are not told: the caller is the one who cancelled it.</summary>
    Task CancelAsync(Guid visitId, string reason, CancellationToken cancellationToken);

    Task<IReadOnlyList<VisitInfo>> OfSubjectAsync(string subjectType, Guid subjectId, CancellationToken cancellationToken);
}

/// <summary>A visit was carried out (with its outcome and the recorded data), or will not be (cancelled).</summary>
/// <param name="OutcomeKind"><c>PASS</c>, <c>FAIL</c> or <c>OTHER</c>.</param>
public sealed record VisitClosed(
    Guid VisitId,
    string Number,
    string SubjectType,
    Guid SubjectId,
    bool Completed,
    string Status,
    string? Outcome,
    string? OutcomeKind,
    JsonObject Data,
    DateTimeOffset At);

/// <summary>Told when a visit closes, in the same unit of work (the workflow continues a request waiting for it).</summary>
public interface IVisitListener
{
    Task OnVisitClosedAsync(VisitClosed visit, CancellationToken cancellationToken);
}

/// <summary>The visit cannot be requested (unknown type, the external system refused it…).</summary>
public sealed class VisitException(string message) : Exception(message);
