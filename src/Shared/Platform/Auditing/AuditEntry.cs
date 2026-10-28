namespace DynamicCube.Platform.Auditing;

/// <summary>One audited change to a business or configuration entity.</summary>
public sealed class AuditEntry
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAt { get; set; }

    public required string UserId { get; set; }

    public required string AppCode { get; set; }

    /// <summary>CLR entity name, e.g. <c>Domain</c>.</summary>
    public required string EntityType { get; set; }

    /// <summary>Primary key value(s) of the changed entity.</summary>
    public required string EntityId { get; set; }

    public AuditAction Action { get; set; }

    /// <summary>JSON object of changed properties: <c>{ "prop": { "old": ..., "new": ... } }</c>.</summary>
    public required string Changes { get; set; }

    public string? CorrelationId { get; set; }
}

public enum AuditAction
{
    Created,
    Updated,
    Deleted,
}
