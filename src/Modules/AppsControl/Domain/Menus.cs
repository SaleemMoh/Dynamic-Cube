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
public sealed class MenuGroup : ConfigEntity
{
    /// <summary>The channel; null for a connected app's menus.</summary>
    public Guid? AppId { get; set; }

    public App? App { get; set; }

    public int DisplayOrder { get; set; }

    public string? Icon { get; set; }
}

/// <summary>Menu item. Shown to users holding <c>VIEW</c> on <see cref="Resource"/> (items without a resource are visible to all).</summary>
public sealed class Menu : ConfigEntity
{
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
