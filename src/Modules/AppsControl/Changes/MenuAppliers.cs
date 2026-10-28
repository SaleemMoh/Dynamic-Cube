using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Changes;

public sealed class MenuGroupPayload : ConfigPayload
{
    public string AppCode { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public string? Icon { get; set; }
}

internal sealed class MenuGroupApplier : ConfigApplier<MenuGroup, MenuGroupPayload>
{
    public override string EntityType => AppsControlChangeTypes.MenuGroup;

    public override string Area => AppsControlAreas.Menus;

    public override string ResourceCode => AppsControlResources.Menus;

    protected override IQueryable<MenuGroup> Query(AppsControlDbContext context) => context.MenuGroups.Include(g => g.App);

    protected override IQueryable<MenuGroup> ListQuery(AppsControlDbContext context, string? parent) =>
        (parent is null ? Query(context) : Query(context).Where(g => g.App!.Code == parent)).OrderBy(g => g.App!.Code).ThenBy(g => g.DisplayOrder);

    protected override async Task ApplyAsync(MenuGroup entity, MenuGroupPayload payload, RefResolver refs, ValidationErrors errors, MenuGroup? existing, CancellationToken cancellationToken)
    {
        errors.Required("appCode", payload.AppCode);
        entity.AppId = await refs.AppAsync(payload.AppCode, "appCode", errors, cancellationToken) ?? Guid.Empty;
        entity.DisplayOrder = payload.DisplayOrder;
        entity.Icon = payload.Icon;
        errors.MaxLength("icon", payload.Icon, 100);
    }

    protected override Task<MenuGroupPayload> ToPayloadAsync(AppsControlDbContext context, MenuGroup entity, CancellationToken cancellationToken) =>
        Task.FromResult(new MenuGroupPayload { AppCode = entity.App?.Code ?? string.Empty, DisplayOrder = entity.DisplayOrder, Icon = entity.Icon });
}

public sealed class MenuPayload : ConfigPayload
{
    public string AppCode { get; set; } = string.Empty;

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

internal sealed class MenuApplier : ConfigApplier<Menu, MenuPayload>
{
    public override string EntityType => AppsControlChangeTypes.Menu;

    public override string Area => AppsControlAreas.Menus;

    public override string ResourceCode => AppsControlResources.Menus;

    protected override IQueryable<Menu> Query(AppsControlDbContext context) =>
        context.Menus.Include(m => m.App).Include(m => m.MenuGroup).Include(m => m.Parent).Include(m => m.Resource);

    protected override IQueryable<Menu> ListQuery(AppsControlDbContext context, string? parent) =>
        (parent is null ? Query(context) : Query(context).Where(m => m.App!.Code == parent)).OrderBy(m => m.App!.Code).ThenBy(m => m.DisplayOrder);

    protected override async Task ApplyAsync(Menu entity, MenuPayload payload, RefResolver refs, ValidationErrors errors, Menu? existing, CancellationToken cancellationToken)
    {
        errors.Required("appCode", payload.AppCode);
        entity.AppId = await refs.AppAsync(payload.AppCode, "appCode", errors, cancellationToken) ?? Guid.Empty;
        entity.MenuGroupId = await refs.MenuGroupAsync(payload.GroupCode, "groupCode", errors, cancellationToken);
        if (payload.ParentCode == entity.Code && entity.Code.Length > 0)
        {
            errors.Add("parentCode", "A menu item cannot be its own parent.");
        }
        else
        {
            entity.ParentId = await refs.MenuAsync(payload.ParentCode, "parentCode", errors, cancellationToken);
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
            AppCode = entity.App?.Code ?? string.Empty,
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
