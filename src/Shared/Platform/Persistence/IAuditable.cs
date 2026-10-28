namespace DynamicCube.Platform.Persistence;

/// <summary>Marks an entity whose changes are written to the audit log.</summary>
#pragma warning disable CA1040 // Marker interface is intentional: auditing is opt-in per entity type.
public interface IAuditable;
#pragma warning restore CA1040

/// <summary>Entity with standard audit columns, maintained automatically on save.</summary>
public interface IHasAuditColumns
{
    DateTimeOffset CreatedAt { get; set; }

    string CreatedBy { get; set; }

    DateTimeOffset? UpdatedAt { get; set; }

    string? UpdatedBy { get; set; }
}
