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

    /// <summary>
    /// Other business apps of the same entity the row also serves (e.g. a document type used by two apps); empty for most
    /// rows. Staff of any of its apps see the row; changing it needs all of them.
    /// </summary>
    public List<string> SharedApps { get; set; } = [];

    /// <summary>Every business app of the row: its own first, then the shared ones.</summary>
    public IEnumerable<string> AllApps => BusinessApp is null ? [] : [BusinessApp, .. SharedApps];

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}
