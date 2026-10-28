using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Domain;

/// <summary>Configurable kind of org unit (sector, general department, department, branch, committee…).</summary>
public sealed class OrgUnitType : ConfigEntity
{
    public int DisplayOrder { get; set; }

    /// <summary>Codes of the unit types allowed as parent; when set, a parent of one of these types is required. Empty means no constraint.</summary>
    public List<string> AllowedParentTypes { get; set; } = [];
}

/// <summary>Node of the organization structure.</summary>
public sealed class OrgUnit : ConfigEntity
{
    public Guid OrgUnitTypeId { get; set; }

    public OrgUnitType? OrgUnitType { get; set; }

    public Guid? ParentId { get; set; }

    public OrgUnit? Parent { get; set; }

    /// <summary>Materialized path of unit ids (root first) for subtree queries.</summary>
    public LTree Path { get; set; }

    /// <summary>Free attributes (region, amanah, cost centre…) usable in grant rules.</summary>
    public string Attributes { get; set; } = "{}";

    public DateOnly? EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public static string PathLabel(Guid id) => id.ToString("N");
}

/// <summary>Generic position used for routing (manager, analyst, auditor, inspector…).</summary>
public sealed class Position : ConfigEntity
{
}
