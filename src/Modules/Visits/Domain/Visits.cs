using System.Text.Json.Nodes;
using DynamicCube.Platform.Persistence;

namespace DynamicCube.Modules.Visits.Domain;

public enum VisitMode
{
    /// <summary>The entity's inspectors visit the site and record the result in the field app.</summary>
    Field,

    /// <summary>An external inspection system carries out the visit; its result comes by notification or polling.</summary>
    External,

    /// <summary>Staff record the result of a visit made outside the platform.</summary>
    Manual,
}

/// <summary>
/// A kind of visit (site inspection, safety check): who carries it out, the outcomes it can end with, what the inspector
/// records (form fields, photos, location, signature) and, for external visits, how the external system is called and
/// how its results are read.
/// </summary>
public sealed class VisitType : ConfigEntity
{
    public VisitMode Mode { get; set; }

    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    public List<VisitOutcome> Outcomes { get; set; } = [];

    /// <summary>What the inspector fills in besides the outcome.</summary>
    public List<VisitField> Fields { get; set; } = [];

    /// <summary>Photos the inspector must attach (0 for none).</summary>
    public int PhotosRequired { get; set; }

    /// <summary>The inspector's position must be captured.</summary>
    public bool RequireLocation { get; set; }

    /// <summary>The site representative signs on the inspector's device.</summary>
    public bool RequireSignature { get; set; }

    /// <summary>Employees of this role can be assigned the visits (default: anyone with the field app).</summary>
    public string? InspectorRole { get; set; }

    public int DueDays { get; set; } = 5;

    public ExternalVisitSettings? External { get; set; }
}

public enum OutcomeKind
{
    Pass,
    Fail,
    Other,
}

public sealed class VisitOutcome
{
    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public OutcomeKind Kind { get; set; }
}

public enum VisitFieldType
{
    Text,
    Textarea,
    Number,
    Date,
    Select,
    Checkbox,
}

public sealed class VisitField
{
    public string Code { get; set; } = string.Empty;

    public string LabelAr { get; set; } = string.Empty;

    public string LabelEn { get; set; } = string.Empty;

    public VisitFieldType Type { get; set; }

    public bool Required { get; set; }

    /// <summary>Select: the choices.</summary>
    public List<VisitFieldOption> Options { get; set; } = [];
}

public sealed class VisitFieldOption
{
    public string Code { get; set; } = string.Empty;

    public string LabelAr { get; set; } = string.Empty;

    public string LabelEn { get; set; } = string.Empty;
}

/// <summary>
/// How an external inspection system is used: the Integration Manager operation that books the visit (its inputs are
/// JSONLogic over the request's facts and the visit), the output holding the system's reference, and where results
/// carry their values, whether polled through <see cref="ResultOperation"/> or notified to the signed callback.
/// </summary>
public sealed class ExternalVisitSettings
{
    public string CreateOperation { get; set; } = string.Empty;

    /// <summary>
    /// Operation input → JSONLogic over <c>visit</c> (<c>number</c>, <c>siteAddress</c>, <c>latitude</c>, <c>dueDate</c>…) and
    /// the request's <c>data</c>, <c>party</c>…; none sends only <c>visitNumber</c>.
    /// </summary>
    public Dictionary<string, JsonNode?> Inputs { get; set; } = [];

    public string ReferenceOutput { get; set; } = "reference";

    /// <summary>Operation that answers with a visit's result; it receives <c>reference</c>. None: results only come by notification.</summary>
    public string? ResultOperation { get; set; }

    public int PollMinutes { get; set; } = 15;

    public ExternalResultFields Result { get; set; } = new();

    /// <summary>External outcome value → outcome code (values that are already outcome codes need no entry).</summary>
    public Dictionary<string, string> OutcomeMap { get; set; } = [];

    /// <summary>Secret name: result notifications must carry an HMAC-SHA256 signature made with <c>Secrets:{name}:key</c>.</summary>
    public string? CallbackSecret { get; set; }
}

/// <summary>Where a result carries its values (dot paths into the notification body or the result operation's outputs).</summary>
public sealed class ExternalResultFields
{
    public string Reference { get; set; } = "reference";

    public string Status { get; set; } = "status";

    /// <summary>The status value of a finished visit (other values are recorded as progress).</summary>
    public string CompletedValue { get; set; } = "COMPLETED";

    public string Outcome { get; set; } = "outcome";

    /// <summary>An object of recorded values, kept as the visit's data.</summary>
    public string? Data { get; set; } = "findings";

    public string? Report { get; set; } = "report";
}

public enum VisitStatus
{
    /// <summary>Waiting for an inspector and a date.</summary>
    Pending,
    Scheduled,

    /// <summary>The inspector started the visit.</summary>
    InProgress,

    /// <summary>Booked with the external system, waiting for its result.</summary>
    Sent,
    Completed,
    Cancelled,
}

/// <summary>A visit: for what, where, by whom and when, and what it found.</summary>
public sealed class Visit : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>E.g. <c>VIS-2026-000001</c>.</summary>
    public string Number { get; set; } = string.Empty;

    public Guid TypeId { get; set; }

    public string TypeCode { get; set; } = string.Empty;

    public VisitMode Mode { get; set; }

    public VisitStatus Status { get; set; }

    public string SubjectType { get; set; } = string.Empty;

    public Guid SubjectId { get; set; }

    public string? SubjectNumber { get; set; }

    public string TitleAr { get; set; } = string.Empty;

    public string TitleEn { get; set; } = string.Empty;

    public Guid? PartyId { get; set; }

    public string? PartyName { get; set; }

    public string? SiteAddress { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string? Instructions { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    public DateTimeOffset? DueAt { get; set; }

    /// <summary>User name of the assigned inspector.</summary>
    public string? Inspector { get; set; }

    public string? InspectorName { get; set; }

    public DateTimeOffset? ScheduledAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string? CompletedBy { get; set; }

    public string? Outcome { get; set; }

    /// <summary>The recorded field values (JSON object).</summary>
    public string Data { get; set; } = "{}";

    public string? Report { get; set; }

    public double? CapturedLatitude { get; set; }

    public double? CapturedLongitude { get; set; }

    public double? CapturedAccuracy { get; set; }

    public string? SignatureKey { get; set; }

    public string? SignedBy { get; set; }

    /// <summary>The field app's id for the report it sent, so a resent report is not recorded twice.</summary>
    public string? SubmissionId { get; set; }

    public string? ExternalReference { get; set; }

    public string? ExternalStatus { get; set; }

    public DateTimeOffset? NextPollAt { get; set; }

    public string? CancelReason { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>A photo taken during a visit (the file is in file storage).</summary>
public sealed class VisitPhoto
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid VisitId { get; set; }

    public string FileKey { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long Length { get; set; }

    public string? Caption { get; set; }

    public DateTimeOffset At { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>The visit's history, including external notifications that were refused.</summary>
public sealed class VisitEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid VisitId { get; set; }

    public DateTimeOffset At { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Actor { get; set; } = string.Empty;

    public string? Comment { get; set; }
}

public static class VisitEventTypes
{
    public const string Requested = "REQUESTED";
    public const string Sent = "SENT";
    public const string Scheduled = "SCHEDULED";
    public const string Rescheduled = "RESCHEDULED";
    public const string Started = "STARTED";
    public const string Completed = "COMPLETED";
    public const string Recorded = "RECORDED";
    public const string Cancelled = "CANCELLED";
    public const string ExternalProgress = "EXTERNAL_PROGRESS";
    public const string ExternalRefused = "EXTERNAL_REFUSED";
}

/// <summary>Counters for visit numbers (one row per year).</summary>
public sealed class VisitNumberSequence
{
    public string Key { get; set; } = string.Empty;

    public long Value { get; set; }
}
