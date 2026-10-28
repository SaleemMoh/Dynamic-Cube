namespace DynamicCube.Modules.AppsControl.Changes;

/// <summary>Entity types, change-set areas and guarding resources of Apps Control configuration.</summary>
public static class AppsControlChangeTypes
{
    public const string Installation = "apps.installation";
    public const string Entity = "apps.entity";
    public const string App = "apps.app";
    public const string OrgUnitType = "apps.org-unit-type";
    public const string OrgUnit = "apps.org-unit";
    public const string Position = "apps.position";
    public const string User = "apps.user";
    public const string Role = "apps.role";
    public const string Resource = "apps.resource";
    public const string MenuGroup = "apps.menu-group";
    public const string Menu = "apps.menu";
    public const string Class = "apps.class";
    public const string Domain = "apps.domain";
    public const string DataObject = "apps.data-object";
    public const string Lov = "apps.lov";
    public const string Setting = "apps.setting";
    public const string UiText = "apps.ui-text";
}

public static class AppsControlAreas
{
    public const string System = "apps.system";
    public const string Organization = "apps.organization";
    public const string Security = "apps.security";
    public const string Menus = "apps.menus";
    public const string Lookups = "apps.lookups";
    public const string Lovs = "apps.lovs";
    public const string Settings = "apps.settings";
    public const string UiTexts = "apps.ui-texts";
}

/// <summary>Codes of the SYSTEM resources that guard Apps Control screens and APIs.</summary>
public static class AppsControlResources
{
    public const string Root = "apps-control";
    public const string Installation = "apps-control.installation";
    public const string Organization = "apps-control.organization";
    public const string Users = "apps-control.users";
    public const string Roles = "apps-control.roles";
    public const string Resources = "apps-control.resources";
    public const string Menus = "apps-control.menus";
    public const string Classes = "apps-control.classes";
    public const string Lovs = "apps-control.lovs";
    public const string LovSql = "apps-control.lov-sql";
    public const string Settings = "apps-control.settings";
    public const string UiTexts = "apps-control.ui-texts";
    public const string ChangeSets = "apps-control.change-sets";
}
