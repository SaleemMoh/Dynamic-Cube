using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Domain;

namespace DynamicCube.Modules.AppsControl.Seeding;

/// <summary>Product-owned (SYSTEM) Apps Control rows. Product upgrades insert and update these; entities cannot change them.</summary>
internal static class SystemCatalog
{
    public const string SystemAdminRole = "SYSTEM_ADMIN";
    public const string ConfigMakerRole = "CONFIG_MAKER";
    public const string ConfigCheckerRole = "CONFIG_CHECKER";
    public const string AppsControlMenuGroup = "APPS_CONTROL";
    public const string HomeResource = "internal.home";

    public sealed record AppDef(string Code, string NameAr, string NameEn, AppType Type, int Order);

    public sealed record ResourceDef(string Code, string NameAr, string NameEn, string App, string? Parent, string[] Actions, ResourceType Type = ResourceType.Screen);

    public sealed record MenuDef(string Code, string NameAr, string NameEn, string Target, string Resource, string Icon, int Order);

    public sealed record ClassDef(string Code, string NameAr, string NameEn, (string Code, string NameAr, string NameEn)[] Values);

    private static readonly string[] EditActions = [PrivilegeActions.View, PrivilegeActions.Create, PrivilegeActions.Edit, PrivilegeActions.Delete];

    public static IReadOnlyList<AppDef> Apps { get; } =
    [
        new("INTERNAL", "النظام الداخلي", "Internal system", AppType.Internal, 1),
        new("EXTERNAL", "بوابة الخدمات الإلكترونية", "E-services portal", AppType.External, 2),
        new("FIELD", "تطبيق الزيارات الميدانية", "Field app", AppType.Field, 3),
    ];

    public static IReadOnlyList<ResourceDef> Resources { get; } =
    [
        new(HomeResource, "الرئيسية", "Home", "INTERNAL", null, [PrivilegeActions.View]),
        new(AppsControlResources.Root, "التحكم بالتطبيقات", "Apps Control", "INTERNAL", null, [PrivilegeActions.View]),
        new(AppsControlResources.Installation, "إعداد النظام والجهة", "Installation & entity", "INTERNAL", AppsControlResources.Root, EditActions),
        new(AppsControlResources.Organization, "الهيكل التنظيمي", "Organization structure", "INTERNAL", AppsControlResources.Root, EditActions),
        new(AppsControlResources.Users, "المستخدمون", "Users", "INTERNAL", AppsControlResources.Root, EditActions),
        new(AppsControlResources.Roles, "الأدوار والصلاحيات", "Roles & privileges", "INTERNAL", AppsControlResources.Root, EditActions),
        new(AppsControlResources.Resources, "الموارد", "Resources", "INTERNAL", AppsControlResources.Root, EditActions),
        new(AppsControlResources.Menus, "القوائم", "Menus", "INTERNAL", AppsControlResources.Root, EditActions),
        new(AppsControlResources.Classes, "التصنيفات والقيم", "Classes & domains", "INTERNAL", AppsControlResources.Root, EditActions),
        new(AppsControlResources.Lovs, "قوائم القيم", "Lists of values", "INTERNAL", AppsControlResources.Root, EditActions),
        new(AppsControlResources.LovSql, "استعلامات قوائم القيم", "LOV restricted SQL", "INTERNAL", AppsControlResources.Lovs, [PrivilegeActions.Edit], ResourceType.Api),
        new(AppsControlResources.Settings, "الإعدادات", "Settings", "INTERNAL", AppsControlResources.Root, EditActions),
        new(AppsControlResources.UiTexts, "نصوص الواجهة", "UI texts", "INTERNAL", AppsControlResources.Root, EditActions),
        new(AppsControlResources.ChangeSets, "طلبات التغيير", "Change sets", "INTERNAL", AppsControlResources.Root, [PrivilegeActions.View, PrivilegeActions.Create, PrivilegeActions.Approve]),
        new(AppsControlResources.ApprovalPolicy, "سياسة الاعتماد", "Approval policy", "INTERNAL", AppsControlResources.Root, [PrivilegeActions.View, PrivilegeActions.Edit]),
    ];

    public static IReadOnlyList<MenuDef> AppsControlMenus { get; } =
    [
        new("apps-control.change-sets", "طلبات التغيير", "Change sets", "/apps-control/change-sets", AppsControlResources.ChangeSets, "fact_check", 10),
        new("apps-control.classes", "التصنيفات والقيم", "Classes & domains", "/apps-control/classes", AppsControlResources.Classes, "category", 20),
        new("apps-control.lovs", "قوائم القيم", "Lists of values", "/apps-control/lovs", AppsControlResources.Lovs, "filter_list", 30),
        new("apps-control.organization", "الهيكل التنظيمي", "Organization structure", "/apps-control/organization", AppsControlResources.Organization, "account_tree", 40),
        new("apps-control.users", "المستخدمون", "Users", "/apps-control/users", AppsControlResources.Users, "group", 50),
        new("apps-control.roles", "الأدوار والصلاحيات", "Roles & privileges", "/apps-control/roles", AppsControlResources.Roles, "admin_panel_settings", 60),
        new("apps-control.menus", "القوائم", "Menus", "/apps-control/menus", AppsControlResources.Menus, "menu", 70),
        new("apps-control.resources", "الموارد", "Resources", "/apps-control/resources", AppsControlResources.Resources, "lock", 80),
        new("apps-control.settings", "الإعدادات", "Settings", "/apps-control/settings", AppsControlResources.Settings, "settings", 90),
        new("apps-control.ui-texts", "نصوص الواجهة", "UI texts", "/apps-control/ui-texts", AppsControlResources.UiTexts, "translate", 100),
        new("apps-control.installation", "النظام والجهة", "Installation & entity", "/apps-control/installation", AppsControlResources.Installation, "domain", 110),
        new("apps-control.approval-policy", "سياسة الاعتماد", "Approval policy", "/apps-control/approval-policy", AppsControlResources.ApprovalPolicy, "rule", 120),
    ];

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

    /// <summary>Role → actions granted per resource; <c>null</c> means no privilege on that resource.</summary>
    public static IReadOnlyDictionary<string, (string NameAr, string NameEn, Func<ResourceDef, string[]?> Actions)> Roles { get; } =
        new Dictionary<string, (string, string, Func<ResourceDef, string[]?>)>
        {
            [SystemAdminRole] = ("مدير النظام", "System administrator", r => r.Actions),
            [ConfigMakerRole] = ("مُعد الإعدادات", "Configuration maker", r => r.Code switch
            {
                AppsControlResources.ChangeSets => [PrivilegeActions.View, PrivilegeActions.Create],
                AppsControlResources.LovSql => null,
                _ => r.Actions,
            }),
            [ConfigCheckerRole] = ("مُعتمد الإعدادات", "Configuration checker", r => r.Code switch
            {
                AppsControlResources.ChangeSets => [PrivilegeActions.View, PrivilegeActions.Approve],
                AppsControlResources.LovSql => null,
                _ => r.Actions.Contains(PrivilegeActions.View) ? [PrivilegeActions.View] : null,
            }),
        };
}
