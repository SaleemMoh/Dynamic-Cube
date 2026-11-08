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

/// <summary>
/// Companies, keyed like the legacy Apps Control by instance and company number: the change-item key is
/// <c>instid/cmp_num</c> (see <see cref="CompanyKey"/>), and the instance is the company's organization.
/// </summary>
internal sealed class CompanyApplier(AppsControlDbContext db) : ConfigApplier<Company, CompanyPayload>(db)
{
    public override string EntityType => AppsControlChangeTypes.Company;

    public override string Area => AppsControlAreas.System;

    public override string ResourceCode => AppsControlResources.Installation;

    protected override IQueryable<Company> Query(AppsControlDbContext context) => context.Companies;

    protected override IQueryable<Company> ListQuery(AppsControlDbContext context, string? parent) =>
        (parent is null ? context.Companies : context.Companies.Where(c => c.EntityCode == parent))
            .OrderBy(c => c.EntityCode).ThenBy(c => c.DisplayOrder).ThenBy(c => c.Code);

    protected override Task<Company?> FindAsync(AppsControlDbContext context, string entityCode, CancellationToken cancellationToken) =>
        _ = CompanyKey.TryParse(entityCode, out var instid, out var number)
            ? context.Companies.SingleOrDefaultAsync(c => c.EntityCode == instid && c.Code == number, cancellationToken)
            : Task.FromResult<Company?>(null);

    protected override void ValidateKey(string entityCode, ValidationErrors errors)
    {
        if (!CompanyKey.TryParse(entityCode, out var instid, out var number))
        {
            errors.Add("code", $"'{entityCode}' is not a company key: write the instance and the company number as instid/cmp_num (e.g. 5/2).");
            return;
        }

        errors.Code("entityCode", instid, ValidationErrors.GeneralCode());
        errors.Code("code", number, ValidationErrors.GeneralCode());
    }

    protected override string CodeFromKey(string entityCode) => CompanyKey.TryParse(entityCode, out _, out var number) ? number : entityCode;

    protected override string EntityCodeOf(Company entity) => entity.Key;

    protected override async Task ApplyAsync(Company entity, CompanyPayload payload, RefResolver refs, ValidationErrors errors, Company? existing, CancellationToken cancellationToken)
    {
        // The instance comes from the key.
        _ = CompanyKey.TryParse(payload.Code, out var instid, out _);
        if (payload.EntityCode is { Length: > 0 } given && given != instid)
        {
            errors.Add("entityCode", $"Company {payload.Code} belongs to instance {instid}.");
        }

        if (instid.Length > 0 && !await refs.Context.Entities.AnyAsync(e => e.Code == instid, cancellationToken)
            && !refs.IsPendingCreate(AppsControlChangeTypes.Entity, instid))
        {
            errors.Add("entityCode", $"Instance '{instid}' does not exist.");
        }

        if (payload.BusinessApp is not null || payload.BusinessApps is { Count: > 0 })
        {
            errors.Add("businessApp", "A company belongs to an organization, not to an app.");
        }

        errors.MaxLength("crNumber", payload.CrNumber, 50);
        errors.MaxLength("unifiedNumber", payload.UnifiedNumber, 50);
        errors.MaxLength("vatNumber", payload.VatNumber, 50);
        entity.EntityCode = instid;
        entity.BusinessApp = null;
        entity.CrNumber = Blank(payload.CrNumber);
        entity.UnifiedNumber = Blank(payload.UnifiedNumber);
        entity.VatNumber = Blank(payload.VatNumber);
        entity.DisplayOrder = payload.DisplayOrder;
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
        var key = entity.Key;
        if (await context.Branches.AnyAsync(b => b.EntityCode == entity.EntityCode && b.CompanyCode == entity.Code, cancellationToken))
        {
            throw new ChangeSetException($"Company {key} still has branches; delete or move them first.");
        }

        if (await context.Roles.AnyAsync(r => r.EntityCode == entity.EntityCode && r.CompanyCode == entity.Code, cancellationToken))
        {
            throw new ChangeSetException($"Company {key} still has roles; delete them first.");
        }

        if (await context.Roles.Where(r => r.OverrideCompanies.Contains(key)).Select(r => r.Code).FirstOrDefaultAsync(cancellationToken) is { } role)
        {
            throw new ChangeSetException($"Role {role} differs in company {key}; remove that override first.");
        }
    }

    internal static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class BranchPayload : ConfigPayload
{
    /// <summary>The company's key, <c>instid/cmp_num</c>.</summary>
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
        (parent is null ? context.Branches : context.Branches.Where(b => b.EntityCode + "/" + b.CompanyCode == parent))
            .OrderBy(b => b.EntityCode).ThenBy(b => b.CompanyCode).ThenBy(b => b.DisplayOrder).ThenBy(b => b.Code);

    protected override async Task ApplyAsync(Branch entity, BranchPayload payload, RefResolver refs, ValidationErrors errors, Branch? existing, CancellationToken cancellationToken)
    {
        if (payload.BusinessApp is not null || payload.BusinessApps is { Count: > 0 })
        {
            errors.Add("businessApp", "A branch belongs to a company, not to an app.");
        }

        errors.Required("companyCode", payload.CompanyCode);
        errors.MaxLength("city", payload.City, 200);
        var companyCode = payload.CompanyCode?.Trim() ?? string.Empty;
        if (existing is not null && existing.CompanyKey != companyCode)
        {
            errors.Add("companyCode", "A branch cannot move to another company; create a new branch instead.");
        }

        if (companyCode.Length > 0 && !CompanyKey.TryParse(companyCode, out _, out _))
        {
            errors.Add("companyCode", $"'{companyCode}' is not a company key (instid/cmp_num).");
        }

        _ = CompanyKey.TryParse(companyCode, out var instid, out var number);
        var known = await refs.Context.Companies.AsNoTracking().AnyAsync(c => c.EntityCode == instid && c.Code == number, cancellationToken);
        if (!known && companyCode.Length > 0 && !refs.IsPendingCreate(AppsControlChangeTypes.Company, companyCode))
        {
            errors.Add("companyCode", $"Company '{companyCode}' does not exist.");
        }

        if (payload.EntityCode is { Length: > 0 } org && instid.Length > 0 && org != instid)
        {
            errors.Add("entityCode", $"Company {companyCode} belongs to instance {instid}.");
        }

        await refs.OrgUnitAsync(CompanyApplier.Blank(payload.OrgUnitCode), "orgUnitCode", errors, cancellationToken);
        await refs.DomainAsync(RegionClass, CompanyApplier.Blank(payload.RegionCode), "regionCode", errors, cancellationToken);
        entity.EntityCode = instid.Length > 0 ? instid : entity.EntityCode;
        entity.BusinessApp = null;
        entity.CompanyCode = number;
        entity.OrgUnitCode = CompanyApplier.Blank(payload.OrgUnitCode);
        entity.RegionCode = CompanyApplier.Blank(payload.RegionCode);
        entity.City = CompanyApplier.Blank(payload.City);
        entity.DisplayOrder = payload.DisplayOrder;
        entity.IsDefault = payload.IsDefault;
        if (payload.IsDefault && !refs.IsValidating)
        {
            // One default per company.
            foreach (var other in await refs.Context.Branches.Where(b => b.EntityCode == instid && b.CompanyCode == number && b.Code != entity.Code && b.IsDefault).ToListAsync(cancellationToken))
            {
                other.IsDefault = false;
            }
        }
    }

    protected override Task<BranchPayload> ToPayloadAsync(AppsControlDbContext context, Branch entity, CancellationToken cancellationToken) =>
        Task.FromResult(new BranchPayload
        {
            CompanyCode = entity.CompanyKey,
            OrgUnitCode = entity.OrgUnitCode,
            RegionCode = entity.RegionCode,
            City = entity.City,
            IsDefault = entity.IsDefault,
            DisplayOrder = entity.DisplayOrder,
        });
}
