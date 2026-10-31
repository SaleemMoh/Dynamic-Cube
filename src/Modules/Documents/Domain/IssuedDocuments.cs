using DynamicCube.Platform.Persistence;

namespace DynamicCube.Modules.Documents.Domain;

public enum DocumentStatus
{
    Active,
    Suspended,
    Cancelled,
    Expired,
}

/// <summary>
/// A document issued to a holder: number, status, dates, the current version and the code its QR verification link
/// carries. Its content lives in immutable versions.
/// </summary>
public sealed class IssuedDocument : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid DocumentTypeId { get; set; }

    public string TypeCode { get; set; } = string.Empty;

    public string Number { get; set; } = string.Empty;

    public Guid HolderPartyId { get; set; }

    public string HolderNameAr { get; set; } = string.Empty;

    public string HolderNameEn { get; set; } = string.Empty;

    public DocumentStatus Status { get; set; }

    public DateTimeOffset IssuedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public int CurrentVersion { get; set; }

    /// <summary>Random, unguessable code of the public verification link.</summary>
    public string VerificationCode { get; set; } = string.Empty;

    /// <summary>The company the document belongs to (that of the request it was issued for, ADR-0036 step 6).</summary>
    public string? CompanyCode { get; set; }

    /// <summary>The branch of that company: staff see the document when their roles cover it.</summary>
    public string? BranchCode { get; set; }

    /// <summary>Expiry reminders already sent (days before expiry).</summary>
    public int[] AlertsSent { get; set; } = [];

    public DateTimeOffset? StatusChangedAt { get; set; }
}

/// <summary>One immutable state of a document: its data and lines, the holder as they were, and the printed PDF.</summary>
public sealed class DocumentVersion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid DocumentId { get; set; }

    public int Version { get; set; }

    public string Data { get; set; } = "{}";

    public string Lines { get; set; } = "[]";

    /// <summary>The holder's names and identifiers at issue.</summary>
    public string Holder { get; set; } = "{}";

    public DateTimeOffset IssuedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public string IssuedBy { get; set; } = string.Empty;

    public string SourceType { get; set; } = string.Empty;

    public Guid SourceId { get; set; }

    public string? SourceNumber { get; set; }

    public string? Reason { get; set; }

    public Guid? TemplateId { get; set; }

    /// <summary>Stored PDF, once rendered.</summary>
    public string? FileKey { get; set; }

    public DateTimeOffset? RenderedAt { get; set; }

    public string? RenderError { get; set; }
}

public sealed class DocumentEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid DocumentId { get; set; }

    public DateTimeOffset At { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Actor { get; set; } = string.Empty;

    public int? Version { get; set; }

    public string? Comment { get; set; }
}

public static class DocumentEventTypes
{
    public const string Issued = "ISSUED";
    public const string Amended = "AMENDED";
    public const string Suspended = "SUSPENDED";
    public const string Reinstated = "REINSTATED";
    public const string Cancelled = "CANCELLED";
    public const string Expired = "EXPIRED";
    public const string ExpiryReminder = "EXPIRY_REMINDER";
}

/// <summary>Counters for document numbers (one row per prefix and year).</summary>
public sealed class DocumentNumberSequence
{
    public string Key { get; set; } = string.Empty;

    public long Value { get; set; }
}
