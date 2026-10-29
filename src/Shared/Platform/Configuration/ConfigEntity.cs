using DynamicCube.Platform.Persistence;

namespace DynamicCube.Platform.Configuration;

/// <summary>Who maintains a configuration row: product upgrades (<see cref="System"/>) or the entity's administrators.</summary>
public enum Ownership
{
    System,
    Entity,
}

/// <summary>Common shape of configuration rows in every module: stable code, bilingual name, ownership, audit columns.</summary>
public abstract class ConfigEntity : IAuditable, IHasAuditColumns
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Stable business key, used by change sets and configuration packages across environments.</summary>
    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public Ownership Owner { get; set; } = Ownership.Entity;

    /// <summary>
    /// The government entity the row belongs to; null for rows of the whole installation (system level). See
    /// <see cref="ConfigScope"/>.
    /// </summary>
    public string? EntityCode { get; set; }

    /// <summary>The business app the row belongs to (app level); null for entity and system rows.</summary>
    public string? BusinessApp { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}
