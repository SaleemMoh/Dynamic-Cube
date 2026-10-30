namespace DynamicCube.Modules.AppsControl.Domain;

/// <summary>
/// A legal entity of an organization (<see cref="ConfigEntity.EntityCode"/>, the <see cref="GovernmentEntity"/>): it holds
/// branches, and requests, tasks and documents belong to one of its branches (ADR-0036).
/// </summary>
public sealed class Company : ConfigEntity
{
    public string? CrNumber { get; set; }

    public string? UnifiedNumber { get; set; }

    public string? VatNumber { get; set; }

    public int DisplayOrder { get; set; }
}

/// <summary>A business unit of a company (head office, regional office); its organization is the company's.</summary>
public sealed class Branch : ConfigEntity
{
    public string CompanyCode { get; set; } = string.Empty;

    /// <summary>Code of the org unit that serves the branch (tasks of its requests are routed under it); optional.</summary>
    public string? OrgUnitCode { get; set; }

    /// <summary>Domain code of class <c>REGION</c> (where the branch is), optional.</summary>
    public string? RegionCode { get; set; }

    public string? City { get; set; }

    /// <summary>The company's default branch: used when nothing else says which one. One per company.</summary>
    public bool IsDefault { get; set; }

    public int DisplayOrder { get; set; }
}
