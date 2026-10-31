using DynamicCube.Modules.AppsControl.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Access;

/// <summary>Roles people receive from the units they are placed in (an org unit's default roles, ADR-0036).</summary>
internal static class DepartmentRoles
{
    /// <summary>Codes of the default roles of the active units the user is placed in today.</summary>
    public static async Task<List<string>> OfUserAsync(AppsControlDbContext context, Guid userId, DateOnly today, CancellationToken cancellationToken) =>
        (await OfUsersAsync(context, [userId], today, cancellationToken)).GetValueOrDefault(userId) ?? [];

    /// <summary>
    /// The default roles of the units the user is placed in today, each with the company it counts in: the company of the
    /// branch the unit serves (the branch linked to the unit or to the nearest unit above it), or every company.
    /// </summary>
    public static async Task<List<(string Role, string Company)>> OfUserByCompanyAsync(AppsControlDbContext context, Guid userId, DateOnly today, CancellationToken cancellationToken)
    {
        var units = await context.UserAssignments.AsNoTracking()
            .Where(a => a.UserId == userId && a.OrgUnit!.IsActive && a.OrgUnit.DefaultRoles.Count > 0
                && (a.EffectiveFrom == null || a.EffectiveFrom <= today) && (a.EffectiveTo == null || a.EffectiveTo >= today))
            .Select(a => new { a.OrgUnit!.Path, a.OrgUnit.DefaultRoles })
            .ToListAsync(cancellationToken);
        if (units.Count == 0)
        {
            return [];
        }

        var branches = (await context.Branches.AsNoTracking()
            .Where(b => b.IsActive && b.OrgUnitCode != null)
            .Join(context.OrgUnits.AsNoTracking(), b => b.OrgUnitCode, u => u.Code, (b, u) => new { b.CompanyCode, u.Path })
            .ToListAsync(cancellationToken))
            .Select(b => (b.CompanyCode, Path: b.Path.ToString()))
            .ToList();
        var result = new List<(string Role, string Company)>();
        foreach (var unit in units)
        {
            var path = unit.Path.ToString();
            var company = branches
                .Where(b => path == b.Path || path.StartsWith(b.Path + ".", StringComparison.Ordinal))
                .OrderByDescending(b => b.Path.Length)
                .Select(b => b.CompanyCode)
                .FirstOrDefault() ?? Domain.CompanyScope.All;
            result.AddRange(unit.DefaultRoles.Select(r => (r, company)));
        }

        return result.Distinct().ToList();
    }

    /// <summary>The department roles of several users, by user.</summary>
    public static async Task<Dictionary<Guid, List<string>>> OfUsersAsync(AppsControlDbContext context, IReadOnlyCollection<Guid> userIds, DateOnly today, CancellationToken cancellationToken)
    {
        var rows = await context.UserAssignments.AsNoTracking()
            .Where(a => userIds.Contains(a.UserId) && a.OrgUnit!.IsActive && a.OrgUnit.DefaultRoles.Count > 0
                && (a.EffectiveFrom == null || a.EffectiveFrom <= today) && (a.EffectiveTo == null || a.EffectiveTo >= today))
            .Select(a => new { a.UserId, a.OrgUnit!.DefaultRoles })
            .ToListAsync(cancellationToken);
        return rows.GroupBy(r => r.UserId).ToDictionary(g => g.Key, g => g.SelectMany(r => r.DefaultRoles).Distinct(StringComparer.Ordinal).ToList());
    }
}
