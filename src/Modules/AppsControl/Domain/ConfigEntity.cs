using DynamicCube.Platform.Persistence;

namespace DynamicCube.Modules.AppsControl.Domain;

/// <summary>Who maintains a configuration row: product upgrades (<see cref="System"/>) or the entity's administrators.</summary>
public enum Ownership
{
    System,
    Entity,
}

/// <summary>Common shape of Apps Control configuration rows: stable code, bilingual name, ownership, audit columns.</summary>
public abstract class ConfigEntity : IAuditable, IHasAuditColumns
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Stable business key, used by change sets and configuration packages across environments.</summary>
    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public Ownership Owner { get; set; } = Ownership.Entity;

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}
