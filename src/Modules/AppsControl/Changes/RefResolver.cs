using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.ChangeSets;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Changes;

/// <summary>
/// Resolves references by code. While validating a draft, rows created by earlier items of the same change set count
/// as existing (their id is not known yet, so <see cref="Guid.Empty"/> is returned).
/// </summary>
internal sealed class RefResolver(AppsControlDbContext context, ChangeSet? draft) : ConfigRefs<AppsControlDbContext>(context, draft)
{
    public Task<Guid?> AppAsync(string? code, string field, ValidationErrors errors, CancellationToken ct) =>
        ResolveAsync(Context.Apps, AppsControlChangeTypes.App, code, field, errors, ct);

    /// <summary>A company by its key, <c>instid/cmp_num</c>.</summary>
    public Task<Guid?> CompanyAsync(string? code, string field, ValidationErrors errors, CancellationToken ct)
    {
        _ = CompanyKey.TryParse(code, out var instid, out var number);
        return ResolveMatchAsync(Context.Companies.Where(c => c.EntityCode == instid && c.Code == number), AppsControlChangeTypes.Company, code, field, errors, ct);
    }

    public Task<Guid?> BranchAsync(string? code, string field, ValidationErrors errors, CancellationToken ct) =>
        ResolveAsync(Context.Branches, AppsControlChangeTypes.Branch, code, field, errors, ct);

    public Task<Guid?> OrgUnitTypeAsync(string? code, string field, ValidationErrors errors, CancellationToken ct) =>
        ResolveAsync(Context.OrgUnitTypes, AppsControlChangeTypes.OrgUnitType, code, field, errors, ct);

    public Task<Guid?> OrgUnitAsync(string? code, string field, ValidationErrors errors, CancellationToken ct) =>
        ResolveAsync(Context.OrgUnits, AppsControlChangeTypes.OrgUnit, code, field, errors, ct);

    public Task<Guid?> PositionAsync(string? code, string field, ValidationErrors errors, CancellationToken ct) =>
        ResolveAsync(Context.Positions, AppsControlChangeTypes.Position, code, field, errors, ct);

    /// <summary>A role by its key: the code of a role of no company, or <c>instid/cmp_num/role_code</c>.</summary>
    public Task<Guid?> RoleAsync(string? code, string field, ValidationErrors errors, CancellationToken ct)
    {
        _ = RoleKey.TryParse(code, out var instid, out var number, out var role);
        var matches = number.Length == 0
            ? Context.Roles.Where(r => r.CompanyCode == null && r.Code == role)
            : Context.Roles.Where(r => r.EntityCode == instid && r.CompanyCode == number && r.Code == role);
        return ResolveMatchAsync(matches, AppsControlChangeTypes.Role, code, field, errors, ct);
    }

    public Task<Guid?> ResourceAsync(string? code, string field, ValidationErrors errors, CancellationToken ct) =>
        ResolveAsync(Context.Resources, AppsControlChangeTypes.Resource, code, field, errors, ct);

    /// <summary>A menu group by its key, <c>appsid/menu_group_code</c> (the code alone for a group of no app).</summary>
    public Task<Guid?> MenuGroupAsync(string? code, string field, ValidationErrors errors, CancellationToken ct)
    {
        var (app, group) = AppScopedKey.Parse(code ?? string.Empty);
        return ResolveMatchAsync(MenuTrees.InTree(Context.MenuGroups, app).Where(g => g.Code == group), AppsControlChangeTypes.MenuGroup, code, field, errors, ct);
    }

    /// <summary>A menu item by its key, <c>appsid/menu_code</c> (the code alone for an item of no app).</summary>
    public Task<Guid?> MenuAsync(string? code, string field, ValidationErrors errors, CancellationToken ct)
    {
        var (app, menu) = AppScopedKey.Parse(code ?? string.Empty);
        return ResolveMatchAsync(MenuTrees.InTree(Context.Menus, app).Where(m => m.Code == menu), AppsControlChangeTypes.Menu, code, field, errors, ct);
    }

    public Task<Guid?> ClassAsync(string? code, string field, ValidationErrors errors, CancellationToken ct) =>
        ResolveAsync(Context.Classes, AppsControlChangeTypes.Class, code, field, errors, ct);

    public Task<Guid?> DataObjectAsync(string? code, string field, ValidationErrors errors, CancellationToken ct) =>
        ResolveAsync(Context.DataObjects, AppsControlChangeTypes.DataObject, code, field, errors, ct);

    public Task<Guid?> LovAsync(string? code, string field, ValidationErrors errors, CancellationToken ct) =>
        ResolveAsync(Context.Lovs, AppsControlChangeTypes.Lov, code, field, errors, ct);

    /// <summary>Resolves a domain value of a class (pending creates count while validating).</summary>
    public async Task<Guid?> DomainAsync(string classCode, string? code, string field, ValidationErrors errors, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        // A value of every company first, then a company's own value of that code.
        var id = await Context.Domains.Where(d => d.Class!.Code == classCode && d.Code == code)
            .OrderBy(d => d.CompanyCode != null).Select(d => (Guid?)d.Id).FirstOrDefaultAsync(ct);
        if (id is null && (IsPendingCreate(AppsControlChangeTypes.Domain, DomainKey.Format(classCode, code))
            || (Draft?.Items.Any(i => i.EntityType == AppsControlChangeTypes.Domain && i.Operation == ChangeOperation.Create
                && DomainKey.TryParse(i.EntityCode, out var c, out _, out var v) && c == classCode && v == code) ?? false)))
        {
            return Guid.Empty;
        }

        if (id is null)
        {
            errors.Add(field, $"'{code}' is not a value of class {classCode}.");
        }

        return id;
    }
}

/// <summary>Composite key of a domain value: <c>CLASS/CODE</c>.</summary>
/// <summary>
/// The key of a class value: <c>CLASS/VALUE</c> for a value of every company, <c>CLASS/instid/cmp_num/VALUE</c> for a
/// value of one company (the legacy <c>DD_COMPANY_DOMAIN</c> key).
/// </summary>
internal static class DomainKey
{
    public static string Format(string classCode, string code) => $"{classCode}/{code}";

    public static string Format(string classCode, string? companyKey, string code) =>
        companyKey is null ? Format(classCode, code) : $"{classCode}/{companyKey}/{code}";

    public static bool TryParse(string entityCode, out string classCode, out string code) =>
        TryParse(entityCode, out classCode, out _, out code);

    /// <summary>Splits a key; <paramref name="companyKey"/> is null for a value of every company.</summary>
    public static bool TryParse(string entityCode, out string classCode, out string? companyKey, out string code)
    {
        var parts = entityCode.Split('/');
        classCode = code = string.Empty;
        companyKey = null;
        if (parts.Any(p => p.Length == 0) || parts.Length is not (2 or 4))
        {
            return false;
        }

        classCode = parts[0];
        code = parts[^1];
        companyKey = parts.Length == 4 ? $"{parts[1]}/{parts[2]}" : null;
        return true;
    }
}
