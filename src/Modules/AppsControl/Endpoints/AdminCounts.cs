using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Endpoints;

/// <summary>
/// How many rows hang under each row of a master list, keyed like the list's rows: a role by its company
/// (<c>instid/cmp_num/CODE</c>, so roles of the same code in two companies count apart), a menu group by its app
/// (<c>appsid/CODE</c>).
/// </summary>
public static class AdminCounts
{
    public static async Task<Dictionary<string, int>> UsersPerRoleAsync(AppsControlDbContext db, CancellationToken ct) =>
        (await db.UserRoles
            .GroupBy(r => new { r.Role!.EntityCode, r.Role.CompanyCode, r.Role.Code })
            .Select(g => new { g.Key.EntityCode, g.Key.CompanyCode, g.Key.Code, Users = g.Select(r => r.UserId).Distinct().Count() })
            .ToListAsync(ct))
        .ToDictionary(g => RoleKey.Format(g.EntityCode, g.CompanyCode, g.Code), g => g.Users, StringComparer.Ordinal);

    public static async Task<Dictionary<string, int>> MenusPerGroupAsync(AppsControlDbContext db, CancellationToken ct) =>
        (await db.Menus.Where(m => m.MenuGroup != null)
            .GroupBy(m => new { App = m.MenuGroup!.App != null ? m.MenuGroup.App.Code : m.MenuGroup.BusinessApp, m.MenuGroup.Code })
            .Select(g => new { g.Key.App, g.Key.Code, Menus = g.Count() })
            .ToListAsync(ct))
        .ToDictionary(g => MenuKey.Format(g.App, g.Code), g => g.Menus, StringComparer.Ordinal);
}
