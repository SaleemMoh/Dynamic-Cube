namespace DynamicCube.Platform.Security;

/// <summary>An active company of the installation.</summary>
public sealed record CompanyInfo(string Code, string NameAr, string NameEn, string? EntityCode, int DisplayOrder);

/// <summary>An active branch of a company: where it is, the unit that serves it, and whether it is the company's default.</summary>
public sealed record BranchInfo(
    string Code,
    string CompanyCode,
    string NameAr,
    string NameEn,
    string? RegionCode,
    string? City,
    string? OrgUnitCode,
    bool IsDefault,
    int DisplayOrder);

/// <summary>Companies and branches as other modules read them (implemented by Apps Control).</summary>
public interface IOrganizationDirectory
{
    /// <summary>Active companies in display order; the first is the installation's default company.</summary>
    Task<IReadOnlyList<CompanyInfo>> CompaniesAsync(CancellationToken cancellationToken);

    /// <summary>Active branches in display order.</summary>
    Task<IReadOnlyList<BranchInfo>> BranchesAsync(CancellationToken cancellationToken);
}
