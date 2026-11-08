using DynamicCube.Modules.AppsControl.Domain;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Persistence;

/// <summary>
/// Table and column names of the Apps Control schema, following the naming of the legacy Apps Control it replaces
/// (Oracle <c>SYS_*</c> / <c>DD_*</c> tables, in lower case): the legacy name where a legacy table or column exists,
/// the same words (<c>_desc_ntv</c> / <c>_desc_frn</c>, <c>instid</c>, <c>cmp_num</c>, <c>sys_</c> / <c>dd_</c> /
/// <c>org_</c> / <c>bas_</c>) where it does not. Every other column keeps the snake_case name of its property. The
/// mapping is applied after the model is built, so entities and their configuration keep their C# names; see
/// docs/handover/legacy-apps-control-mapping.md.
/// </summary>
internal static class AppsControlNaming
{
    /// <summary>Columns named the same way in every Apps Control table.</summary>
    private static readonly Dictionary<string, string> Common = new(StringComparer.Ordinal)
    {
        ["EntityCode"] = "instid",
        ["CompanyCode"] = "cmp_num",
        ["Companies"] = "cmp_nums",
        ["DisplayOrder"] = "order_num",
    };

    /// <summary>Each entity's table and the columns named after the legacy schema.</summary>
    private static readonly Dictionary<Type, (string Table, Dictionary<string, string> Columns)> Tables = new()
    {
        [typeof(GovernmentEntity)] = ("sys_instance_profile", Columns(
            ("Code", "instid"), ("EntityCode", "ref_instid"), ("NameAr", "instname_ntv"), ("NameEn", "instname_frn"),
            ("IsActive", "activeflag"), ("LogoFileKey", "instlogo"))),
        [typeof(Installation)] = ("sys_installation", Columns(
            ("Code", "install_code"), ("NameAr", "install_desc_ntv"), ("NameEn", "install_desc_frn"), ("SystemType", "systemtypeid"))),
        [typeof(Company)] = ("sys_company_profile", Columns(
            ("Code", "cmp_num"), ("NameAr", "cmp_des_ntv"), ("NameEn", "cmp_des_frn"), ("IsActive", "active_flag"))),
        [typeof(Branch)] = ("sys_company_branches", Columns(
            ("Code", "branch_code"), ("NameAr", "branch_desc_ntv"), ("NameEn", "branch_desc_frn"))),
        [typeof(App)] = ("sys_apps_profile", Columns(
            ("Code", "appsid"), ("NameAr", "apps_des_ntv"), ("NameEn", "apps_des_frn"), ("IsActive", "active_flag"))),
        [typeof(BusinessApplication)] = ("sys_business_apps", Columns(
            ("Code", "business_app_code"), ("NameAr", "business_app_desc_ntv"), ("NameEn", "business_app_desc_frn"),
            ("DescriptionAr", "business_app_long_desc_ntv"), ("DescriptionEn", "business_app_long_desc_frn"))),
        [typeof(AppBranding)] = ("sys_apps_branding", Columns(
            ("Code", "branding_code"), ("NameAr", "branding_desc_ntv"), ("NameEn", "branding_desc_frn"))),
        [typeof(BrandingAsset)] = ("sys_branding_assets", Columns()),
        [typeof(UserAccount)] = ("sys_users", Columns(
            ("Code", "userid"), ("NameAr", "usernamentv"), ("NameEn", "usernamefrn"), ("Email", "useremail"), ("Mobile", "userphoneno"),
            ("LastLoginAt", "last_login_date"), ("LastCompanyCode", "last_cmp_num"))),
        [typeof(Role)] = ("sys_roles", Columns(
            ("Code", "role_code"), ("NameAr", "role_desc_ntv"), ("NameEn", "role_desc_frn"), ("ReferenceCode", "ref_code"),
            ("OverrideCompanies", "override_cmp_nums"))),
        [typeof(UserRole)] = ("sys_user_roles", Columns()),
        [typeof(Resource)] = ("sys_resources", Columns(
            ("Code", "resource_code"), ("NameAr", "resource_desc_ntv"), ("NameEn", "resource_desc_frn"))),
        [typeof(Menu)] = ("sys_menu", Columns(
            ("Code", "menu_code"), ("NameAr", "menu_desc_ntv"), ("NameEn", "menu_desc_frn"),
            ("DescriptionAr", "menu_long_desc_ntv"), ("DescriptionEn", "menu_long_desc_frn"), ("Icon", "menu_icon"),
            ("IsVisible", "show_flag"), ("ParentId", "parent_menu_id"))),
        [typeof(MenuGroup)] = ("sys_menu_groups", Columns(
            ("Code", "menu_group_code"), ("NameAr", "menu_group_des_ntv"), ("NameEn", "menu_group_des_frn"),
            ("DisplayOrder", "menu_group_order"), ("Icon", "menu_icon"))),
        [typeof(RoleMenu)] = ("sys_role_menus", Columns()),
        [typeof(Privilege)] = ("sys_role_privileges", Columns()),
        [typeof(LovGrant)] = ("sys_role_menu_lovs", Columns(("GrantedValues", "value_codes"), ("DefaultValue", "default_value_code"))),
        [typeof(LovBinding)] = ("sys_menu_lovs", Columns()),
        [typeof(UserPrivilege)] = ("sys_user_privileges", Columns()),
        [typeof(UserPrivilegeGrant)] = ("sys_user_privilege_lovs", Columns(("GrantedValues", "value_codes"))),
        [typeof(Setting)] = ("sys_settings", Columns(
            ("Code", "setting_code"), ("NameAr", "setting_desc_ntv"), ("NameEn", "setting_desc_frn"), ("Description", "setting_long_desc"))),
        [typeof(UiText)] = ("sys_ui_texts", Columns(("Code", "text_code"), ("NameAr", "text_desc_ntv"), ("NameEn", "text_desc_frn"))),
        [typeof(ChangeArea)] = ("sys_change_areas", Columns(("Code", "area_code"), ("NameAr", "area_desc_ntv"), ("NameEn", "area_desc_frn"))),
        [typeof(DictionaryClass)] = ("dd_classes", Columns(
            ("Code", "class_code"), ("NameAr", "class_desc_ntv"), ("NameEn", "class_desc_frn"),
            ("DescriptionAr", "class_long_desc_ntv"), ("DescriptionEn", "class_long_desc_frn"),
            ("ParentClassId", "reference_class_id"), ("AllowMultiAssign", "virtual_enable_multiple_assign"))),
        [typeof(AttributeDefinition)] = ("dd_class_attributes", Columns(
            ("Code", "attribute_code"), ("NameAr", "attribute_label_ntv"), ("NameEn", "attribute_label_frn"), ("DataType", "attribute_type"))),
        [typeof(DomainValue)] = ("dd_domain", Columns(
            ("Code", "value"), ("NameAr", "domain_desc_ntv"), ("NameEn", "domain_desc_frn"),
            ("DetailsAr", "domain_desc_detail_ntv"), ("DetailsEn", "domain_desc_detail_frn"), ("DisplayOrder", "orderby"),
            ("IsActive", "active_flag"))),
        [typeof(Lov)] = ("dd_lovs", Columns(
            ("Code", "lov_code"), ("NameAr", "lov_desc_ntv"), ("NameEn", "lov_desc_frn"), ("SqlText", "sqlstmt"),
            ("ValueColumn", "value_field"), ("ClassId", "ref_class_id"))),
        [typeof(DataObject)] = ("dd_data_objects", Columns(("Code", "object_code"), ("NameAr", "object_desc_ntv"), ("NameEn", "object_desc_frn"))),
        [typeof(DataScopeBinding)] = ("dd_data_scope_bindings", Columns(("ColumnName", "value_field"))),
        [typeof(OrgUnit)] = ("org_units", Columns(
            ("Code", "unit_code"), ("NameAr", "unit_desc_ntv"), ("NameEn", "unit_desc_frn"), ("ParentId", "parent_unit_id"))),
        [typeof(OrgUnitType)] = ("org_unit_types", Columns(("Code", "unit_type_code"), ("NameAr", "unit_type_desc_ntv"), ("NameEn", "unit_type_desc_frn"))),
        [typeof(Position)] = ("org_positions", Columns(("Code", "position_code"), ("NameAr", "position_desc_ntv"), ("NameEn", "position_desc_frn"))),
        [typeof(UserAssignment)] = ("org_user_assignments", Columns()),
        [typeof(Holiday)] = ("bas_holidays", Columns(("Code", "holiday_code"), ("NameAr", "holiday_desc_ntv"), ("NameEn", "holiday_desc_frn"))),
    };

    /// <summary>The table an entity is stored in (for the few statements written in SQL).</summary>
    public static string Table<T>() => Tables[typeof(T)].Table;

    /// <summary>The column a property is stored in.</summary>
    public static string Column<T>(string property) =>
        Tables[typeof(T)].Columns.GetValueOrDefault(property) ?? Common.GetValueOrDefault(property) ?? SnakeCase(property);

    /// <summary>Renames the Apps Control tables and columns of the built model (other schemas are left as they are).</summary>
    public static void Apply(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            if (!Tables.TryGetValue(entity.ClrType, out var names) || entity.GetSchema() != AppsControlDbContext.Schema)
            {
                continue;
            }

            entity.SetTableName(names.Table);
            foreach (var property in entity.GetProperties())
            {
                if (names.Columns.TryGetValue(property.Name, out var column) || Common.TryGetValue(property.Name, out column))
                {
                    property.SetColumnName(column);
                }
            }
        }
    }

    private static Dictionary<string, string> Columns(params (string Property, string Column)[] columns) =>
        columns.ToDictionary(c => c.Property, c => c.Column, StringComparer.Ordinal);

    private static string SnakeCase(string name) =>
        string.Concat(name.Select((c, i) => char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}
