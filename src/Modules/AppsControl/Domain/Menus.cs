namespace DynamicCube.Modules.AppsControl.Domain;

public enum MenuItemType
{
    Group,
    Screen,
    Service,
    Report,
    Dashboard,
    Queue,
    Url,
}

/// <summary>
/// A top-level folder of a menu tree. A tree is a channel's (<see cref="AppId"/>: the platform's screens, where Dynamic
/// Cube apps may add their own items) or a connected app's (its <see cref="ConfigEntity.BusinessApp"/>, no channel).
/// </summary>
/// <summary>
/// Menu groups and items are keyed like the legacy Apps Control by their app and code, written <c>appsid/code</c>: the
/// app is the channel of the tree (e.g. <c>INTERNAL</c>) or, for a connected app's own tree, that app.
/// </summary>
public static class MenuKey
{
    public static string Format(string? app, string code) => string.IsNullOrEmpty(app) ? code : $"{app}/{code}";
}

/// <summary>A row of a menu tree (group or item): its channel app, if any.</summary>
public interface IMenuTreeRow
{
    Guid? AppId { get; }

    App? App { get; }
}

public sealed class MenuGroup : ConfigEntity, IMenuTreeRow
{
    /// <summary>The group's key, <c>appsid/code</c> (needs <see cref="App"/> loaded).</summary>
    public string Key => MenuKey.Format(App?.Code ?? BusinessApp, Code);

    /// <summary>The channel; null for a connected app's menus.</summary>
    public Guid? AppId { get; set; }

    public App? App { get; set; }

    public int DisplayOrder { get; set; }

    public string? Icon { get; set; }
}

/// <summary>Menu item. Shown to users holding <c>VIEW</c> on <see cref="Resource"/> (items without a resource are visible to all).</summary>
public sealed class Menu : ConfigEntity, IMenuTreeRow
{
    /// <summary>The item's key, <c>appsid/code</c> (needs <see cref="App"/> loaded).</summary>
    public string Key => MenuKey.Format(App?.Code ?? BusinessApp, Code);

    /// <summary>The channel; null for a connected app's menus.</summary>
    public Guid? AppId { get; set; }

    public App? App { get; set; }

    public Guid? MenuGroupId { get; set; }

    public MenuGroup? MenuGroup { get; set; }

    public Guid? ParentId { get; set; }

    public Menu? Parent { get; set; }

    public MenuItemType ItemType { get; set; } = MenuItemType.Screen;

    /// <summary>Route, service code, report code, queue code or URL depending on <see cref="ItemType"/>.</summary>
    public string? Target { get; set; }

    public Guid? ResourceId { get; set; }

    public Resource? Resource { get; set; }

    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    public string? Icon { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsVisible { get; set; } = true;
}
