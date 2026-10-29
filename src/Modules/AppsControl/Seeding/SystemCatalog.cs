using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Platform.Security;

namespace DynamicCube.Modules.AppsControl.Seeding;

/// <summary>Product-owned (SYSTEM) Apps Control rows. Product upgrades insert and update these; entities cannot change them.</summary>
internal static class SystemCatalog
{
    public const string SystemAdminRole = BuiltInRoles.SystemAdmin;
    public const string ConfigMakerRole = BuiltInRoles.ConfigMaker;
    public const string ConfigCheckerRole = BuiltInRoles.ConfigChecker;
    public const string AppsControlMenuGroup = "APPS_CONTROL";
    public const string HomeResource = "internal.home";

    public sealed record AppDef(string Code, string NameAr, string NameEn, AppType Type, int Order);

    public sealed record ClassDef(string Code, string NameAr, string NameEn, (string Code, string NameAr, string NameEn)[] Values);

    private static readonly string[] EditActions = [.. CatalogActions.Edits];

    public static IReadOnlyList<AppDef> Apps { get; } =
    [
        new("INTERNAL", "النظام الداخلي", "Internal system", AppType.Internal, 1),
        new("EXTERNAL", "بوابة الخدمات الإلكترونية", "E-services portal", AppType.External, 2),
        new("FIELD", "تطبيق الزيارات الميدانية", "Field app", AppType.Field, 3),
    ];

    /// <summary>Only the administrator: makers and checkers get no privilege.</summary>
    private static readonly Dictionary<string, string[]> NoRole = new() { [ConfigMakerRole] = [], [ConfigCheckerRole] = [] };

    private static readonly CatalogResource[] Resources =
    [
        new(HomeResource, "الرئيسية", "Home", "INTERNAL", null, [PrivilegeActions.View]),
        new(AppsControlResources.Root, "التحكم بالتطبيقات", "Apps Control", "INTERNAL", null, [PrivilegeActions.View]),
        new(AppsControlResources.Installation, "إعداد النظام والجهة", "Installation & entity", "INTERNAL", AppsControlResources.Root, EditActions),
        new(AppsControlResources.Organization, "الهيكل التنظيمي", "Organization structure", "INTERNAL", AppsControlResources.Root, EditActions),
        new(AppsControlResources.Users, "المستخدمون", "Users", "INTERNAL", AppsControlResources.Root, EditActions),
        new(AppsControlResources.PortalAccounts, "حسابات البوابة", "Portal accounts", "INTERNAL", AppsControlResources.Root,
            [PrivilegeActions.View, PrivilegeActions.Create, PrivilegeActions.Edit]),
        new(AppsControlResources.Roles, "الأدوار والصلاحيات", "Roles & privileges", "INTERNAL", AppsControlResources.Root, EditActions),
        new(AppsControlResources.Resources, "الموارد", "Resources", "INTERNAL", AppsControlResources.Root, EditActions),
        new(AppsControlResources.Menus, "القوائم", "Menus", "INTERNAL", AppsControlResources.Root, EditActions),
        new(AppsControlResources.Classes, "التصنيفات والقيم", "Classes & domains", "INTERNAL", AppsControlResources.Root, EditActions),
        new(AppsControlResources.Lovs, "قوائم القيم", "Lists of values", "INTERNAL", AppsControlResources.Root, EditActions),
        new(AppsControlResources.LovSql, "استعلامات قوائم القيم", "LOV restricted SQL", "INTERNAL", AppsControlResources.Lovs, [PrivilegeActions.Edit], CatalogResourceTypes.Api, NoRole),
        new(AppsControlResources.Settings, "الإعدادات", "Settings", "INTERNAL", AppsControlResources.Root, EditActions),
        new(AppsControlResources.UiTexts, "نصوص الواجهة", "UI texts", "INTERNAL", AppsControlResources.Root, EditActions),
        new(AppsControlResources.ChangeSets, "طلبات التغيير", "Change sets", "INTERNAL", AppsControlResources.Root, [PrivilegeActions.View, PrivilegeActions.Create, PrivilegeActions.Approve],
            RoleActions: new Dictionary<string, string[]>
            {
                [ConfigMakerRole] = [PrivilegeActions.View, PrivilegeActions.Create],
                [ConfigCheckerRole] = [PrivilegeActions.View, PrivilegeActions.Approve],
            }),
        new(AppsControlResources.ApprovalPolicy, "سياسة الاعتماد", "Approval policy", "INTERNAL", AppsControlResources.Root, [PrivilegeActions.View, PrivilegeActions.Edit]),
    ];

    /// <summary>
    /// Menus the product no longer offers; the seeder hides them where earlier versions created them. Settings only held
    /// the portal home page, which is edited under Portal content.
    /// </summary>
    public static readonly IReadOnlyList<string> RetiredMenus = ["apps-control.settings"];

    private static readonly CatalogMenu[] Menus =
    [
        new("apps-control.change-sets", "طلبات التغيير", "Change sets", "/apps-control/change-sets", AppsControlResources.ChangeSets, "fact_check", 10),
        new("apps-control.classes", "التصنيفات والقيم", "Classes & domains", "/apps-control/classes", AppsControlResources.Classes, "category", 20),
        new("apps-control.lovs", "قوائم القيم", "Lists of values", "/apps-control/lovs", AppsControlResources.Lovs, "filter_list", 30),
        new("apps-control.organization", "الهيكل التنظيمي", "Organization structure", "/apps-control/organization", AppsControlResources.Organization, "account_tree", 40),
        new("apps-control.users", "المستخدمون", "Users", "/apps-control/users", AppsControlResources.Users, "group", 50),
        new("apps-control.portal-accounts", "حسابات البوابة", "Portal accounts", "/apps-control/portal-accounts", AppsControlResources.PortalAccounts, "badge", 55),
        new("apps-control.roles", "الأدوار والصلاحيات", "Roles & privileges", "/apps-control/roles", AppsControlResources.Roles, "admin_panel_settings", 60),
        new("apps-control.menus", "القوائم", "Menus", "/apps-control/menus", AppsControlResources.Menus, "menu", 70),
        new("apps-control.resources", "الموارد", "Resources", "/apps-control/resources", AppsControlResources.Resources, "lock", 80),
        new("apps-control.ui-texts", "نصوص الواجهة", "UI texts", "/apps-control/ui-texts", AppsControlResources.UiTexts, "translate", 100),
        new("apps-control.installation", "النظام والجهة", "Installation & entity", "/apps-control/installation", AppsControlResources.Installation, "domain", 110),
        new("apps-control.branding", "الهوية والشعار", "Branding", "/apps-control/branding", AppsControlResources.Installation, "palette", 115),
        new("apps-control.approval-policy", "سياسة الاعتماد", "Approval policy", "/apps-control/approval-policy", AppsControlResources.ApprovalPolicy, "rule", 120),
    ];

    /// <summary>Apps Control's own resources and menus, seeded like every other module's contribution.</summary>
    public static SystemCatalogContribution Contribution { get; } = new()
    {
        Module = "apps-control",
        Resources = Resources,
        MenuGroup = new CatalogMenuGroup(AppsControlMenuGroup, "التحكم بالتطبيقات", "Apps Control", "INTERNAL", "tune", 900),
        Menus = Menus,
    };

    public static IReadOnlyList<ClassDef> Classes { get; } =
    [
        new(EntityApplier.EntityKindClass, "نوع الجهة", "Entity kind",
        [
            ("MINISTRY", "وزارة", "Ministry"),
            ("AUTHORITY", "هيئة", "Authority"),
            ("COMPANY", "شركة", "Company"),
            ("OTHER", "أخرى", "Other"),
        ]),
    ];

    public static IReadOnlyList<(string Code, string NameAr, string NameEn)> Positions { get; } =
    [
        ("MANAGER", "مدير", "Manager"),
    ];

    /// <summary>Built-in roles and their default actions on a resource; <see cref="CatalogResource.RoleActions"/> overrides them.</summary>
    public static IReadOnlyDictionary<string, (string NameAr, string NameEn, Func<CatalogResource, IReadOnlyList<string>> Actions)> Roles { get; } =
        new Dictionary<string, (string, string, Func<CatalogResource, IReadOnlyList<string>>)>
        {
            [SystemAdminRole] = ("مدير النظام", "System administrator", r => r.Actions),
            [ConfigMakerRole] = ("مُعد الإعدادات", "Configuration maker", r => r.Actions),
            [ConfigCheckerRole] = ("مُعتمد الإعدادات", "Configuration checker", r => r.Actions.Contains(PrivilegeActions.View) ? [PrivilegeActions.View] : []),
        };

    /// <summary>Actions of a built-in role on a resource; empty means no privilege.</summary>
    public static IReadOnlyList<string> ActionsOf(string role, CatalogResource resource) =>
        resource.RoleActions is not null && resource.RoleActions.TryGetValue(role, out var actions) ? actions : Roles[role].Actions(resource);
}
