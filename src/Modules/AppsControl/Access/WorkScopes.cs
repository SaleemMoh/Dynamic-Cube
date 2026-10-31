using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Access;

/// <summary>
/// The employee's work scope (ADR-0036 step 6): the company they work in and the branches of it their roles cover. Outside
/// the internal system (portal, worker) and for background work nothing is filtered.
/// </summary>
internal sealed class WorkScopes(AccessResolver access, ICurrentUser currentUser, AppsControlHostOptions host) : IWorkScope
{
    public async Task<WorkScope> CurrentAsync(CancellationToken cancellationToken)
    {
        if (!host.ManagesUsers || !currentUser.IsAuthenticated || currentUser.UserId == SystemUser.Id)
        {
            return WorkScope.Unrestricted;
        }

        var snapshot = await access.GetAsync(cancellationToken);
        return new WorkScope(true, snapshot.Company, snapshot.Branches?.ToHashSet(StringComparer.Ordinal));
    }
}

/// <summary>Active companies and branches for other modules.</summary>
internal sealed class OrganizationDirectory(AppsControlDbContext context) : IOrganizationDirectory
{
    private IReadOnlyList<CompanyInfo>? _companies;
    private IReadOnlyList<BranchInfo>? _branches;

    public async Task<IReadOnlyList<CompanyInfo>> CompaniesAsync(CancellationToken cancellationToken) =>
        _companies ??= await context.Companies.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Code)
            .Select(c => new CompanyInfo(c.Code, c.NameAr, c.NameEn, c.EntityCode, c.DisplayOrder))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BranchInfo>> BranchesAsync(CancellationToken cancellationToken) =>
        _branches ??= await context.Branches.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.DisplayOrder).ThenBy(b => b.Code)
            .Select(b => new BranchInfo(b.Code, b.CompanyCode, b.NameAr, b.NameEn, b.RegionCode, b.City, b.OrgUnitCode, b.IsDefault, b.DisplayOrder))
            .ToListAsync(cancellationToken);
}
