using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.Configuration;
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

        // A channel's menu: the platform's items and those of the apps the user works in.
        return await BuildAsync(
            context.MenuGroups.AsNoTracking().Where(g => g.App!.Code == appCode && g.IsActive),
            context.Menus.AsNoTracking().Where(m => m.App!.Code == appCode && m.IsActive && m.IsVisible),
            (row, resourceCode) => (resourceCode is null || access.Can(resourceCode, PrivilegeActions.View)) && access.Scope.CanSee(null, row.BusinessApp, row.SharedApps),
            cancellationToken);
    }

    /// <summary>
    /// A connected app's own tree (ADR-0036 step 7): with <paramref name="access"/>, the items the user may open (their
    /// screen's <c>VIEW</c>, or no screen); without, every active item, for the app itself.
    /// </summary>
    public async Task<IReadOnlyList<MenuGroupNode>> GetConnectedMenuAsync(string appCode, AccessSnapshot? access, CancellationToken cancellationToken) =>
        await BuildAsync(
            context.MenuGroups.AsNoTracking().Where(g => g.AppId == null && g.BusinessApp == appCode && g.IsActive),
            context.Menus.AsNoTracking().Where(m => m.AppId == null && m.BusinessApp == appCode && m.IsActive && (access == null || m.IsVisible)),
            (_, resourceCode) => access is null || resourceCode is null || access.Can(resourceCode, PrivilegeActions.View),
            cancellationToken);

    private static async Task<IReadOnlyList<MenuGroupNode>> BuildAsync(
        IQueryable<MenuGroup> groupQuery, IQueryable<Menu> itemQuery, Func<ConfigEntity, string?, bool> visible, CancellationToken cancellationToken)
    {
        var groups = (await groupQuery
            .OrderBy(g => g.DisplayOrder).ThenBy(g => g.Code)
            .ToListAsync(cancellationToken))
            .Where(g => visible(g, null))
            .ToList();
        var items = await itemQuery
            .OrderBy(m => m.DisplayOrder).ThenBy(m => m.Code)
            .Select(m => new { Menu = m, ResourceCode = m.Resource != null ? m.Resource.Code : null })
            .ToListAsync(cancellationToken);

        var shown = items
            .Where(i => visible(i.Menu, i.ResourceCode))
            .Select(i => i.Menu)
            .ToList();

        IReadOnlyList<MenuNode> Build(Guid? parentId, Guid? groupId) =>
            shown
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
