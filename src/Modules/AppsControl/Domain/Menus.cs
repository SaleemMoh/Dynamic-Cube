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

public sealed class MenuGroup : ConfigEntity
{
    public Guid AppId { get; set; }

    public App? App { get; set; }

    public int DisplayOrder { get; set; }

    public string? Icon { get; set; }
}

/// <summary>Menu item. Shown to users holding <c>VIEW</c> on <see cref="Resource"/> (items without a resource are visible to all).</summary>
public sealed class Menu : ConfigEntity
{
    public Guid AppId { get; set; }

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
