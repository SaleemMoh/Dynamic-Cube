using DynamicCube.Modules.AppsControl.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Access;

/// <summary>Roles people receive from the units they are placed in (an org unit's default roles, ADR-0036).</summary>
internal static class DepartmentRoles
{
    /// <summary>Codes of the default roles of the active units the user is placed in today.</summary>
    public static async Task<List<string>> OfUserAsync(AppsControlDbContext context, Guid userId, DateOnly today, CancellationToken cancellationToken) =>
        (await OfUsersAsync(context, [userId], today, cancellationToken)).GetValueOrDefault(userId) ?? [];

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
