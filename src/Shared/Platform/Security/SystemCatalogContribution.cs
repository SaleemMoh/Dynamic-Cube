namespace DynamicCube.Platform.Security;

/// <summary>
/// Product-owned (SYSTEM) security and navigation rows a module contributes to Apps Control: its resources, its menu
/// group and menus, and the privileges of the built-in roles. The Apps Control seeder inserts and refreshes them.
/// Register with <c>services.AddSystemCatalog(...)</c>.
/// </summary>
public sealed class SystemCatalogContribution
{
    public required string Module { get; init; }

    public IReadOnlyList<CatalogResource> Resources { get; init; } = [];

    public CatalogMenuGroup? MenuGroup { get; init; }

    public IReadOnlyList<CatalogMenu> Menus { get; init; } = [];
}

/// <summary>A securable resource. <paramref name="Parent"/> may name a resource of another module.</summary>
/// <param name="RoleActions">
/// Actions of built-in roles that differ from the defaults (administrator: all; configuration maker: all;
/// configuration checker: view). An empty array means the role gets no privilege on the resource.
/// </param>
public sealed record CatalogResource(
    string Code,
    string NameAr,
    string NameEn,
    string App,
    string? Parent,
    IReadOnlyList<string> Actions,
    string Type = CatalogResourceTypes.Screen,
    IReadOnlyDictionary<string, string[]>? RoleActions = null);

public sealed record CatalogMenuGroup(string Code, string NameAr, string NameEn, string App, string Icon, int Order);

public sealed record CatalogMenu(string Code, string NameAr, string NameEn, string Target, string Resource, string Icon, int Order);

/// <summary>Resource types (the names of Apps Control's resource type values).</summary>
public static class CatalogResourceTypes
{
    public const string Screen = "SCREEN";
    public const string Api = "API";
    public const string Report = "REPORT";
    public const string Service = "SERVICE";
}

/// <summary>Built-in role codes and privilege actions shared by every module.</summary>
public static class BuiltInRoles
{
    public const string SystemAdmin = "SYSTEM_ADMIN";
    public const string ConfigMaker = "CONFIG_MAKER";
    public const string ConfigChecker = "CONFIG_CHECKER";
}

public static class CatalogActions
{
    public const string View = "VIEW";
    public const string Create = "CREATE";
    public const string Edit = "EDIT";
    public const string Delete = "DELETE";
    public const string Approve = "APPROVE";
    public const string Execute = "EXECUTE";

    public static IReadOnlyList<string> Edits { get; } = [View, Create, Edit, Delete];
}
