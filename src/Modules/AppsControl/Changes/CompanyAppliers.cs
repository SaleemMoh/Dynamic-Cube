using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.ChangeSets;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Changes;

public sealed class CompanyPayload : ConfigPayload
{
    public string? CrNumber { get; set; }

    public string? UnifiedNumber { get; set; }

    public string? VatNumber { get; set; }

    public int DisplayOrder { get; set; }
}

/// <summary>Companies of an organization (its <c>entityCode</c>; the default organization when none is given).</summary>
internal sealed class CompanyApplier(AppsControlDbContext db) : ConfigApplier<Company, CompanyPayload>(db)
{
    public override string EntityType => AppsControlChangeTypes.Company;

    public override string Area => AppsControlAreas.System;

    public override string ResourceCode => AppsControlResources.Installation;

    protected override IQueryable<Company> Query(AppsControlDbContext context) => context.Companies;

    protected override IQueryable<Company> ListQuery(AppsControlDbContext context, string? parent) =>
        (parent is null ? context.Companies : context.Companies.Where(c => c.EntityCode == parent)).OrderBy(c => c.DisplayOrder).ThenBy(c => c.Code);

    protected override Task ApplyAsync(Company entity, CompanyPayload payload, RefResolver refs, ValidationErrors errors, Company? existing, CancellationToken cancellationToken)
    {
        if (payload.BusinessApp is not null || payload.BusinessApps is { Count: > 0 })
        {
            errors.Add("businessApp", "A company belongs to an organization, not to an app.");
        }

        errors.MaxLength("crNumber", payload.CrNumber, 50);
        errors.MaxLength("unifiedNumber", payload.UnifiedNumber, 50);
        errors.MaxLength("vatNumber", payload.VatNumber, 50);
        entity.EntityCode ??= GovernmentEntity.SingletonCode;
        entity.BusinessApp = null;
        entity.CrNumber = Blank(payload.CrNumber);
        entity.UnifiedNumber = Blank(payload.UnifiedNumber);
        entity.VatNumber = Blank(payload.VatNumber);
        entity.DisplayOrder = payload.DisplayOrder;
        return Task.CompletedTask;
    }

    protected override Task<CompanyPayload> ToPayloadAsync(AppsControlDbContext context, Company entity, CancellationToken cancellationToken) =>
        Task.FromResult(new CompanyPayload
        {
            CrNumber = entity.CrNumber,
            UnifiedNumber = entity.UnifiedNumber,
            VatNumber = entity.VatNumber,
            DisplayOrder = entity.DisplayOrder,
        });

    protected override async Task BeforeDeleteAsync(AppsControlDbContext context, Company entity, CancellationToken cancellationToken)
    {
        if (await context.Branches.AnyAsync(b => b.CompanyCode == entity.Code, cancellationToken))
        {
            throw new ChangeSetException($"Company {entity.Code} still has branches; delete or move them first.");
        }
    }

    internal static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class BranchPayload : ConfigPayload
{
    public string CompanyCode { get; set; } = string.Empty;

    public string? OrgUnitCode { get; set; }

    public string? RegionCode { get; set; }

    public string? City { get; set; }

    /// <summary>The company's default branch; setting it clears the flag on the company's other branches.</summary>
    public bool IsDefault { get; set; }

    public int DisplayOrder { get; set; }
}

/// <summary>Branches of a company; a branch takes its company's organization.</summary>
internal sealed class BranchApplier(AppsControlDbContext db) : ConfigApplier<Branch, BranchPayload>(db)
{
    public const string RegionClass = "REGION";

    public override string EntityType => AppsControlChangeTypes.Branch;

    public override string Area => AppsControlAreas.System;

    public override string ResourceCode => AppsControlResources.Installation;

    protected override IQueryable<Branch> Query(AppsControlDbContext context) => context.Branches;

    protected override IQueryable<Branch> ListQuery(AppsControlDbContext context, string? parent) =>
        (parent is null ? context.Branches : context.Branches.Where(b => b.CompanyCode == parent))
            .OrderBy(b => b.CompanyCode).ThenBy(b => b.DisplayOrder).ThenBy(b => b.Code);

    protected override async Task ApplyAsync(Branch entity, BranchPayload payload, RefResolver refs, ValidationErrors errors, Branch? existing, CancellationToken cancellationToken)
    {
        if (payload.BusinessApp is not null || payload.BusinessApps is { Count: > 0 })
        {
            errors.Add("businessApp", "A branch belongs to a company, not to an app.");
        }

        errors.Required("companyCode", payload.CompanyCode);
        errors.MaxLength("city", payload.City, 200);
        var companyCode = payload.CompanyCode?.Trim() ?? string.Empty;
        if (existing is not null && existing.CompanyCode != companyCode)
        {
            errors.Add("companyCode", "A branch cannot move to another company; create a new branch instead.");
        }

        var company = await refs.Context.Companies.AsNoTracking().Where(c => c.Code == companyCode).Select(c => new { c.EntityCode }).SingleOrDefaultAsync(cancellationToken);
        if (company is null && companyCode.Length > 0 && !refs.IsPendingCreate(AppsControlChangeTypes.Company, companyCode))
        {
            errors.Add("companyCode", $"Company '{companyCode}' does not exist.");
        }

        if (company is not null && payload.EntityCode is { Length: > 0 } org && org != company.EntityCode)
        {
            errors.Add("entityCode", $"Company {companyCode} belongs to organization {company.EntityCode}.");
        }

        await refs.OrgUnitAsync(CompanyApplier.Blank(payload.OrgUnitCode), "orgUnitCode", errors, cancellationToken);
        await refs.DomainAsync(RegionClass, CompanyApplier.Blank(payload.RegionCode), "regionCode", errors, cancellationToken);
        entity.EntityCode = company?.EntityCode ?? entity.EntityCode ?? GovernmentEntity.SingletonCode;
        entity.BusinessApp = null;
        entity.CompanyCode = companyCode;
        entity.OrgUnitCode = CompanyApplier.Blank(payload.OrgUnitCode);
        entity.RegionCode = CompanyApplier.Blank(payload.RegionCode);
        entity.City = CompanyApplier.Blank(payload.City);
        entity.DisplayOrder = payload.DisplayOrder;
        entity.IsDefault = payload.IsDefault;
        if (payload.IsDefault && !refs.IsValidating)
        {
            // One default per company.
            foreach (var other in await refs.Context.Branches.Where(b => b.CompanyCode == companyCode && b.Code != entity.Code && b.IsDefault).ToListAsync(cancellationToken))
            {
                other.IsDefault = false;
            }
        }
    }

    protected override Task<BranchPayload> ToPayloadAsync(AppsControlDbContext context, Branch entity, CancellationToken cancellationToken) =>
        Task.FromResult(new BranchPayload
        {
            CompanyCode = entity.CompanyCode,
            OrgUnitCode = entity.OrgUnitCode,
            RegionCode = entity.RegionCode,
            City = entity.City,
            IsDefault = entity.IsDefault,
            DisplayOrder = entity.DisplayOrder,
        });
}
