using DynamicCube.Platform.Persistence;

namespace DynamicCube.Modules.Services.Domain;

/// <summary>Fixed status categories used by the engine, SLA and reports; users see display statuses.</summary>
public enum StatusCategory
{
    Draft,
    Submitted,
    InProgress,
    PendingCustomer,
    PendingExternal,

    /// <summary>A bill was issued; processing continues once it is paid.</summary>
    PendingPayment,
    Approved,
    Rejected,
    Cancelled,
    Closed,
}

/// <summary>One fee item as charged: net amount, VAT and total, in SAR.</summary>
public sealed record FeeLine(string Code, string NameAr, string NameEn, decimal Net, decimal Vat, decimal Total, bool IsEstimate);

/// <summary>
/// One application, from draft to closure. Pinned to the service version it started on; <see cref="Data"/> holds the
/// answers keyed by question code.
/// </summary>
public sealed class ServiceRequest : IAuditable, IHasAuditColumns
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Assigned on submission, e.g. <c>CLS-2026-000123</c>.</summary>
    public string? Number { get; set; }

    public Guid ServiceId { get; set; }

    public string ServiceCode { get; set; } = string.Empty;

    public Guid ServiceVersionId { get; set; }

    public int VersionNumber { get; set; }

    public string? LevelCode { get; set; }

    /// <summary>The individual who filled in the request.</summary>
    public Guid SubmitterPartyId { get; set; }

    public string SubmitterName { get; set; } = string.Empty;

    /// <summary>The party the request is for (the individual or the establishment they act for).</summary>
    public Guid BeneficiaryPartyId { get; set; }

    public string BeneficiaryNameAr { get; set; } = string.Empty;

    public string BeneficiaryNameEn { get; set; } = string.Empty;

    public StatusCategory StatusCategory { get; set; }

    /// <summary>Configurable status shown to users (e.g. <c>DRAFT</c>, <c>SUBMITTED</c>, <c>UNDER_REVIEW</c>).</summary>
    public string DisplayStatus { get; set; } = "DRAFT";

    public string Data { get; set; } = "{}";

    public string? CurrentStep { get; set; }

    public List<FeeLine> Fees { get; set; } = [];

    public decimal FeeTotal { get; set; }

    public DateTimeOffset? DraftExpiresAt { get; set; }

    /// <summary>The document a follow-up request is made for (services with a document type).</summary>
    public Guid? SourceDocumentId { get; set; }

    public string? SourceDocumentNumber { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}

/// <summary>The request's timeline: every transition, action and comment.</summary>
public sealed class RequestEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid RequestId { get; set; }

    public DateTimeOffset At { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public StatusCategory? FromStatus { get; set; }

    public StatusCategory? ToStatus { get; set; }

    public string? Comment { get; set; }
}

public static class RequestEventTypes
{
    public const string Created = "CREATED";
    public const string VersionChanged = "VERSION_CHANGED";
    public const string Submitted = "SUBMITTED";
    public const string Cancelled = "CANCELLED";
    public const string Expired = "EXPIRED";
    public const string Resubmitted = "RESUBMITTED";
}

/// <summary>Counters for request numbers (one row per prefix and year).</summary>
public sealed class NumberSequence
{
    public string Key { get; set; } = string.Empty;

    public long Value { get; set; }
}
