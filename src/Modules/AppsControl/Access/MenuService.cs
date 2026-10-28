using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Access;

public sealed record MenuNode(
    string Code,
    string NameAr,
    string NameEn,
    MenuItemType ItemType,
    string? Target,
    string? Icon,
    string? DescriptionAr,
    string? DescriptionEn,
    IReadOnlyList<MenuNode> Children);

public sealed record MenuGroupNode(string Code, string NameAr, string NameEn, string? Icon, IReadOnlyList<MenuNode> Items);

public sealed record AppInfo(string Code, string NameAr, string NameEn, AppType AppType, string? BaseUrl);

/// <summary>Builds a user's menu: active, visible items whose resource the user may <c>VIEW</c> (or that have no resource).</summary>
public sealed class MenuService(AppsControlDbContext context)
{
    public async Task<IReadOnlyList<MenuGroupNode>> GetMenuAsync(string appCode, AccessSnapshot access, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(access);

        var groups = await context.MenuGroups.AsNoTracking()
            .Where(g => g.App!.Code == appCode && g.IsActive)
            .OrderBy(g => g.DisplayOrder).ThenBy(g => g.Code)
            .ToListAsync(cancellationToken);
        var items = await context.Menus.AsNoTracking()
            .Where(m => m.App!.Code == appCode && m.IsActive && m.IsVisible)
            .OrderBy(m => m.DisplayOrder).ThenBy(m => m.Code)
            .Select(m => new { Menu = m, ResourceCode = m.Resource != null ? m.Resource.Code : null })
            .ToListAsync(cancellationToken);

        var visible = items.Where(i => i.ResourceCode is null || access.Can(i.ResourceCode, PrivilegeActions.View)).Select(i => i.Menu).ToList();

        IReadOnlyList<MenuNode> Build(Guid? parentId, Guid? groupId) =>
            visible
                .Where(m => m.ParentId == parentId && (parentId != null || m.MenuGroupId == groupId))
                .Select(m => new MenuNode(m.Code, m.NameAr, m.NameEn, m.ItemType, m.Target, m.Icon, m.DescriptionAr, m.DescriptionEn, Build(m.Id, null)))
                .Where(n => n.ItemType != MenuItemType.Group || n.Children.Count > 0)
                .ToList();

        var result = groups
            .Select(g => new MenuGroupNode(g.Code, g.NameAr, g.NameEn, g.Icon, Build(null, g.Id)))
            .Where(g => g.Items.Count > 0)
            .ToList();

        var ungrouped = Build(null, null);
        if (ungrouped.Count > 0)
        {
            result.Insert(0, new MenuGroupNode(string.Empty, string.Empty, string.Empty, null, ungrouped));
        }

        return result;
    }

    /// <summary>Apps the user can open: apps where the user holds VIEW on at least one resource, plus the current app.</summary>
    public async Task<IReadOnlyList<AppInfo>> GetAppsAsync(AccessSnapshot access, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(access);

        var resourceCodes = access.Privileges.Where(p => p.Value.Contains(PrivilegeActions.View)).Select(p => p.Key).ToList();
        var appIds = await context.Resources.AsNoTracking()
            .Where(r => resourceCodes.Contains(r.Code) && r.AppId != null)
            .Select(r => r.AppId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return await context.Apps.AsNoTracking()
            .Where(a => a.IsActive && (appIds.Contains(a.Id) || a.Code == access.User.App))
            .OrderBy(a => a.DisplayOrder)
            .Select(a => new AppInfo(a.Code, a.NameAr, a.NameEn, a.AppType, a.BaseUrl))
            .ToListAsync(cancellationToken);
    }
}
