using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Changes;

/// <summary>
/// The menu tree a row belongs to (ADR-0036): a channel's (the platform's screens; a Dynamic Cube app may add items on
/// the channels it ticks) or a connected app's own tree, which has no channel.
/// </summary>
internal static class MenuTrees
{
    /// <summary>Checks the channel against the row's app and returns the channel id (null for a connected app's tree).</summary>
    public static async Task<Guid?> ChannelAsync(ConfigEntity entity, string? appCode, RefResolver refs, ValidationErrors errors, CancellationToken cancellationToken)
    {
        var channel = string.IsNullOrWhiteSpace(appCode) ? null : appCode.Trim();
        var app = entity.BusinessApp is null
            ? null
            : await refs.Context.BusinessApps.AsNoTracking().Where(a => a.Code == entity.BusinessApp).Select(a => new { a.Kind, a.Channels }).SingleOrDefaultAsync(cancellationToken);
        if (app?.Kind == AppKind.Connected)
        {
            if (channel is not null)
            {
                errors.Add("appCode", "A connected app has its own menu tree; leave the channel empty.");
            }

            return null;
        }

        errors.Required("appCode", channel);
        if (channel is not null && app is not null && !app.Channels.Contains(channel))
        {
            errors.Add("appCode", $"App {entity.BusinessApp} does not appear on channel {channel}; tick it in the app first.");
        }

        return await refs.AppAsync(channel, "appCode", errors, cancellationToken);
    }

    /// <summary>Whether two rows are in the same tree: the same channel, and for a connected app the same app.</summary>
    public static bool SameTree(Guid? channel, string? app, Guid? otherChannel, string? otherApp) =>
        channel is null ? otherChannel is null && otherApp == app : otherChannel == channel;
}

public sealed class MenuGroupPayload : ConfigPayload
{
    /// <summary>The channel; empty for a connected app's tree.</summary>
    public string? AppCode { get; set; }

    public int DisplayOrder { get; set; }

    public string? Icon { get; set; }
}

internal sealed class MenuGroupApplier(AppsControlDbContext db) : ConfigApplier<MenuGroup, MenuGroupPayload>(db)
{
    public override string EntityType => AppsControlChangeTypes.MenuGroup;

    public override string Area => AppsControlAreas.Menus;

    public override string ResourceCode => AppsControlResources.Menus;

    protected override IQueryable<MenuGroup> Query(AppsControlDbContext context) => context.MenuGroups.Include(g => g.App);

    /// <summary>A tree's groups: <paramref name="parent"/> is a channel code or a connected app's code.</summary>
    protected override IQueryable<MenuGroup> ListQuery(AppsControlDbContext context, string? parent) =>
        (parent is null ? Query(context) : Query(context).Where(g => g.App!.Code == parent || (g.AppId == null && g.BusinessApp == parent)))
            .OrderBy(g => g.App!.Code).ThenBy(g => g.DisplayOrder);

    protected override async Task ApplyAsync(MenuGroup entity, MenuGroupPayload payload, RefResolver refs, ValidationErrors errors, MenuGroup? existing, CancellationToken cancellationToken)
    {
        entity.AppId = await MenuTrees.ChannelAsync(entity, payload.AppCode, refs, errors, cancellationToken);
        entity.DisplayOrder = payload.DisplayOrder;
        entity.Icon = payload.Icon;
        errors.MaxLength("icon", payload.Icon, 100);
    }

    protected override Task<MenuGroupPayload> ToPayloadAsync(AppsControlDbContext context, MenuGroup entity, CancellationToken cancellationToken) =>
        Task.FromResult(new MenuGroupPayload { AppCode = entity.App?.Code, DisplayOrder = entity.DisplayOrder, Icon = entity.Icon });
}

public sealed class MenuPayload : ConfigPayload
{
    /// <summary>The channel; empty for a connected app's tree.</summary>
    public string? AppCode { get; set; }

    public string? GroupCode { get; set; }

    public string? ParentCode { get; set; }

    public MenuItemType ItemType { get; set; } = MenuItemType.Screen;

    public string? Target { get; set; }

    public string? ResourceCode { get; set; }

    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    public string? Icon { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsVisible { get; set; } = true;
}

internal sealed class MenuApplier(AppsControlDbContext db) : ConfigApplier<Menu, MenuPayload>(db)
{
    public override string EntityType => AppsControlChangeTypes.Menu;

    public override string Area => AppsControlAreas.Menus;

    public override string ResourceCode => AppsControlResources.Menus;

    protected override IQueryable<Menu> Query(AppsControlDbContext context) =>
        context.Menus.Include(m => m.App).Include(m => m.MenuGroup).Include(m => m.Parent).Include(m => m.Resource);

    /// <summary>A tree's items: <paramref name="parent"/> is a channel code or a connected app's code.</summary>
    protected override IQueryable<Menu> ListQuery(AppsControlDbContext context, string? parent) =>
        (parent is null ? Query(context) : Query(context).Where(m => m.App!.Code == parent || (m.AppId == null && m.BusinessApp == parent)))
            .OrderBy(m => m.App!.Code).ThenBy(m => m.DisplayOrder);

    protected override Guid? ParentOf(Menu entity) => entity.ParentId;

    protected override async Task ApplyAsync(Menu entity, MenuPayload payload, RefResolver refs, ValidationErrors errors, Menu? existing, CancellationToken cancellationToken)
    {
        entity.AppId = await MenuTrees.ChannelAsync(entity, payload.AppCode, refs, errors, cancellationToken);
        entity.MenuGroupId = await refs.MenuGroupAsync(payload.GroupCode, "groupCode", errors, cancellationToken);
        if (entity.MenuGroupId is { } groupId && groupId != Guid.Empty
            && await refs.Context.MenuGroups.AsNoTracking().Where(g => g.Id == groupId).Select(g => new { g.AppId, g.BusinessApp }).SingleAsync(cancellationToken) is var group
            && !errors.HasErrors && !MenuTrees.SameTree(entity.AppId, entity.BusinessApp, group.AppId, group.BusinessApp))
        {
            errors.Add("groupCode", $"Group {payload.GroupCode} is in another menu tree.");
        }

        entity.ParentId = await refs.MenuAsync(payload.ParentCode, "parentCode", errors, cancellationToken);
        if (entity.ParentId is { } parentId && parentId != Guid.Empty)
        {
            // The parent is in the same tree and is not the item itself or one of its descendants.
            var chain = new List<string>();
            Guid? cursor = parentId;
            while (cursor is { } id && chain.Count < 50)
            {
                var node = await refs.Context.Menus.AsNoTracking().Where(m => m.Id == id).Select(m => new { m.Code, m.ParentId, m.AppId, m.BusinessApp }).SingleAsync(cancellationToken);
                if (chain.Count == 0 && !errors.HasErrors && !MenuTrees.SameTree(entity.AppId, entity.BusinessApp, node.AppId, node.BusinessApp))
                {
                    errors.Add("parentCode", $"Item {node.Code} is in another menu tree.");
                }

                chain.Add(node.Code);
                cursor = node.ParentId;
            }

            if (chain.Contains(entity.Code))
            {
                errors.Add("parentCode", "A menu item cannot be under itself or one of its own items.");
            }
        }
        else if (payload.ParentCode == entity.Code && entity.Code.Length > 0)
        {
            errors.Add("parentCode", "A menu item cannot be its own parent.");
        }

        entity.ResourceId = await refs.ResourceAsync(payload.ResourceCode, "resourceCode", errors, cancellationToken);
        if (payload.ItemType != MenuItemType.Group)
        {
            errors.Required("target", payload.Target);
        }

        if (payload.ItemType == MenuItemType.Url && payload.Target is { } url && !Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            errors.Add("target", "Use an absolute URL.");
        }

        errors.MaxLength("target", payload.Target, 1000);
        errors.MaxLength("icon", payload.Icon, 4000);
        entity.ItemType = payload.ItemType;
        entity.Target = payload.Target;
        entity.DescriptionAr = payload.DescriptionAr;
        entity.DescriptionEn = payload.DescriptionEn;
        entity.Icon = payload.Icon;
        entity.DisplayOrder = payload.DisplayOrder;
        entity.IsVisible = payload.IsVisible;
    }

    protected override Task<MenuPayload> ToPayloadAsync(AppsControlDbContext context, Menu entity, CancellationToken cancellationToken) =>
        Task.FromResult(new MenuPayload
        {
            AppCode = entity.App?.Code,
            GroupCode = entity.MenuGroup?.Code,
            ParentCode = entity.Parent?.Code,
            ItemType = entity.ItemType,
            Target = entity.Target,
            ResourceCode = entity.Resource?.Code,
            DescriptionAr = entity.DescriptionAr,
            DescriptionEn = entity.DescriptionEn,
            Icon = entity.Icon,
            DisplayOrder = entity.DisplayOrder,
            IsVisible = entity.IsVisible,
        });
}
