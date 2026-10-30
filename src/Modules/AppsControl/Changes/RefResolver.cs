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

    public Task<Guid?> CompanyAsync(string? code, string field, ValidationErrors errors, CancellationToken ct) =>
        ResolveAsync(Context.Companies, AppsControlChangeTypes.Company, code, field, errors, ct);

    public Task<Guid?> BranchAsync(string? code, string field, ValidationErrors errors, CancellationToken ct) =>
        ResolveAsync(Context.Branches, AppsControlChangeTypes.Branch, code, field, errors, ct);

    public Task<Guid?> OrgUnitTypeAsync(string? code, string field, ValidationErrors errors, CancellationToken ct) =>
        ResolveAsync(Context.OrgUnitTypes, AppsControlChangeTypes.OrgUnitType, code, field, errors, ct);

    public Task<Guid?> OrgUnitAsync(string? code, string field, ValidationErrors errors, CancellationToken ct) =>
        ResolveAsync(Context.OrgUnits, AppsControlChangeTypes.OrgUnit, code, field, errors, ct);

    public Task<Guid?> PositionAsync(string? code, string field, ValidationErrors errors, CancellationToken ct) =>
        ResolveAsync(Context.Positions, AppsControlChangeTypes.Position, code, field, errors, ct);

    public Task<Guid?> RoleAsync(string? code, string field, ValidationErrors errors, CancellationToken ct) =>
        ResolveAsync(Context.Roles, AppsControlChangeTypes.Role, code, field, errors, ct);

    public Task<Guid?> ResourceAsync(string? code, string field, ValidationErrors errors, CancellationToken ct) =>
        ResolveAsync(Context.Resources, AppsControlChangeTypes.Resource, code, field, errors, ct);

    public Task<Guid?> MenuGroupAsync(string? code, string field, ValidationErrors errors, CancellationToken ct) =>
        ResolveAsync(Context.MenuGroups, AppsControlChangeTypes.MenuGroup, code, field, errors, ct);

    public Task<Guid?> MenuAsync(string? code, string field, ValidationErrors errors, CancellationToken ct) =>
        ResolveAsync(Context.Menus, AppsControlChangeTypes.Menu, code, field, errors, ct);

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

        var id = await Context.Domains.Where(d => d.Class!.Code == classCode && d.Code == code).Select(d => (Guid?)d.Id).SingleOrDefaultAsync(ct);
        if (id is null && IsPendingCreate(AppsControlChangeTypes.Domain, DomainKey.Format(classCode, code)))
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
internal static class DomainKey
{
    public static string Format(string classCode, string code) => $"{classCode}/{code}";

    public static bool TryParse(string entityCode, out string classCode, out string code)
    {
        var index = entityCode.IndexOf('/', StringComparison.Ordinal);
        if (index <= 0 || index == entityCode.Length - 1)
        {
            classCode = code = string.Empty;
            return false;
        }

        classCode = entityCode[..index];
        code = entityCode[(index + 1)..];
        return true;
    }
}
