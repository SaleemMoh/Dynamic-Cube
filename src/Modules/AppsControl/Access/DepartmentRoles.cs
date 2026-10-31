using DynamicCube.Modules.AppsControl.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Access;

/// <summary>Roles people receive from the units they are placed in (an org unit's default roles, ADR-0036).</summary>
internal static class DepartmentRoles
{
    /// <summary>
    /// The default roles of the units the user is placed in today, each with where it counts: the company and branch the
    /// unit serves (the branch linked to the unit or to the nearest unit above it), or every company (no branch).
    /// </summary>
    public static async Task<List<(string Role, string Company, string? Branch)>> OfUserByCompanyAsync(AppsControlDbContext context, Guid userId, DateOnly today, CancellationToken cancellationToken) =>
        (await OfUsersByCompanyAsync(context, [userId], today, cancellationToken)).GetValueOrDefault(userId) ?? [];

    /// <summary>The department roles of several users with where each counts, by user.</summary>
    public static async Task<Dictionary<Guid, List<(string Role, string Company, string? Branch)>>> OfUsersByCompanyAsync(
        AppsControlDbContext context, IReadOnlyCollection<Guid> userIds, DateOnly today, CancellationToken cancellationToken)
    {
        var units = await context.UserAssignments.AsNoTracking()
            .Where(a => userIds.Contains(a.UserId) && a.OrgUnit!.IsActive && a.OrgUnit.DefaultRoles.Count > 0
                && (a.EffectiveFrom == null || a.EffectiveFrom <= today) && (a.EffectiveTo == null || a.EffectiveTo >= today))
            .Select(a => new { a.UserId, a.OrgUnit!.Path, a.OrgUnit.DefaultRoles })
            .ToListAsync(cancellationToken);
        if (units.Count == 0)
        {
            return [];
        }

        var branches = (await context.Branches.AsNoTracking()
            .Where(b => b.IsActive && b.OrgUnitCode != null)
            .Join(context.OrgUnits.AsNoTracking(), b => b.OrgUnitCode, u => u.Code, (b, u) => new { b.Code, b.CompanyCode, u.Path })
            .ToListAsync(cancellationToken))
            .Select(b => (b.Code, b.CompanyCode, Path: b.Path.ToString()))
            .ToList();
        var result = new Dictionary<Guid, List<(string Role, string Company, string? Branch)>>();
        foreach (var unit in units)
        {
            var path = unit.Path.ToString();
            var branch = branches
                .Where(b => path == b.Path || path.StartsWith(b.Path + ".", StringComparison.Ordinal))
                .OrderByDescending(b => b.Path.Length)
                .Select(b => ((string Code, string Company)?)(b.Code, b.CompanyCode))
                .FirstOrDefault();
            var list = result.TryGetValue(unit.UserId, out var existing) ? existing : result[unit.UserId] = [];
            list.AddRange(unit.DefaultRoles.Select(r => (r, branch?.Company ?? Domain.CompanyScope.All, branch?.Code)));
        }

        return result.ToDictionary(r => r.Key, r => r.Value.Distinct().ToList());
    }
}
