using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.AppsControl.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LegacyTableAndColumnNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_class_class_parent_class_id",
                schema: "apps",
                table: "class");

            migrationBuilder.DropForeignKey(
                name: "fk_class_attribute_class_class_id",
                schema: "apps",
                table: "class_attribute");

            migrationBuilder.DropForeignKey(
                name: "fk_data_scope_binding_data_object_data_object_id",
                schema: "apps",
                table: "data_scope_binding");

            migrationBuilder.DropForeignKey(
                name: "fk_data_scope_binding_lovs_lov_id",
                schema: "apps",
                table: "data_scope_binding");

            migrationBuilder.DropForeignKey(
                name: "fk_domain_class_class_id",
                schema: "apps",
                table: "domain");

            migrationBuilder.DropForeignKey(
                name: "fk_domain_domain_parent_domain_id",
                schema: "apps",
                table: "domain");

            migrationBuilder.DropForeignKey(
                name: "fk_lov_class_class_id",
                schema: "apps",
                table: "lov");

            migrationBuilder.DropForeignKey(
                name: "fk_lov_data_object_data_object_id",
                schema: "apps",
                table: "lov");

            migrationBuilder.DropForeignKey(
                name: "fk_lov_binding_lov_lov_id",
                schema: "apps",
                table: "lov_binding");

            migrationBuilder.DropForeignKey(
                name: "fk_lov_binding_resource_resource_id",
                schema: "apps",
                table: "lov_binding");

            migrationBuilder.DropForeignKey(
                name: "fk_lov_grant_lov_lov_id",
                schema: "apps",
                table: "lov_grant");

            migrationBuilder.DropForeignKey(
                name: "fk_lov_grant_resource_resource_id",
                schema: "apps",
                table: "lov_grant");

            migrationBuilder.DropForeignKey(
                name: "fk_lov_grant_role_role_id",
                schema: "apps",
                table: "lov_grant");

            migrationBuilder.DropForeignKey(
                name: "fk_menu_app_app_id",
                schema: "apps",
                table: "menu");

            migrationBuilder.DropForeignKey(
                name: "fk_menu_menu_group_menu_group_id",
                schema: "apps",
                table: "menu");

            migrationBuilder.DropForeignKey(
                name: "fk_menu_menu_parent_id",
                schema: "apps",
                table: "menu");

            migrationBuilder.DropForeignKey(
                name: "fk_menu_resource_resource_id",
                schema: "apps",
                table: "menu");

            migrationBuilder.DropForeignKey(
                name: "fk_menu_group_app_app_id",
                schema: "apps",
                table: "menu_group");

            migrationBuilder.DropForeignKey(
                name: "fk_org_unit_org_unit_parent_id",
                schema: "apps",
                table: "org_unit");

            migrationBuilder.DropForeignKey(
                name: "fk_org_unit_org_unit_type_org_unit_type_id",
                schema: "apps",
                table: "org_unit");

            migrationBuilder.DropForeignKey(
                name: "fk_privilege_resource_resource_id",
                schema: "apps",
                table: "privilege");

            migrationBuilder.DropForeignKey(
                name: "fk_privilege_role_role_id",
                schema: "apps",
                table: "privilege");

            migrationBuilder.DropForeignKey(
                name: "fk_resource_app_app_id",
                schema: "apps",
                table: "resource");

            migrationBuilder.DropForeignKey(
                name: "fk_resource_resource_parent_id",
                schema: "apps",
                table: "resource");

            migrationBuilder.DropForeignKey(
                name: "fk_role_app_app_id",
                schema: "apps",
                table: "role");

            migrationBuilder.DropForeignKey(
                name: "fk_role_menu_menus_menu_id",
                schema: "apps",
                table: "role_menu");

            migrationBuilder.DropForeignKey(
                name: "fk_role_menu_role_role_id",
                schema: "apps",
                table: "role_menu");

            migrationBuilder.DropForeignKey(
                name: "fk_setting_app_app_id",
                schema: "apps",
                table: "setting");

            migrationBuilder.DropForeignKey(
                name: "fk_ui_text_app_app_id",
                schema: "apps",
                table: "ui_text");

            migrationBuilder.DropForeignKey(
                name: "fk_user_assignment_org_unit_org_unit_id",
                schema: "apps",
                table: "user_assignment");

            migrationBuilder.DropForeignKey(
                name: "fk_user_assignment_position_position_id",
                schema: "apps",
                table: "user_assignment");

            migrationBuilder.DropForeignKey(
                name: "fk_user_assignment_user_account_user_id",
                schema: "apps",
                table: "user_assignment");

            migrationBuilder.DropForeignKey(
                name: "fk_user_privilege_resources_resource_id",
                schema: "apps",
                table: "user_privilege");

            migrationBuilder.DropForeignKey(
                name: "fk_user_privilege_user_account_user_id",
                schema: "apps",
                table: "user_privilege");

            migrationBuilder.DropForeignKey(
                name: "fk_user_privilege_grant_lovs_lov_id",
                schema: "apps",
                table: "user_privilege_grant");

            migrationBuilder.DropForeignKey(
                name: "fk_user_privilege_grant_user_privileges_user_privilege_id",
                schema: "apps",
                table: "user_privilege_grant");

            migrationBuilder.DropForeignKey(
                name: "fk_user_role_org_unit_org_unit_id",
                schema: "apps",
                table: "user_role");

            migrationBuilder.DropForeignKey(
                name: "fk_user_role_roles_role_id",
                schema: "apps",
                table: "user_role");

            migrationBuilder.DropForeignKey(
                name: "fk_user_role_user_account_user_id",
                schema: "apps",
                table: "user_role");

            migrationBuilder.DropPrimaryKey(
                name: "pk_user_role",
                schema: "apps",
                table: "user_role");

            migrationBuilder.DropPrimaryKey(
                name: "pk_user_privilege_grant",
                schema: "apps",
                table: "user_privilege_grant");

            migrationBuilder.DropPrimaryKey(
                name: "pk_user_privilege",
                schema: "apps",
                table: "user_privilege");

            migrationBuilder.DropPrimaryKey(
                name: "pk_user_assignment",
                schema: "apps",
                table: "user_assignment");

            migrationBuilder.DropPrimaryKey(
                name: "pk_user_account",
                schema: "apps",
                table: "user_account");

            migrationBuilder.DropPrimaryKey(
                name: "pk_ui_text",
                schema: "apps",
                table: "ui_text");

            migrationBuilder.DropPrimaryKey(
                name: "pk_setting",
                schema: "apps",
                table: "setting");

            migrationBuilder.DropPrimaryKey(
                name: "pk_role_menu",
                schema: "apps",
                table: "role_menu");

            migrationBuilder.DropPrimaryKey(
                name: "pk_role",
                schema: "apps",
                table: "role");

            migrationBuilder.DropPrimaryKey(
                name: "pk_resource",
                schema: "apps",
                table: "resource");

            migrationBuilder.DropPrimaryKey(
                name: "pk_privilege",
                schema: "apps",
                table: "privilege");

            migrationBuilder.DropPrimaryKey(
                name: "pk_position",
                schema: "apps",
                table: "position");

            migrationBuilder.DropPrimaryKey(
                name: "pk_org_unit_type",
                schema: "apps",
                table: "org_unit_type");

            migrationBuilder.DropPrimaryKey(
                name: "pk_org_unit",
                schema: "apps",
                table: "org_unit");

            migrationBuilder.DropPrimaryKey(
                name: "pk_menu_group",
                schema: "apps",
                table: "menu_group");

            migrationBuilder.DropPrimaryKey(
                name: "pk_menu",
                schema: "apps",
                table: "menu");

            migrationBuilder.DropPrimaryKey(
                name: "pk_lov_grant",
                schema: "apps",
                table: "lov_grant");

            migrationBuilder.DropPrimaryKey(
                name: "pk_lov_binding",
                schema: "apps",
                table: "lov_binding");

            migrationBuilder.DropPrimaryKey(
                name: "pk_lov",
                schema: "apps",
                table: "lov");

            migrationBuilder.DropPrimaryKey(
                name: "pk_installation",
                schema: "apps",
                table: "installation");

            migrationBuilder.DropPrimaryKey(
                name: "pk_holiday",
                schema: "apps",
                table: "holiday");

            migrationBuilder.DropPrimaryKey(
                name: "pk_entity",
                schema: "apps",
                table: "entity");

            migrationBuilder.DropPrimaryKey(
                name: "pk_domain",
                schema: "apps",
                table: "domain");

            migrationBuilder.DropPrimaryKey(
                name: "pk_data_scope_binding",
                schema: "apps",
                table: "data_scope_binding");

            migrationBuilder.DropPrimaryKey(
                name: "pk_data_object",
                schema: "apps",
                table: "data_object");

            migrationBuilder.DropPrimaryKey(
                name: "pk_company",
                schema: "apps",
                table: "company");

            migrationBuilder.DropPrimaryKey(
                name: "pk_class_attribute",
                schema: "apps",
                table: "class_attribute");

            migrationBuilder.DropPrimaryKey(
                name: "pk_class",
                schema: "apps",
                table: "class");

            migrationBuilder.DropPrimaryKey(
                name: "pk_change_area",
                schema: "apps",
                table: "change_area");

            migrationBuilder.DropPrimaryKey(
                name: "pk_business_app",
                schema: "apps",
                table: "business_app");

            migrationBuilder.DropPrimaryKey(
                name: "pk_branding_asset",
                schema: "apps",
                table: "branding_asset");

            migrationBuilder.DropPrimaryKey(
                name: "pk_branch",
                schema: "apps",
                table: "branch");

            migrationBuilder.DropPrimaryKey(
                name: "pk_app_branding",
                schema: "apps",
                table: "app_branding");

            migrationBuilder.DropPrimaryKey(
                name: "pk_app",
                schema: "apps",
                table: "app");

            migrationBuilder.RenameTable(
                name: "user_role",
                schema: "apps",
                newName: "sys_user_roles",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "user_privilege_grant",
                schema: "apps",
                newName: "sys_user_privilege_lovs",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "user_privilege",
                schema: "apps",
                newName: "sys_user_privileges",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "user_assignment",
                schema: "apps",
                newName: "org_user_assignments",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "user_account",
                schema: "apps",
                newName: "sys_users",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "ui_text",
                schema: "apps",
                newName: "sys_ui_texts",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "setting",
                schema: "apps",
                newName: "sys_settings",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "role_menu",
                schema: "apps",
                newName: "sys_role_menus",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "role",
                schema: "apps",
                newName: "sys_roles",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "resource",
                schema: "apps",
                newName: "sys_resources",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "privilege",
                schema: "apps",
                newName: "sys_role_privileges",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "position",
                schema: "apps",
                newName: "org_positions",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "org_unit_type",
                schema: "apps",
                newName: "org_unit_types",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "org_unit",
                schema: "apps",
                newName: "org_units",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "menu_group",
                schema: "apps",
                newName: "sys_menu_groups",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "menu",
                schema: "apps",
                newName: "sys_menu",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "lov_grant",
                schema: "apps",
                newName: "sys_role_menu_lovs",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "lov_binding",
                schema: "apps",
                newName: "sys_menu_lovs",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "lov",
                schema: "apps",
                newName: "dd_lovs",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "installation",
                schema: "apps",
                newName: "sys_installation",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "holiday",
                schema: "apps",
                newName: "bas_holidays",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "entity",
                schema: "apps",
                newName: "sys_instance_profile",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "domain",
                schema: "apps",
                newName: "dd_domain",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "data_scope_binding",
                schema: "apps",
                newName: "dd_data_scope_bindings",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "data_object",
                schema: "apps",
                newName: "dd_data_objects",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "company",
                schema: "apps",
                newName: "sys_company_profile",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "class_attribute",
                schema: "apps",
                newName: "dd_class_attributes",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "class",
                schema: "apps",
                newName: "dd_classes",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "change_area",
                schema: "apps",
                newName: "sys_change_areas",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "business_app",
                schema: "apps",
                newName: "sys_business_apps",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "branding_asset",
                schema: "apps",
                newName: "sys_branding_assets",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "branch",
                schema: "apps",
                newName: "sys_company_branches",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "app_branding",
                schema: "apps",
                newName: "sys_apps_branding",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "app",
                schema: "apps",
                newName: "sys_apps_profile",
                newSchema: "apps");

            migrationBuilder.RenameColumn(
                name: "company_code",
                schema: "apps",
                table: "sys_user_roles",
                newName: "cmp_num");

            migrationBuilder.RenameIndex(
                name: "ix_user_role_user_id",
                schema: "apps",
                table: "sys_user_roles",
                newName: "ix_sys_user_roles_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_user_role_role_id",
                schema: "apps",
                table: "sys_user_roles",
                newName: "ix_sys_user_roles_role_id");

            migrationBuilder.RenameIndex(
                name: "ix_user_role_org_unit_id",
                schema: "apps",
                table: "sys_user_roles",
                newName: "ix_sys_user_roles_org_unit_id");

            migrationBuilder.RenameColumn(
                name: "granted_values",
                schema: "apps",
                table: "sys_user_privilege_lovs",
                newName: "value_codes");

            migrationBuilder.RenameIndex(
                name: "ix_user_privilege_grant_user_privilege_id_lov_id",
                schema: "apps",
                table: "sys_user_privilege_lovs",
                newName: "ix_sys_user_privilege_lovs_user_privilege_id_lov_id");

            migrationBuilder.RenameIndex(
                name: "ix_user_privilege_grant_lov_id",
                schema: "apps",
                table: "sys_user_privilege_lovs",
                newName: "ix_sys_user_privilege_lovs_lov_id");

            migrationBuilder.RenameColumn(
                name: "company_code",
                schema: "apps",
                table: "sys_user_privileges",
                newName: "cmp_num");

            migrationBuilder.RenameIndex(
                name: "ix_user_privilege_user_id",
                schema: "apps",
                table: "sys_user_privileges",
                newName: "ix_sys_user_privileges_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_user_privilege_resource_id",
                schema: "apps",
                table: "sys_user_privileges",
                newName: "ix_sys_user_privileges_resource_id");

            migrationBuilder.RenameIndex(
                name: "ix_user_assignment_user_id",
                schema: "apps",
                table: "org_user_assignments",
                newName: "ix_org_user_assignments_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_user_assignment_position_id",
                schema: "apps",
                table: "org_user_assignments",
                newName: "ix_org_user_assignments_position_id");

            migrationBuilder.RenameIndex(
                name: "ix_user_assignment_org_unit_id",
                schema: "apps",
                table: "org_user_assignments",
                newName: "ix_org_user_assignments_org_unit_id");

            migrationBuilder.RenameColumn(
                name: "name_en",
                schema: "apps",
                table: "sys_users",
                newName: "usernamefrn");

            migrationBuilder.RenameColumn(
                name: "name_ar",
                schema: "apps",
                table: "sys_users",
                newName: "usernamentv");

            migrationBuilder.RenameColumn(
                name: "mobile",
                schema: "apps",
                table: "sys_users",
                newName: "userphoneno");

            migrationBuilder.RenameColumn(
                name: "last_login_at",
                schema: "apps",
                table: "sys_users",
                newName: "last_login_date");

            migrationBuilder.RenameColumn(
                name: "last_company_code",
                schema: "apps",
                table: "sys_users",
                newName: "last_cmp_num");

            migrationBuilder.RenameColumn(
                name: "entity_code",
                schema: "apps",
                table: "sys_users",
                newName: "instid");

            migrationBuilder.RenameColumn(
                name: "email",
                schema: "apps",
                table: "sys_users",
                newName: "useremail");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "apps",
                table: "sys_users",
                newName: "userid");

            migrationBuilder.RenameIndex(
                name: "ix_user_account_idp_subject",
                schema: "apps",
                table: "sys_users",
                newName: "ix_sys_users_idp_subject");

            migrationBuilder.RenameIndex(
                name: "ix_user_account_code",
                schema: "apps",
                table: "sys_users",
                newName: "ix_sys_users_code");

            migrationBuilder.RenameColumn(
                name: "name_en",
                schema: "apps",
                table: "sys_ui_texts",
                newName: "text_desc_frn");

            migrationBuilder.RenameColumn(
                name: "name_ar",
                schema: "apps",
                table: "sys_ui_texts",
                newName: "text_desc_ntv");

            migrationBuilder.RenameColumn(
                name: "entity_code",
                schema: "apps",
                table: "sys_ui_texts",
                newName: "instid");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "apps",
                table: "sys_ui_texts",
                newName: "text_code");

            migrationBuilder.RenameIndex(
                name: "ix_ui_text_code_app_id",
                schema: "apps",
                table: "sys_ui_texts",
                newName: "ix_sys_ui_texts_code_app_id");

            migrationBuilder.RenameIndex(
                name: "ix_ui_text_app_id",
                schema: "apps",
                table: "sys_ui_texts",
                newName: "ix_sys_ui_texts_app_id");

            migrationBuilder.RenameColumn(
                name: "name_en",
                schema: "apps",
                table: "sys_settings",
                newName: "setting_desc_frn");

            migrationBuilder.RenameColumn(
                name: "name_ar",
                schema: "apps",
                table: "sys_settings",
                newName: "setting_desc_ntv");

            migrationBuilder.RenameColumn(
                name: "entity_code",
                schema: "apps",
                table: "sys_settings",
                newName: "instid");

            migrationBuilder.RenameColumn(
                name: "description",
                schema: "apps",
                table: "sys_settings",
                newName: "setting_long_desc");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "apps",
                table: "sys_settings",
                newName: "setting_code");

            migrationBuilder.RenameIndex(
                name: "ix_setting_code_app_id",
                schema: "apps",
                table: "sys_settings",
                newName: "ix_sys_settings_code_app_id");

            migrationBuilder.RenameIndex(
                name: "ix_setting_app_id",
                schema: "apps",
                table: "sys_settings",
                newName: "ix_sys_settings_app_id");

            migrationBuilder.RenameColumn(
                name: "company_code",
                schema: "apps",
                table: "sys_role_menus",
                newName: "cmp_num");

            migrationBuilder.RenameIndex(
                name: "ix_role_menu_role_id_menu_id_company_code",
                schema: "apps",
                table: "sys_role_menus",
                newName: "ix_sys_role_menus_role_id_menu_id_company_code");

            migrationBuilder.RenameIndex(
                name: "ix_role_menu_menu_id",
                schema: "apps",
                table: "sys_role_menus",
                newName: "ix_sys_role_menus_menu_id");

            migrationBuilder.RenameColumn(
                name: "reference_code",
                schema: "apps",
                table: "sys_roles",
                newName: "ref_code");

            migrationBuilder.RenameColumn(
                name: "override_companies",
                schema: "apps",
                table: "sys_roles",
                newName: "override_cmp_nums");

            migrationBuilder.RenameColumn(
                name: "name_en",
                schema: "apps",
                table: "sys_roles",
                newName: "role_desc_frn");

            migrationBuilder.RenameColumn(
                name: "name_ar",
                schema: "apps",
                table: "sys_roles",
                newName: "role_desc_ntv");

            migrationBuilder.RenameColumn(
                name: "entity_code",
                schema: "apps",
                table: "sys_roles",
                newName: "instid");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "apps",
                table: "sys_roles",
                newName: "role_code");

            migrationBuilder.RenameIndex(
                name: "ix_role_code",
                schema: "apps",
                table: "sys_roles",
                newName: "ix_sys_roles_code");

            migrationBuilder.RenameIndex(
                name: "ix_role_app_id",
                schema: "apps",
                table: "sys_roles",
                newName: "ix_sys_roles_app_id");

            migrationBuilder.RenameColumn(
                name: "name_en",
                schema: "apps",
                table: "sys_resources",
                newName: "resource_desc_frn");

            migrationBuilder.RenameColumn(
                name: "name_ar",
                schema: "apps",
                table: "sys_resources",
                newName: "resource_desc_ntv");

            migrationBuilder.RenameColumn(
                name: "entity_code",
                schema: "apps",
                table: "sys_resources",
                newName: "instid");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "apps",
                table: "sys_resources",
                newName: "resource_code");

            migrationBuilder.RenameIndex(
                name: "ix_resource_parent_id",
                schema: "apps",
                table: "sys_resources",
                newName: "ix_sys_resources_parent_id");

            migrationBuilder.RenameIndex(
                name: "ix_resource_code",
                schema: "apps",
                table: "sys_resources",
                newName: "ix_sys_resources_code");

            migrationBuilder.RenameIndex(
                name: "ix_resource_app_id",
                schema: "apps",
                table: "sys_resources",
                newName: "ix_sys_resources_app_id");

            migrationBuilder.RenameColumn(
                name: "company_code",
                schema: "apps",
                table: "sys_role_privileges",
                newName: "cmp_num");

            migrationBuilder.RenameIndex(
                name: "ix_privilege_role_id_resource_id_company_code",
                schema: "apps",
                table: "sys_role_privileges",
                newName: "ix_sys_role_privileges_role_id_resource_id_company_code");

            migrationBuilder.RenameIndex(
                name: "ix_privilege_resource_id",
                schema: "apps",
                table: "sys_role_privileges",
                newName: "ix_sys_role_privileges_resource_id");

            migrationBuilder.RenameColumn(
                name: "name_en",
                schema: "apps",
                table: "org_positions",
                newName: "position_desc_frn");

            migrationBuilder.RenameColumn(
                name: "name_ar",
                schema: "apps",
                table: "org_positions",
                newName: "position_desc_ntv");

            migrationBuilder.RenameColumn(
                name: "entity_code",
                schema: "apps",
                table: "org_positions",
                newName: "instid");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "apps",
                table: "org_positions",
                newName: "position_code");

            migrationBuilder.RenameIndex(
                name: "ix_position_code",
                schema: "apps",
                table: "org_positions",
                newName: "ix_org_positions_code");

            migrationBuilder.RenameColumn(
                name: "name_en",
                schema: "apps",
                table: "org_unit_types",
                newName: "unit_type_desc_frn");

            migrationBuilder.RenameColumn(
                name: "name_ar",
                schema: "apps",
                table: "org_unit_types",
                newName: "unit_type_desc_ntv");

            migrationBuilder.RenameColumn(
                name: "entity_code",
                schema: "apps",
                table: "org_unit_types",
                newName: "instid");

            migrationBuilder.RenameColumn(
                name: "display_order",
                schema: "apps",
                table: "org_unit_types",
                newName: "order_num");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "apps",
                table: "org_unit_types",
                newName: "unit_type_code");

            migrationBuilder.RenameIndex(
                name: "ix_org_unit_type_code",
                schema: "apps",
                table: "org_unit_types",
                newName: "ix_org_unit_types_code");

            migrationBuilder.RenameColumn(
                name: "parent_id",
                schema: "apps",
                table: "org_units",
                newName: "parent_unit_id");

            migrationBuilder.RenameColumn(
                name: "name_en",
                schema: "apps",
                table: "org_units",
                newName: "unit_desc_frn");

            migrationBuilder.RenameColumn(
                name: "name_ar",
                schema: "apps",
                table: "org_units",
                newName: "unit_desc_ntv");

            migrationBuilder.RenameColumn(
                name: "entity_code",
                schema: "apps",
                table: "org_units",
                newName: "instid");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "apps",
                table: "org_units",
                newName: "unit_code");

            migrationBuilder.RenameIndex(
                name: "ix_org_unit_path",
                schema: "apps",
                table: "org_units",
                newName: "ix_org_units_path");

            migrationBuilder.RenameIndex(
                name: "ix_org_unit_parent_id",
                schema: "apps",
                table: "org_units",
                newName: "ix_org_units_parent_id");

            migrationBuilder.RenameIndex(
                name: "ix_org_unit_org_unit_type_id",
                schema: "apps",
                table: "org_units",
                newName: "ix_org_units_org_unit_type_id");

            migrationBuilder.RenameIndex(
                name: "ix_org_unit_code",
                schema: "apps",
                table: "org_units",
                newName: "ix_org_units_code");

            migrationBuilder.RenameColumn(
                name: "name_en",
                schema: "apps",
                table: "sys_menu_groups",
                newName: "menu_group_des_frn");

            migrationBuilder.RenameColumn(
                name: "name_ar",
                schema: "apps",
                table: "sys_menu_groups",
                newName: "menu_group_des_ntv");

            migrationBuilder.RenameColumn(
                name: "icon",
                schema: "apps",
                table: "sys_menu_groups",
                newName: "menu_icon");

            migrationBuilder.RenameColumn(
                name: "entity_code",
                schema: "apps",
                table: "sys_menu_groups",
                newName: "instid");

            migrationBuilder.RenameColumn(
                name: "display_order",
                schema: "apps",
                table: "sys_menu_groups",
                newName: "menu_group_order");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "apps",
                table: "sys_menu_groups",
                newName: "menu_group_code");

            migrationBuilder.RenameIndex(
                name: "ix_menu_group_code",
                schema: "apps",
                table: "sys_menu_groups",
                newName: "ix_sys_menu_groups_code");

            migrationBuilder.RenameIndex(
                name: "ix_menu_group_app_id",
                schema: "apps",
                table: "sys_menu_groups",
                newName: "ix_sys_menu_groups_app_id");

            migrationBuilder.RenameColumn(
                name: "parent_id",
                schema: "apps",
                table: "sys_menu",
                newName: "parent_menu_id");

            migrationBuilder.RenameColumn(
                name: "name_en",
                schema: "apps",
                table: "sys_menu",
                newName: "menu_desc_frn");

            migrationBuilder.RenameColumn(
                name: "name_ar",
                schema: "apps",
                table: "sys_menu",
                newName: "menu_desc_ntv");

            migrationBuilder.RenameColumn(
                name: "is_visible",
                schema: "apps",
                table: "sys_menu",
                newName: "show_flag");

            migrationBuilder.RenameColumn(
                name: "icon",
                schema: "apps",
                table: "sys_menu",
                newName: "menu_icon");

            migrationBuilder.RenameColumn(
                name: "entity_code",
                schema: "apps",
                table: "sys_menu",
                newName: "instid");

            migrationBuilder.RenameColumn(
                name: "display_order",
                schema: "apps",
                table: "sys_menu",
                newName: "order_num");

            migrationBuilder.RenameColumn(
                name: "description_en",
                schema: "apps",
                table: "sys_menu",
                newName: "menu_long_desc_frn");

            migrationBuilder.RenameColumn(
                name: "description_ar",
                schema: "apps",
                table: "sys_menu",
                newName: "menu_long_desc_ntv");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "apps",
                table: "sys_menu",
                newName: "menu_code");

            migrationBuilder.RenameIndex(
                name: "ix_menu_resource_id",
                schema: "apps",
                table: "sys_menu",
                newName: "ix_sys_menu_resource_id");

            migrationBuilder.RenameIndex(
                name: "ix_menu_parent_id",
                schema: "apps",
                table: "sys_menu",
                newName: "ix_sys_menu_parent_id");

            migrationBuilder.RenameIndex(
                name: "ix_menu_menu_group_id",
                schema: "apps",
                table: "sys_menu",
                newName: "ix_sys_menu_menu_group_id");

            migrationBuilder.RenameIndex(
                name: "ix_menu_code",
                schema: "apps",
                table: "sys_menu",
                newName: "ix_sys_menu_code");

            migrationBuilder.RenameIndex(
                name: "ix_menu_app_id",
                schema: "apps",
                table: "sys_menu",
                newName: "ix_sys_menu_app_id");

            migrationBuilder.RenameColumn(
                name: "granted_values",
                schema: "apps",
                table: "sys_role_menu_lovs",
                newName: "value_codes");

            migrationBuilder.RenameColumn(
                name: "default_value",
                schema: "apps",
                table: "sys_role_menu_lovs",
                newName: "default_value_code");

            migrationBuilder.RenameColumn(
                name: "company_code",
                schema: "apps",
                table: "sys_role_menu_lovs",
                newName: "cmp_num");

            migrationBuilder.RenameIndex(
                name: "ix_lov_grant_role_id_lov_id_resource_id_company_code",
                schema: "apps",
                table: "sys_role_menu_lovs",
                newName: "ix_sys_role_menu_lovs_role_id_lov_id_resource_id_company_code");

            migrationBuilder.RenameIndex(
                name: "ix_lov_grant_resource_id",
                schema: "apps",
                table: "sys_role_menu_lovs",
                newName: "ix_sys_role_menu_lovs_resource_id");

            migrationBuilder.RenameIndex(
                name: "ix_lov_grant_lov_id",
                schema: "apps",
                table: "sys_role_menu_lovs",
                newName: "ix_sys_role_menu_lovs_lov_id");

            migrationBuilder.RenameIndex(
                name: "ix_lov_binding_resource_id_field_code",
                schema: "apps",
                table: "sys_menu_lovs",
                newName: "ix_sys_menu_lovs_resource_id_field_code");

            migrationBuilder.RenameIndex(
                name: "ix_lov_binding_lov_id",
                schema: "apps",
                table: "sys_menu_lovs",
                newName: "ix_sys_menu_lovs_lov_id");

            migrationBuilder.RenameColumn(
                name: "value_column",
                schema: "apps",
                table: "dd_lovs",
                newName: "value_field");

            migrationBuilder.RenameColumn(
                name: "sql_text",
                schema: "apps",
                table: "dd_lovs",
                newName: "sqlstmt");

            migrationBuilder.RenameColumn(
                name: "name_en",
                schema: "apps",
                table: "dd_lovs",
                newName: "lov_desc_frn");

            migrationBuilder.RenameColumn(
                name: "name_ar",
                schema: "apps",
                table: "dd_lovs",
                newName: "lov_desc_ntv");

            migrationBuilder.RenameColumn(
                name: "entity_code",
                schema: "apps",
                table: "dd_lovs",
                newName: "instid");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "apps",
                table: "dd_lovs",
                newName: "lov_code");

            migrationBuilder.RenameColumn(
                name: "class_id",
                schema: "apps",
                table: "dd_lovs",
                newName: "ref_class_id");

            migrationBuilder.RenameIndex(
                name: "ix_lov_data_object_id",
                schema: "apps",
                table: "dd_lovs",
                newName: "ix_dd_lovs_data_object_id");

            migrationBuilder.RenameIndex(
                name: "ix_lov_code",
                schema: "apps",
                table: "dd_lovs",
                newName: "ix_dd_lovs_code");

            migrationBuilder.RenameIndex(
                name: "ix_lov_class_id",
                schema: "apps",
                table: "dd_lovs",
                newName: "ix_dd_lovs_class_id");

            migrationBuilder.RenameColumn(
                name: "system_type",
                schema: "apps",
                table: "sys_installation",
                newName: "systemtypeid");

            migrationBuilder.RenameColumn(
                name: "name_en",
                schema: "apps",
                table: "sys_installation",
                newName: "install_desc_frn");

            migrationBuilder.RenameColumn(
                name: "name_ar",
                schema: "apps",
                table: "sys_installation",
                newName: "install_desc_ntv");

            migrationBuilder.RenameColumn(
                name: "entity_code",
                schema: "apps",
                table: "sys_installation",
                newName: "instid");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "apps",
                table: "sys_installation",
                newName: "install_code");

            migrationBuilder.RenameIndex(
                name: "ix_installation_code",
                schema: "apps",
                table: "sys_installation",
                newName: "ix_sys_installation_code");

            migrationBuilder.RenameColumn(
                name: "name_en",
                schema: "apps",
                table: "bas_holidays",
                newName: "holiday_desc_frn");

            migrationBuilder.RenameColumn(
                name: "name_ar",
                schema: "apps",
                table: "bas_holidays",
                newName: "holiday_desc_ntv");

            migrationBuilder.RenameColumn(
                name: "entity_code",
                schema: "apps",
                table: "bas_holidays",
                newName: "instid");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "apps",
                table: "bas_holidays",
                newName: "holiday_code");

            migrationBuilder.RenameIndex(
                name: "ix_holiday_code",
                schema: "apps",
                table: "bas_holidays",
                newName: "ix_bas_holidays_code");

            migrationBuilder.RenameColumn(
                name: "name_en",
                schema: "apps",
                table: "sys_instance_profile",
                newName: "instname_frn");

            migrationBuilder.RenameColumn(
                name: "name_ar",
                schema: "apps",
                table: "sys_instance_profile",
                newName: "instname_ntv");

            migrationBuilder.RenameColumn(
                name: "logo_file_key",
                schema: "apps",
                table: "sys_instance_profile",
                newName: "instlogo");

            migrationBuilder.RenameColumn(
                name: "is_active",
                schema: "apps",
                table: "sys_instance_profile",
                newName: "activeflag");

            migrationBuilder.RenameColumn(
                name: "entity_code",
                schema: "apps",
                table: "sys_instance_profile",
                newName: "ref_instid");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "apps",
                table: "sys_instance_profile",
                newName: "instid");

            migrationBuilder.RenameIndex(
                name: "ix_entity_code",
                schema: "apps",
                table: "sys_instance_profile",
                newName: "ix_sys_instance_profile_code");

            migrationBuilder.RenameColumn(
                name: "name_en",
                schema: "apps",
                table: "dd_domain",
                newName: "domain_desc_frn");

            migrationBuilder.RenameColumn(
                name: "name_ar",
                schema: "apps",
                table: "dd_domain",
                newName: "domain_desc_ntv");

            migrationBuilder.RenameColumn(
                name: "is_active",
                schema: "apps",
                table: "dd_domain",
                newName: "active_flag");

            migrationBuilder.RenameColumn(
                name: "entity_code",
                schema: "apps",
                table: "dd_domain",
                newName: "instid");

            migrationBuilder.RenameColumn(
                name: "display_order",
                schema: "apps",
                table: "dd_domain",
                newName: "orderby");

            migrationBuilder.RenameColumn(
                name: "details_en",
                schema: "apps",
                table: "dd_domain",
                newName: "domain_desc_detail_frn");

            migrationBuilder.RenameColumn(
                name: "details_ar",
                schema: "apps",
                table: "dd_domain",
                newName: "domain_desc_detail_ntv");

            migrationBuilder.RenameColumn(
                name: "companies",
                schema: "apps",
                table: "dd_domain",
                newName: "cmp_nums");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "apps",
                table: "dd_domain",
                newName: "value");

            migrationBuilder.RenameIndex(
                name: "ix_domain_parent_domain_id",
                schema: "apps",
                table: "dd_domain",
                newName: "ix_dd_domain_parent_domain_id");

            migrationBuilder.RenameIndex(
                name: "ix_domain_class_id_code",
                schema: "apps",
                table: "dd_domain",
                newName: "ix_dd_domain_class_id_code");

            migrationBuilder.RenameIndex(
                name: "ix_domain_attributes",
                schema: "apps",
                table: "dd_domain",
                newName: "ix_dd_domain_attributes");

            migrationBuilder.RenameColumn(
                name: "column_name",
                schema: "apps",
                table: "dd_data_scope_bindings",
                newName: "value_field");

            migrationBuilder.RenameIndex(
                name: "ix_data_scope_binding_lov_id",
                schema: "apps",
                table: "dd_data_scope_bindings",
                newName: "ix_dd_data_scope_bindings_lov_id");

            migrationBuilder.RenameIndex(
                name: "ix_data_scope_binding_data_object_id",
                schema: "apps",
                table: "dd_data_scope_bindings",
                newName: "ix_dd_data_scope_bindings_data_object_id");

            migrationBuilder.RenameColumn(
                name: "name_en",
                schema: "apps",
                table: "dd_data_objects",
                newName: "object_desc_frn");

            migrationBuilder.RenameColumn(
                name: "name_ar",
                schema: "apps",
                table: "dd_data_objects",
                newName: "object_desc_ntv");

            migrationBuilder.RenameColumn(
                name: "entity_code",
                schema: "apps",
                table: "dd_data_objects",
                newName: "instid");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "apps",
                table: "dd_data_objects",
                newName: "object_code");

            migrationBuilder.RenameIndex(
                name: "ix_data_object_code",
                schema: "apps",
                table: "dd_data_objects",
                newName: "ix_dd_data_objects_code");

            migrationBuilder.RenameColumn(
                name: "name_en",
                schema: "apps",
                table: "sys_company_profile",
                newName: "cmp_des_frn");

            migrationBuilder.RenameColumn(
                name: "name_ar",
                schema: "apps",
                table: "sys_company_profile",
                newName: "cmp_des_ntv");

            migrationBuilder.RenameColumn(
                name: "is_active",
                schema: "apps",
                table: "sys_company_profile",
                newName: "active_flag");

            migrationBuilder.RenameColumn(
                name: "entity_code",
                schema: "apps",
                table: "sys_company_profile",
                newName: "instid");

            migrationBuilder.RenameColumn(
                name: "display_order",
                schema: "apps",
                table: "sys_company_profile",
                newName: "order_num");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "apps",
                table: "sys_company_profile",
                newName: "cmp_num");

            migrationBuilder.RenameIndex(
                name: "ix_company_code",
                schema: "apps",
                table: "sys_company_profile",
                newName: "ix_sys_company_profile_code");

            migrationBuilder.RenameColumn(
                name: "name_en",
                schema: "apps",
                table: "dd_class_attributes",
                newName: "attribute_label_frn");

            migrationBuilder.RenameColumn(
                name: "name_ar",
                schema: "apps",
                table: "dd_class_attributes",
                newName: "attribute_label_ntv");

            migrationBuilder.RenameColumn(
                name: "display_order",
                schema: "apps",
                table: "dd_class_attributes",
                newName: "order_num");

            migrationBuilder.RenameColumn(
                name: "data_type",
                schema: "apps",
                table: "dd_class_attributes",
                newName: "attribute_type");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "apps",
                table: "dd_class_attributes",
                newName: "attribute_code");

            migrationBuilder.RenameIndex(
                name: "ix_class_attribute_class_id_code",
                schema: "apps",
                table: "dd_class_attributes",
                newName: "ix_dd_class_attributes_class_id_code");

            migrationBuilder.RenameColumn(
                name: "parent_class_id",
                schema: "apps",
                table: "dd_classes",
                newName: "reference_class_id");

            migrationBuilder.RenameColumn(
                name: "name_en",
                schema: "apps",
                table: "dd_classes",
                newName: "class_desc_frn");

            migrationBuilder.RenameColumn(
                name: "name_ar",
                schema: "apps",
                table: "dd_classes",
                newName: "class_desc_ntv");

            migrationBuilder.RenameColumn(
                name: "entity_code",
                schema: "apps",
                table: "dd_classes",
                newName: "instid");

            migrationBuilder.RenameColumn(
                name: "description_en",
                schema: "apps",
                table: "dd_classes",
                newName: "class_long_desc_frn");

            migrationBuilder.RenameColumn(
                name: "description_ar",
                schema: "apps",
                table: "dd_classes",
                newName: "class_long_desc_ntv");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "apps",
                table: "dd_classes",
                newName: "class_code");

            migrationBuilder.RenameColumn(
                name: "allow_multi_assign",
                schema: "apps",
                table: "dd_classes",
                newName: "virtual_enable_multiple_assign");

            migrationBuilder.RenameIndex(
                name: "ix_class_parent_class_id",
                schema: "apps",
                table: "dd_classes",
                newName: "ix_dd_classes_parent_class_id");

            migrationBuilder.RenameIndex(
                name: "ix_class_code",
                schema: "apps",
                table: "dd_classes",
                newName: "ix_dd_classes_code");

            migrationBuilder.RenameColumn(
                name: "name_en",
                schema: "apps",
                table: "sys_change_areas",
                newName: "area_desc_frn");

            migrationBuilder.RenameColumn(
                name: "name_ar",
                schema: "apps",
                table: "sys_change_areas",
                newName: "area_desc_ntv");

            migrationBuilder.RenameColumn(
                name: "entity_code",
                schema: "apps",
                table: "sys_change_areas",
                newName: "instid");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "apps",
                table: "sys_change_areas",
                newName: "area_code");

            migrationBuilder.RenameIndex(
                name: "ix_change_area_code",
                schema: "apps",
                table: "sys_change_areas",
                newName: "ix_sys_change_areas_code");

            migrationBuilder.RenameColumn(
                name: "name_en",
                schema: "apps",
                table: "sys_business_apps",
                newName: "business_app_desc_frn");

            migrationBuilder.RenameColumn(
                name: "name_ar",
                schema: "apps",
                table: "sys_business_apps",
                newName: "business_app_desc_ntv");

            migrationBuilder.RenameColumn(
                name: "entity_code",
                schema: "apps",
                table: "sys_business_apps",
                newName: "instid");

            migrationBuilder.RenameColumn(
                name: "display_order",
                schema: "apps",
                table: "sys_business_apps",
                newName: "order_num");

            migrationBuilder.RenameColumn(
                name: "description_en",
                schema: "apps",
                table: "sys_business_apps",
                newName: "business_app_long_desc_frn");

            migrationBuilder.RenameColumn(
                name: "description_ar",
                schema: "apps",
                table: "sys_business_apps",
                newName: "business_app_long_desc_ntv");

            migrationBuilder.RenameColumn(
                name: "companies",
                schema: "apps",
                table: "sys_business_apps",
                newName: "cmp_nums");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "apps",
                table: "sys_business_apps",
                newName: "business_app_code");

            migrationBuilder.RenameIndex(
                name: "ix_business_app_code",
                schema: "apps",
                table: "sys_business_apps",
                newName: "ix_sys_business_apps_code");

            migrationBuilder.RenameIndex(
                name: "ix_business_app_client_id",
                schema: "apps",
                table: "sys_business_apps",
                newName: "ix_sys_business_apps_client_id");

            migrationBuilder.RenameColumn(
                name: "name_en",
                schema: "apps",
                table: "sys_company_branches",
                newName: "branch_desc_frn");

            migrationBuilder.RenameColumn(
                name: "name_ar",
                schema: "apps",
                table: "sys_company_branches",
                newName: "branch_desc_ntv");

            migrationBuilder.RenameColumn(
                name: "entity_code",
                schema: "apps",
                table: "sys_company_branches",
                newName: "instid");

            migrationBuilder.RenameColumn(
                name: "display_order",
                schema: "apps",
                table: "sys_company_branches",
                newName: "order_num");

            migrationBuilder.RenameColumn(
                name: "company_code",
                schema: "apps",
                table: "sys_company_branches",
                newName: "cmp_num");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "apps",
                table: "sys_company_branches",
                newName: "branch_code");

            migrationBuilder.RenameIndex(
                name: "ix_branch_company_code",
                schema: "apps",
                table: "sys_company_branches",
                newName: "ix_sys_company_branches_company_code");

            migrationBuilder.RenameIndex(
                name: "ix_branch_code",
                schema: "apps",
                table: "sys_company_branches",
                newName: "ix_sys_company_branches_code");

            migrationBuilder.RenameColumn(
                name: "name_en",
                schema: "apps",
                table: "sys_apps_branding",
                newName: "branding_desc_frn");

            migrationBuilder.RenameColumn(
                name: "name_ar",
                schema: "apps",
                table: "sys_apps_branding",
                newName: "branding_desc_ntv");

            migrationBuilder.RenameColumn(
                name: "entity_code",
                schema: "apps",
                table: "sys_apps_branding",
                newName: "instid");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "apps",
                table: "sys_apps_branding",
                newName: "branding_code");

            migrationBuilder.RenameIndex(
                name: "ix_app_branding_code",
                schema: "apps",
                table: "sys_apps_branding",
                newName: "ix_sys_apps_branding_code");

            migrationBuilder.RenameColumn(
                name: "name_en",
                schema: "apps",
                table: "sys_apps_profile",
                newName: "apps_des_frn");

            migrationBuilder.RenameColumn(
                name: "name_ar",
                schema: "apps",
                table: "sys_apps_profile",
                newName: "apps_des_ntv");

            migrationBuilder.RenameColumn(
                name: "is_active",
                schema: "apps",
                table: "sys_apps_profile",
                newName: "active_flag");

            migrationBuilder.RenameColumn(
                name: "entity_code",
                schema: "apps",
                table: "sys_apps_profile",
                newName: "instid");

            migrationBuilder.RenameColumn(
                name: "display_order",
                schema: "apps",
                table: "sys_apps_profile",
                newName: "order_num");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "apps",
                table: "sys_apps_profile",
                newName: "appsid");

            migrationBuilder.RenameIndex(
                name: "ix_app_code",
                schema: "apps",
                table: "sys_apps_profile",
                newName: "ix_sys_apps_profile_code");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sys_user_roles",
                schema: "apps",
                table: "sys_user_roles",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sys_user_privilege_lovs",
                schema: "apps",
                table: "sys_user_privilege_lovs",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sys_user_privileges",
                schema: "apps",
                table: "sys_user_privileges",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_org_user_assignments",
                schema: "apps",
                table: "org_user_assignments",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sys_users",
                schema: "apps",
                table: "sys_users",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sys_ui_texts",
                schema: "apps",
                table: "sys_ui_texts",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sys_settings",
                schema: "apps",
                table: "sys_settings",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sys_role_menus",
                schema: "apps",
                table: "sys_role_menus",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sys_roles",
                schema: "apps",
                table: "sys_roles",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sys_resources",
                schema: "apps",
                table: "sys_resources",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sys_role_privileges",
                schema: "apps",
                table: "sys_role_privileges",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_org_positions",
                schema: "apps",
                table: "org_positions",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_org_unit_types",
                schema: "apps",
                table: "org_unit_types",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_org_units",
                schema: "apps",
                table: "org_units",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sys_menu_groups",
                schema: "apps",
                table: "sys_menu_groups",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sys_menu",
                schema: "apps",
                table: "sys_menu",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sys_role_menu_lovs",
                schema: "apps",
                table: "sys_role_menu_lovs",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sys_menu_lovs",
                schema: "apps",
                table: "sys_menu_lovs",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_dd_lovs",
                schema: "apps",
                table: "dd_lovs",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sys_installation",
                schema: "apps",
                table: "sys_installation",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_bas_holidays",
                schema: "apps",
                table: "bas_holidays",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sys_instance_profile",
                schema: "apps",
                table: "sys_instance_profile",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_dd_domain",
                schema: "apps",
                table: "dd_domain",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_dd_data_scope_bindings",
                schema: "apps",
                table: "dd_data_scope_bindings",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_dd_data_objects",
                schema: "apps",
                table: "dd_data_objects",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sys_company_profile",
                schema: "apps",
                table: "sys_company_profile",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_dd_class_attributes",
                schema: "apps",
                table: "dd_class_attributes",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_dd_classes",
                schema: "apps",
                table: "dd_classes",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sys_change_areas",
                schema: "apps",
                table: "sys_change_areas",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sys_business_apps",
                schema: "apps",
                table: "sys_business_apps",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sys_branding_assets",
                schema: "apps",
                table: "sys_branding_assets",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sys_company_branches",
                schema: "apps",
                table: "sys_company_branches",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sys_apps_branding",
                schema: "apps",
                table: "sys_apps_branding",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sys_apps_profile",
                schema: "apps",
                table: "sys_apps_profile",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_dd_class_attributes_class_class_id",
                schema: "apps",
                table: "dd_class_attributes",
                column: "class_id",
                principalSchema: "apps",
                principalTable: "dd_classes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_dd_classes_dd_classes_parent_class_id",
                schema: "apps",
                table: "dd_classes",
                column: "reference_class_id",
                principalSchema: "apps",
                principalTable: "dd_classes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_dd_data_scope_bindings_dd_data_objects_data_object_id",
                schema: "apps",
                table: "dd_data_scope_bindings",
                column: "data_object_id",
                principalSchema: "apps",
                principalTable: "dd_data_objects",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_dd_data_scope_bindings_lov_lov_id",
                schema: "apps",
                table: "dd_data_scope_bindings",
                column: "lov_id",
                principalSchema: "apps",
                principalTable: "dd_lovs",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_dd_domain_dd_classes_class_id",
                schema: "apps",
                table: "dd_domain",
                column: "class_id",
                principalSchema: "apps",
                principalTable: "dd_classes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_dd_domain_dd_domain_parent_domain_id",
                schema: "apps",
                table: "dd_domain",
                column: "parent_domain_id",
                principalSchema: "apps",
                principalTable: "dd_domain",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_dd_lovs_dd_classes_class_id",
                schema: "apps",
                table: "dd_lovs",
                column: "ref_class_id",
                principalSchema: "apps",
                principalTable: "dd_classes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_dd_lovs_dd_data_objects_data_object_id",
                schema: "apps",
                table: "dd_lovs",
                column: "data_object_id",
                principalSchema: "apps",
                principalTable: "dd_data_objects",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_org_units_org_unit_type_org_unit_type_id",
                schema: "apps",
                table: "org_units",
                column: "org_unit_type_id",
                principalSchema: "apps",
                principalTable: "org_unit_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_org_units_org_units_parent_id",
                schema: "apps",
                table: "org_units",
                column: "parent_unit_id",
                principalSchema: "apps",
                principalTable: "org_units",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_org_user_assignments_org_positions_position_id",
                schema: "apps",
                table: "org_user_assignments",
                column: "position_id",
                principalSchema: "apps",
                principalTable: "org_positions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_org_user_assignments_org_units_org_unit_id",
                schema: "apps",
                table: "org_user_assignments",
                column: "org_unit_id",
                principalSchema: "apps",
                principalTable: "org_units",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_org_user_assignments_sys_users_user_id",
                schema: "apps",
                table: "org_user_assignments",
                column: "user_id",
                principalSchema: "apps",
                principalTable: "sys_users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_menu_menu_group_menu_group_id",
                schema: "apps",
                table: "sys_menu",
                column: "menu_group_id",
                principalSchema: "apps",
                principalTable: "sys_menu_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_menu_resource_resource_id",
                schema: "apps",
                table: "sys_menu",
                column: "resource_id",
                principalSchema: "apps",
                principalTable: "sys_resources",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_menu_sys_apps_profile_app_id",
                schema: "apps",
                table: "sys_menu",
                column: "app_id",
                principalSchema: "apps",
                principalTable: "sys_apps_profile",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_menu_sys_menu_parent_id",
                schema: "apps",
                table: "sys_menu",
                column: "parent_menu_id",
                principalSchema: "apps",
                principalTable: "sys_menu",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_menu_groups_sys_apps_profile_app_id",
                schema: "apps",
                table: "sys_menu_groups",
                column: "app_id",
                principalSchema: "apps",
                principalTable: "sys_apps_profile",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_menu_lovs_dd_lovs_lov_id",
                schema: "apps",
                table: "sys_menu_lovs",
                column: "lov_id",
                principalSchema: "apps",
                principalTable: "dd_lovs",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_menu_lovs_resource_resource_id",
                schema: "apps",
                table: "sys_menu_lovs",
                column: "resource_id",
                principalSchema: "apps",
                principalTable: "sys_resources",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_resources_sys_apps_profile_app_id",
                schema: "apps",
                table: "sys_resources",
                column: "app_id",
                principalSchema: "apps",
                principalTable: "sys_apps_profile",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_resources_sys_resources_parent_id",
                schema: "apps",
                table: "sys_resources",
                column: "parent_id",
                principalSchema: "apps",
                principalTable: "sys_resources",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_role_menu_lovs_dd_lovs_lov_id",
                schema: "apps",
                table: "sys_role_menu_lovs",
                column: "lov_id",
                principalSchema: "apps",
                principalTable: "dd_lovs",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_role_menu_lovs_resource_resource_id",
                schema: "apps",
                table: "sys_role_menu_lovs",
                column: "resource_id",
                principalSchema: "apps",
                principalTable: "sys_resources",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_role_menu_lovs_role_role_id",
                schema: "apps",
                table: "sys_role_menu_lovs",
                column: "role_id",
                principalSchema: "apps",
                principalTable: "sys_roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_role_menus_sys_menu_menu_id",
                schema: "apps",
                table: "sys_role_menus",
                column: "menu_id",
                principalSchema: "apps",
                principalTable: "sys_menu",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_role_menus_sys_roles_role_id",
                schema: "apps",
                table: "sys_role_menus",
                column: "role_id",
                principalSchema: "apps",
                principalTable: "sys_roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_role_privileges_resource_resource_id",
                schema: "apps",
                table: "sys_role_privileges",
                column: "resource_id",
                principalSchema: "apps",
                principalTable: "sys_resources",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_role_privileges_role_role_id",
                schema: "apps",
                table: "sys_role_privileges",
                column: "role_id",
                principalSchema: "apps",
                principalTable: "sys_roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_roles_sys_apps_profile_app_id",
                schema: "apps",
                table: "sys_roles",
                column: "app_id",
                principalSchema: "apps",
                principalTable: "sys_apps_profile",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_settings_sys_apps_profile_app_id",
                schema: "apps",
                table: "sys_settings",
                column: "app_id",
                principalSchema: "apps",
                principalTable: "sys_apps_profile",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_ui_texts_sys_apps_profile_app_id",
                schema: "apps",
                table: "sys_ui_texts",
                column: "app_id",
                principalSchema: "apps",
                principalTable: "sys_apps_profile",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_user_privilege_lovs_dd_lovs_lov_id",
                schema: "apps",
                table: "sys_user_privilege_lovs",
                column: "lov_id",
                principalSchema: "apps",
                principalTable: "dd_lovs",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_user_privilege_lovs_sys_user_privileges_user_privilege_",
                schema: "apps",
                table: "sys_user_privilege_lovs",
                column: "user_privilege_id",
                principalSchema: "apps",
                principalTable: "sys_user_privileges",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_user_privileges_sys_resources_resource_id",
                schema: "apps",
                table: "sys_user_privileges",
                column: "resource_id",
                principalSchema: "apps",
                principalTable: "sys_resources",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_user_privileges_sys_users_user_id",
                schema: "apps",
                table: "sys_user_privileges",
                column: "user_id",
                principalSchema: "apps",
                principalTable: "sys_users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_user_roles_org_units_org_unit_id",
                schema: "apps",
                table: "sys_user_roles",
                column: "org_unit_id",
                principalSchema: "apps",
                principalTable: "org_units",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_user_roles_sys_roles_role_id",
                schema: "apps",
                table: "sys_user_roles",
                column: "role_id",
                principalSchema: "apps",
                principalTable: "sys_roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_user_roles_sys_users_user_id",
                schema: "apps",
                table: "sys_user_roles",
                column: "user_id",
                principalSchema: "apps",
                principalTable: "sys_users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.Sql(LegacyNamesDataFix.Sql(up: true));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(LegacyNamesDataFix.Sql(up: false));

            migrationBuilder.DropForeignKey(
                name: "fk_dd_class_attributes_class_class_id",
                schema: "apps",
                table: "dd_class_attributes");

            migrationBuilder.DropForeignKey(
                name: "fk_dd_classes_dd_classes_parent_class_id",
                schema: "apps",
                table: "dd_classes");

            migrationBuilder.DropForeignKey(
                name: "fk_dd_data_scope_bindings_dd_data_objects_data_object_id",
                schema: "apps",
                table: "dd_data_scope_bindings");

            migrationBuilder.DropForeignKey(
                name: "fk_dd_data_scope_bindings_lov_lov_id",
                schema: "apps",
                table: "dd_data_scope_bindings");

            migrationBuilder.DropForeignKey(
                name: "fk_dd_domain_dd_classes_class_id",
                schema: "apps",
                table: "dd_domain");

            migrationBuilder.DropForeignKey(
                name: "fk_dd_domain_dd_domain_parent_domain_id",
                schema: "apps",
                table: "dd_domain");

            migrationBuilder.DropForeignKey(
                name: "fk_dd_lovs_dd_classes_class_id",
                schema: "apps",
                table: "dd_lovs");

            migrationBuilder.DropForeignKey(
                name: "fk_dd_lovs_dd_data_objects_data_object_id",
                schema: "apps",
                table: "dd_lovs");

            migrationBuilder.DropForeignKey(
                name: "fk_org_units_org_unit_type_org_unit_type_id",
                schema: "apps",
                table: "org_units");

            migrationBuilder.DropForeignKey(
                name: "fk_org_units_org_units_parent_id",
                schema: "apps",
                table: "org_units");

            migrationBuilder.DropForeignKey(
                name: "fk_org_user_assignments_org_positions_position_id",
                schema: "apps",
                table: "org_user_assignments");

            migrationBuilder.DropForeignKey(
                name: "fk_org_user_assignments_org_units_org_unit_id",
                schema: "apps",
                table: "org_user_assignments");

            migrationBuilder.DropForeignKey(
                name: "fk_org_user_assignments_sys_users_user_id",
                schema: "apps",
                table: "org_user_assignments");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_menu_menu_group_menu_group_id",
                schema: "apps",
                table: "sys_menu");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_menu_resource_resource_id",
                schema: "apps",
                table: "sys_menu");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_menu_sys_apps_profile_app_id",
                schema: "apps",
                table: "sys_menu");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_menu_sys_menu_parent_id",
                schema: "apps",
                table: "sys_menu");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_menu_groups_sys_apps_profile_app_id",
                schema: "apps",
                table: "sys_menu_groups");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_menu_lovs_dd_lovs_lov_id",
                schema: "apps",
                table: "sys_menu_lovs");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_menu_lovs_resource_resource_id",
                schema: "apps",
                table: "sys_menu_lovs");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_resources_sys_apps_profile_app_id",
                schema: "apps",
                table: "sys_resources");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_resources_sys_resources_parent_id",
                schema: "apps",
                table: "sys_resources");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_role_menu_lovs_dd_lovs_lov_id",
                schema: "apps",
                table: "sys_role_menu_lovs");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_role_menu_lovs_resource_resource_id",
                schema: "apps",
                table: "sys_role_menu_lovs");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_role_menu_lovs_role_role_id",
                schema: "apps",
                table: "sys_role_menu_lovs");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_role_menus_sys_menu_menu_id",
                schema: "apps",
                table: "sys_role_menus");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_role_menus_sys_roles_role_id",
                schema: "apps",
                table: "sys_role_menus");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_role_privileges_resource_resource_id",
                schema: "apps",
                table: "sys_role_privileges");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_role_privileges_role_role_id",
                schema: "apps",
                table: "sys_role_privileges");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_roles_sys_apps_profile_app_id",
                schema: "apps",
                table: "sys_roles");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_settings_sys_apps_profile_app_id",
                schema: "apps",
                table: "sys_settings");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_ui_texts_sys_apps_profile_app_id",
                schema: "apps",
                table: "sys_ui_texts");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_user_privilege_lovs_dd_lovs_lov_id",
                schema: "apps",
                table: "sys_user_privilege_lovs");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_user_privilege_lovs_sys_user_privileges_user_privilege_",
                schema: "apps",
                table: "sys_user_privilege_lovs");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_user_privileges_sys_resources_resource_id",
                schema: "apps",
                table: "sys_user_privileges");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_user_privileges_sys_users_user_id",
                schema: "apps",
                table: "sys_user_privileges");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_user_roles_org_units_org_unit_id",
                schema: "apps",
                table: "sys_user_roles");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_user_roles_sys_roles_role_id",
                schema: "apps",
                table: "sys_user_roles");

            migrationBuilder.DropForeignKey(
                name: "fk_sys_user_roles_sys_users_user_id",
                schema: "apps",
                table: "sys_user_roles");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sys_users",
                schema: "apps",
                table: "sys_users");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sys_user_roles",
                schema: "apps",
                table: "sys_user_roles");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sys_user_privileges",
                schema: "apps",
                table: "sys_user_privileges");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sys_user_privilege_lovs",
                schema: "apps",
                table: "sys_user_privilege_lovs");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sys_ui_texts",
                schema: "apps",
                table: "sys_ui_texts");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sys_settings",
                schema: "apps",
                table: "sys_settings");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sys_roles",
                schema: "apps",
                table: "sys_roles");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sys_role_privileges",
                schema: "apps",
                table: "sys_role_privileges");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sys_role_menus",
                schema: "apps",
                table: "sys_role_menus");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sys_role_menu_lovs",
                schema: "apps",
                table: "sys_role_menu_lovs");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sys_resources",
                schema: "apps",
                table: "sys_resources");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sys_menu_lovs",
                schema: "apps",
                table: "sys_menu_lovs");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sys_menu_groups",
                schema: "apps",
                table: "sys_menu_groups");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sys_menu",
                schema: "apps",
                table: "sys_menu");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sys_instance_profile",
                schema: "apps",
                table: "sys_instance_profile");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sys_installation",
                schema: "apps",
                table: "sys_installation");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sys_company_profile",
                schema: "apps",
                table: "sys_company_profile");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sys_company_branches",
                schema: "apps",
                table: "sys_company_branches");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sys_change_areas",
                schema: "apps",
                table: "sys_change_areas");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sys_business_apps",
                schema: "apps",
                table: "sys_business_apps");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sys_branding_assets",
                schema: "apps",
                table: "sys_branding_assets");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sys_apps_profile",
                schema: "apps",
                table: "sys_apps_profile");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sys_apps_branding",
                schema: "apps",
                table: "sys_apps_branding");

            migrationBuilder.DropPrimaryKey(
                name: "pk_org_user_assignments",
                schema: "apps",
                table: "org_user_assignments");

            migrationBuilder.DropPrimaryKey(
                name: "pk_org_units",
                schema: "apps",
                table: "org_units");

            migrationBuilder.DropPrimaryKey(
                name: "pk_org_unit_types",
                schema: "apps",
                table: "org_unit_types");

            migrationBuilder.DropPrimaryKey(
                name: "pk_org_positions",
                schema: "apps",
                table: "org_positions");

            migrationBuilder.DropPrimaryKey(
                name: "pk_dd_lovs",
                schema: "apps",
                table: "dd_lovs");

            migrationBuilder.DropPrimaryKey(
                name: "pk_dd_domain",
                schema: "apps",
                table: "dd_domain");

            migrationBuilder.DropPrimaryKey(
                name: "pk_dd_data_scope_bindings",
                schema: "apps",
                table: "dd_data_scope_bindings");

            migrationBuilder.DropPrimaryKey(
                name: "pk_dd_data_objects",
                schema: "apps",
                table: "dd_data_objects");

            migrationBuilder.DropPrimaryKey(
                name: "pk_dd_classes",
                schema: "apps",
                table: "dd_classes");

            migrationBuilder.DropPrimaryKey(
                name: "pk_dd_class_attributes",
                schema: "apps",
                table: "dd_class_attributes");

            migrationBuilder.DropPrimaryKey(
                name: "pk_bas_holidays",
                schema: "apps",
                table: "bas_holidays");

            migrationBuilder.RenameTable(
                name: "sys_users",
                schema: "apps",
                newName: "user_account",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "sys_user_roles",
                schema: "apps",
                newName: "user_role",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "sys_user_privileges",
                schema: "apps",
                newName: "user_privilege",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "sys_user_privilege_lovs",
                schema: "apps",
                newName: "user_privilege_grant",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "sys_ui_texts",
                schema: "apps",
                newName: "ui_text",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "sys_settings",
                schema: "apps",
                newName: "setting",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "sys_roles",
                schema: "apps",
                newName: "role",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "sys_role_privileges",
                schema: "apps",
                newName: "privilege",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "sys_role_menus",
                schema: "apps",
                newName: "role_menu",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "sys_role_menu_lovs",
                schema: "apps",
                newName: "lov_grant",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "sys_resources",
                schema: "apps",
                newName: "resource",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "sys_menu_lovs",
                schema: "apps",
                newName: "lov_binding",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "sys_menu_groups",
                schema: "apps",
                newName: "menu_group",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "sys_menu",
                schema: "apps",
                newName: "menu",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "sys_instance_profile",
                schema: "apps",
                newName: "entity",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "sys_installation",
                schema: "apps",
                newName: "installation",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "sys_company_profile",
                schema: "apps",
                newName: "company",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "sys_company_branches",
                schema: "apps",
                newName: "branch",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "sys_change_areas",
                schema: "apps",
                newName: "change_area",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "sys_business_apps",
                schema: "apps",
                newName: "business_app",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "sys_branding_assets",
                schema: "apps",
                newName: "branding_asset",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "sys_apps_profile",
                schema: "apps",
                newName: "app",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "sys_apps_branding",
                schema: "apps",
                newName: "app_branding",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "org_user_assignments",
                schema: "apps",
                newName: "user_assignment",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "org_units",
                schema: "apps",
                newName: "org_unit",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "org_unit_types",
                schema: "apps",
                newName: "org_unit_type",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "org_positions",
                schema: "apps",
                newName: "position",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "dd_lovs",
                schema: "apps",
                newName: "lov",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "dd_domain",
                schema: "apps",
                newName: "domain",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "dd_data_scope_bindings",
                schema: "apps",
                newName: "data_scope_binding",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "dd_data_objects",
                schema: "apps",
                newName: "data_object",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "dd_classes",
                schema: "apps",
                newName: "class",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "dd_class_attributes",
                schema: "apps",
                newName: "class_attribute",
                newSchema: "apps");

            migrationBuilder.RenameTable(
                name: "bas_holidays",
                schema: "apps",
                newName: "holiday",
                newSchema: "apps");

            migrationBuilder.RenameColumn(
                name: "userphoneno",
                schema: "apps",
                table: "user_account",
                newName: "mobile");

            migrationBuilder.RenameColumn(
                name: "usernamentv",
                schema: "apps",
                table: "user_account",
                newName: "name_ar");

            migrationBuilder.RenameColumn(
                name: "usernamefrn",
                schema: "apps",
                table: "user_account",
                newName: "name_en");

            migrationBuilder.RenameColumn(
                name: "userid",
                schema: "apps",
                table: "user_account",
                newName: "code");

            migrationBuilder.RenameColumn(
                name: "useremail",
                schema: "apps",
                table: "user_account",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "last_login_date",
                schema: "apps",
                table: "user_account",
                newName: "last_login_at");

            migrationBuilder.RenameColumn(
                name: "last_cmp_num",
                schema: "apps",
                table: "user_account",
                newName: "last_company_code");

            migrationBuilder.RenameColumn(
                name: "instid",
                schema: "apps",
                table: "user_account",
                newName: "entity_code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_users_idp_subject",
                schema: "apps",
                table: "user_account",
                newName: "ix_user_account_idp_subject");

            migrationBuilder.RenameIndex(
                name: "ix_sys_users_code",
                schema: "apps",
                table: "user_account",
                newName: "ix_user_account_code");

            migrationBuilder.RenameColumn(
                name: "cmp_num",
                schema: "apps",
                table: "user_role",
                newName: "company_code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_user_roles_user_id",
                schema: "apps",
                table: "user_role",
                newName: "ix_user_role_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_sys_user_roles_role_id",
                schema: "apps",
                table: "user_role",
                newName: "ix_user_role_role_id");

            migrationBuilder.RenameIndex(
                name: "ix_sys_user_roles_org_unit_id",
                schema: "apps",
                table: "user_role",
                newName: "ix_user_role_org_unit_id");

            migrationBuilder.RenameColumn(
                name: "cmp_num",
                schema: "apps",
                table: "user_privilege",
                newName: "company_code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_user_privileges_user_id",
                schema: "apps",
                table: "user_privilege",
                newName: "ix_user_privilege_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_sys_user_privileges_resource_id",
                schema: "apps",
                table: "user_privilege",
                newName: "ix_user_privilege_resource_id");

            migrationBuilder.RenameColumn(
                name: "value_codes",
                schema: "apps",
                table: "user_privilege_grant",
                newName: "granted_values");

            migrationBuilder.RenameIndex(
                name: "ix_sys_user_privilege_lovs_user_privilege_id_lov_id",
                schema: "apps",
                table: "user_privilege_grant",
                newName: "ix_user_privilege_grant_user_privilege_id_lov_id");

            migrationBuilder.RenameIndex(
                name: "ix_sys_user_privilege_lovs_lov_id",
                schema: "apps",
                table: "user_privilege_grant",
                newName: "ix_user_privilege_grant_lov_id");

            migrationBuilder.RenameColumn(
                name: "text_desc_ntv",
                schema: "apps",
                table: "ui_text",
                newName: "name_ar");

            migrationBuilder.RenameColumn(
                name: "text_desc_frn",
                schema: "apps",
                table: "ui_text",
                newName: "name_en");

            migrationBuilder.RenameColumn(
                name: "text_code",
                schema: "apps",
                table: "ui_text",
                newName: "code");

            migrationBuilder.RenameColumn(
                name: "instid",
                schema: "apps",
                table: "ui_text",
                newName: "entity_code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_ui_texts_code_app_id",
                schema: "apps",
                table: "ui_text",
                newName: "ix_ui_text_code_app_id");

            migrationBuilder.RenameIndex(
                name: "ix_sys_ui_texts_app_id",
                schema: "apps",
                table: "ui_text",
                newName: "ix_ui_text_app_id");

            migrationBuilder.RenameColumn(
                name: "setting_long_desc",
                schema: "apps",
                table: "setting",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "setting_desc_ntv",
                schema: "apps",
                table: "setting",
                newName: "name_ar");

            migrationBuilder.RenameColumn(
                name: "setting_desc_frn",
                schema: "apps",
                table: "setting",
                newName: "name_en");

            migrationBuilder.RenameColumn(
                name: "setting_code",
                schema: "apps",
                table: "setting",
                newName: "code");

            migrationBuilder.RenameColumn(
                name: "instid",
                schema: "apps",
                table: "setting",
                newName: "entity_code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_settings_code_app_id",
                schema: "apps",
                table: "setting",
                newName: "ix_setting_code_app_id");

            migrationBuilder.RenameIndex(
                name: "ix_sys_settings_app_id",
                schema: "apps",
                table: "setting",
                newName: "ix_setting_app_id");

            migrationBuilder.RenameColumn(
                name: "role_desc_ntv",
                schema: "apps",
                table: "role",
                newName: "name_ar");

            migrationBuilder.RenameColumn(
                name: "role_desc_frn",
                schema: "apps",
                table: "role",
                newName: "name_en");

            migrationBuilder.RenameColumn(
                name: "role_code",
                schema: "apps",
                table: "role",
                newName: "code");

            migrationBuilder.RenameColumn(
                name: "ref_code",
                schema: "apps",
                table: "role",
                newName: "reference_code");

            migrationBuilder.RenameColumn(
                name: "override_cmp_nums",
                schema: "apps",
                table: "role",
                newName: "override_companies");

            migrationBuilder.RenameColumn(
                name: "instid",
                schema: "apps",
                table: "role",
                newName: "entity_code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_roles_code",
                schema: "apps",
                table: "role",
                newName: "ix_role_code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_roles_app_id",
                schema: "apps",
                table: "role",
                newName: "ix_role_app_id");

            migrationBuilder.RenameColumn(
                name: "cmp_num",
                schema: "apps",
                table: "privilege",
                newName: "company_code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_role_privileges_role_id_resource_id_company_code",
                schema: "apps",
                table: "privilege",
                newName: "ix_privilege_role_id_resource_id_company_code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_role_privileges_resource_id",
                schema: "apps",
                table: "privilege",
                newName: "ix_privilege_resource_id");

            migrationBuilder.RenameColumn(
                name: "cmp_num",
                schema: "apps",
                table: "role_menu",
                newName: "company_code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_role_menus_role_id_menu_id_company_code",
                schema: "apps",
                table: "role_menu",
                newName: "ix_role_menu_role_id_menu_id_company_code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_role_menus_menu_id",
                schema: "apps",
                table: "role_menu",
                newName: "ix_role_menu_menu_id");

            migrationBuilder.RenameColumn(
                name: "value_codes",
                schema: "apps",
                table: "lov_grant",
                newName: "granted_values");

            migrationBuilder.RenameColumn(
                name: "default_value_code",
                schema: "apps",
                table: "lov_grant",
                newName: "default_value");

            migrationBuilder.RenameColumn(
                name: "cmp_num",
                schema: "apps",
                table: "lov_grant",
                newName: "company_code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_role_menu_lovs_role_id_lov_id_resource_id_company_code",
                schema: "apps",
                table: "lov_grant",
                newName: "ix_lov_grant_role_id_lov_id_resource_id_company_code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_role_menu_lovs_resource_id",
                schema: "apps",
                table: "lov_grant",
                newName: "ix_lov_grant_resource_id");

            migrationBuilder.RenameIndex(
                name: "ix_sys_role_menu_lovs_lov_id",
                schema: "apps",
                table: "lov_grant",
                newName: "ix_lov_grant_lov_id");

            migrationBuilder.RenameColumn(
                name: "resource_desc_ntv",
                schema: "apps",
                table: "resource",
                newName: "name_ar");

            migrationBuilder.RenameColumn(
                name: "resource_desc_frn",
                schema: "apps",
                table: "resource",
                newName: "name_en");

            migrationBuilder.RenameColumn(
                name: "resource_code",
                schema: "apps",
                table: "resource",
                newName: "code");

            migrationBuilder.RenameColumn(
                name: "instid",
                schema: "apps",
                table: "resource",
                newName: "entity_code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_resources_parent_id",
                schema: "apps",
                table: "resource",
                newName: "ix_resource_parent_id");

            migrationBuilder.RenameIndex(
                name: "ix_sys_resources_code",
                schema: "apps",
                table: "resource",
                newName: "ix_resource_code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_resources_app_id",
                schema: "apps",
                table: "resource",
                newName: "ix_resource_app_id");

            migrationBuilder.RenameIndex(
                name: "ix_sys_menu_lovs_resource_id_field_code",
                schema: "apps",
                table: "lov_binding",
                newName: "ix_lov_binding_resource_id_field_code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_menu_lovs_lov_id",
                schema: "apps",
                table: "lov_binding",
                newName: "ix_lov_binding_lov_id");

            migrationBuilder.RenameColumn(
                name: "menu_icon",
                schema: "apps",
                table: "menu_group",
                newName: "icon");

            migrationBuilder.RenameColumn(
                name: "menu_group_order",
                schema: "apps",
                table: "menu_group",
                newName: "display_order");

            migrationBuilder.RenameColumn(
                name: "menu_group_des_ntv",
                schema: "apps",
                table: "menu_group",
                newName: "name_ar");

            migrationBuilder.RenameColumn(
                name: "menu_group_des_frn",
                schema: "apps",
                table: "menu_group",
                newName: "name_en");

            migrationBuilder.RenameColumn(
                name: "menu_group_code",
                schema: "apps",
                table: "menu_group",
                newName: "code");

            migrationBuilder.RenameColumn(
                name: "instid",
                schema: "apps",
                table: "menu_group",
                newName: "entity_code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_menu_groups_code",
                schema: "apps",
                table: "menu_group",
                newName: "ix_menu_group_code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_menu_groups_app_id",
                schema: "apps",
                table: "menu_group",
                newName: "ix_menu_group_app_id");

            migrationBuilder.RenameColumn(
                name: "show_flag",
                schema: "apps",
                table: "menu",
                newName: "is_visible");

            migrationBuilder.RenameColumn(
                name: "parent_menu_id",
                schema: "apps",
                table: "menu",
                newName: "parent_id");

            migrationBuilder.RenameColumn(
                name: "order_num",
                schema: "apps",
                table: "menu",
                newName: "display_order");

            migrationBuilder.RenameColumn(
                name: "menu_long_desc_ntv",
                schema: "apps",
                table: "menu",
                newName: "description_ar");

            migrationBuilder.RenameColumn(
                name: "menu_long_desc_frn",
                schema: "apps",
                table: "menu",
                newName: "description_en");

            migrationBuilder.RenameColumn(
                name: "menu_icon",
                schema: "apps",
                table: "menu",
                newName: "icon");

            migrationBuilder.RenameColumn(
                name: "menu_desc_ntv",
                schema: "apps",
                table: "menu",
                newName: "name_ar");

            migrationBuilder.RenameColumn(
                name: "menu_desc_frn",
                schema: "apps",
                table: "menu",
                newName: "name_en");

            migrationBuilder.RenameColumn(
                name: "menu_code",
                schema: "apps",
                table: "menu",
                newName: "code");

            migrationBuilder.RenameColumn(
                name: "instid",
                schema: "apps",
                table: "menu",
                newName: "entity_code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_menu_resource_id",
                schema: "apps",
                table: "menu",
                newName: "ix_menu_resource_id");

            migrationBuilder.RenameIndex(
                name: "ix_sys_menu_parent_id",
                schema: "apps",
                table: "menu",
                newName: "ix_menu_parent_id");

            migrationBuilder.RenameIndex(
                name: "ix_sys_menu_menu_group_id",
                schema: "apps",
                table: "menu",
                newName: "ix_menu_menu_group_id");

            migrationBuilder.RenameIndex(
                name: "ix_sys_menu_code",
                schema: "apps",
                table: "menu",
                newName: "ix_menu_code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_menu_app_id",
                schema: "apps",
                table: "menu",
                newName: "ix_menu_app_id");

            migrationBuilder.RenameColumn(
                name: "ref_instid",
                schema: "apps",
                table: "entity",
                newName: "entity_code");

            migrationBuilder.RenameColumn(
                name: "instname_ntv",
                schema: "apps",
                table: "entity",
                newName: "name_ar");

            migrationBuilder.RenameColumn(
                name: "instname_frn",
                schema: "apps",
                table: "entity",
                newName: "name_en");

            migrationBuilder.RenameColumn(
                name: "instlogo",
                schema: "apps",
                table: "entity",
                newName: "logo_file_key");

            migrationBuilder.RenameColumn(
                name: "instid",
                schema: "apps",
                table: "entity",
                newName: "code");

            migrationBuilder.RenameColumn(
                name: "activeflag",
                schema: "apps",
                table: "entity",
                newName: "is_active");

            migrationBuilder.RenameIndex(
                name: "ix_sys_instance_profile_code",
                schema: "apps",
                table: "entity",
                newName: "ix_entity_code");

            migrationBuilder.RenameColumn(
                name: "systemtypeid",
                schema: "apps",
                table: "installation",
                newName: "system_type");

            migrationBuilder.RenameColumn(
                name: "instid",
                schema: "apps",
                table: "installation",
                newName: "entity_code");

            migrationBuilder.RenameColumn(
                name: "install_desc_ntv",
                schema: "apps",
                table: "installation",
                newName: "name_ar");

            migrationBuilder.RenameColumn(
                name: "install_desc_frn",
                schema: "apps",
                table: "installation",
                newName: "name_en");

            migrationBuilder.RenameColumn(
                name: "install_code",
                schema: "apps",
                table: "installation",
                newName: "code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_installation_code",
                schema: "apps",
                table: "installation",
                newName: "ix_installation_code");

            migrationBuilder.RenameColumn(
                name: "order_num",
                schema: "apps",
                table: "company",
                newName: "display_order");

            migrationBuilder.RenameColumn(
                name: "instid",
                schema: "apps",
                table: "company",
                newName: "entity_code");

            migrationBuilder.RenameColumn(
                name: "cmp_num",
                schema: "apps",
                table: "company",
                newName: "code");

            migrationBuilder.RenameColumn(
                name: "cmp_des_ntv",
                schema: "apps",
                table: "company",
                newName: "name_ar");

            migrationBuilder.RenameColumn(
                name: "cmp_des_frn",
                schema: "apps",
                table: "company",
                newName: "name_en");

            migrationBuilder.RenameColumn(
                name: "active_flag",
                schema: "apps",
                table: "company",
                newName: "is_active");

            migrationBuilder.RenameIndex(
                name: "ix_sys_company_profile_code",
                schema: "apps",
                table: "company",
                newName: "ix_company_code");

            migrationBuilder.RenameColumn(
                name: "order_num",
                schema: "apps",
                table: "branch",
                newName: "display_order");

            migrationBuilder.RenameColumn(
                name: "instid",
                schema: "apps",
                table: "branch",
                newName: "entity_code");

            migrationBuilder.RenameColumn(
                name: "cmp_num",
                schema: "apps",
                table: "branch",
                newName: "company_code");

            migrationBuilder.RenameColumn(
                name: "branch_desc_ntv",
                schema: "apps",
                table: "branch",
                newName: "name_ar");

            migrationBuilder.RenameColumn(
                name: "branch_desc_frn",
                schema: "apps",
                table: "branch",
                newName: "name_en");

            migrationBuilder.RenameColumn(
                name: "branch_code",
                schema: "apps",
                table: "branch",
                newName: "code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_company_branches_company_code",
                schema: "apps",
                table: "branch",
                newName: "ix_branch_company_code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_company_branches_code",
                schema: "apps",
                table: "branch",
                newName: "ix_branch_code");

            migrationBuilder.RenameColumn(
                name: "instid",
                schema: "apps",
                table: "change_area",
                newName: "entity_code");

            migrationBuilder.RenameColumn(
                name: "area_desc_ntv",
                schema: "apps",
                table: "change_area",
                newName: "name_ar");

            migrationBuilder.RenameColumn(
                name: "area_desc_frn",
                schema: "apps",
                table: "change_area",
                newName: "name_en");

            migrationBuilder.RenameColumn(
                name: "area_code",
                schema: "apps",
                table: "change_area",
                newName: "code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_change_areas_code",
                schema: "apps",
                table: "change_area",
                newName: "ix_change_area_code");

            migrationBuilder.RenameColumn(
                name: "order_num",
                schema: "apps",
                table: "business_app",
                newName: "display_order");

            migrationBuilder.RenameColumn(
                name: "instid",
                schema: "apps",
                table: "business_app",
                newName: "entity_code");

            migrationBuilder.RenameColumn(
                name: "cmp_nums",
                schema: "apps",
                table: "business_app",
                newName: "companies");

            migrationBuilder.RenameColumn(
                name: "business_app_long_desc_ntv",
                schema: "apps",
                table: "business_app",
                newName: "description_ar");

            migrationBuilder.RenameColumn(
                name: "business_app_long_desc_frn",
                schema: "apps",
                table: "business_app",
                newName: "description_en");

            migrationBuilder.RenameColumn(
                name: "business_app_desc_ntv",
                schema: "apps",
                table: "business_app",
                newName: "name_ar");

            migrationBuilder.RenameColumn(
                name: "business_app_desc_frn",
                schema: "apps",
                table: "business_app",
                newName: "name_en");

            migrationBuilder.RenameColumn(
                name: "business_app_code",
                schema: "apps",
                table: "business_app",
                newName: "code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_business_apps_code",
                schema: "apps",
                table: "business_app",
                newName: "ix_business_app_code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_business_apps_client_id",
                schema: "apps",
                table: "business_app",
                newName: "ix_business_app_client_id");

            migrationBuilder.RenameColumn(
                name: "order_num",
                schema: "apps",
                table: "app",
                newName: "display_order");

            migrationBuilder.RenameColumn(
                name: "instid",
                schema: "apps",
                table: "app",
                newName: "entity_code");

            migrationBuilder.RenameColumn(
                name: "appsid",
                schema: "apps",
                table: "app",
                newName: "code");

            migrationBuilder.RenameColumn(
                name: "apps_des_ntv",
                schema: "apps",
                table: "app",
                newName: "name_ar");

            migrationBuilder.RenameColumn(
                name: "apps_des_frn",
                schema: "apps",
                table: "app",
                newName: "name_en");

            migrationBuilder.RenameColumn(
                name: "active_flag",
                schema: "apps",
                table: "app",
                newName: "is_active");

            migrationBuilder.RenameIndex(
                name: "ix_sys_apps_profile_code",
                schema: "apps",
                table: "app",
                newName: "ix_app_code");

            migrationBuilder.RenameColumn(
                name: "instid",
                schema: "apps",
                table: "app_branding",
                newName: "entity_code");

            migrationBuilder.RenameColumn(
                name: "branding_desc_ntv",
                schema: "apps",
                table: "app_branding",
                newName: "name_ar");

            migrationBuilder.RenameColumn(
                name: "branding_desc_frn",
                schema: "apps",
                table: "app_branding",
                newName: "name_en");

            migrationBuilder.RenameColumn(
                name: "branding_code",
                schema: "apps",
                table: "app_branding",
                newName: "code");

            migrationBuilder.RenameIndex(
                name: "ix_sys_apps_branding_code",
                schema: "apps",
                table: "app_branding",
                newName: "ix_app_branding_code");

            migrationBuilder.RenameIndex(
                name: "ix_org_user_assignments_user_id",
                schema: "apps",
                table: "user_assignment",
                newName: "ix_user_assignment_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_org_user_assignments_position_id",
                schema: "apps",
                table: "user_assignment",
                newName: "ix_user_assignment_position_id");

            migrationBuilder.RenameIndex(
                name: "ix_org_user_assignments_org_unit_id",
                schema: "apps",
                table: "user_assignment",
                newName: "ix_user_assignment_org_unit_id");

            migrationBuilder.RenameColumn(
                name: "unit_desc_ntv",
                schema: "apps",
                table: "org_unit",
                newName: "name_ar");

            migrationBuilder.RenameColumn(
                name: "unit_desc_frn",
                schema: "apps",
                table: "org_unit",
                newName: "name_en");

            migrationBuilder.RenameColumn(
                name: "unit_code",
                schema: "apps",
                table: "org_unit",
                newName: "code");

            migrationBuilder.RenameColumn(
                name: "parent_unit_id",
                schema: "apps",
                table: "org_unit",
                newName: "parent_id");

            migrationBuilder.RenameColumn(
                name: "instid",
                schema: "apps",
                table: "org_unit",
                newName: "entity_code");

            migrationBuilder.RenameIndex(
                name: "ix_org_units_path",
                schema: "apps",
                table: "org_unit",
                newName: "ix_org_unit_path");

            migrationBuilder.RenameIndex(
                name: "ix_org_units_parent_id",
                schema: "apps",
                table: "org_unit",
                newName: "ix_org_unit_parent_id");

            migrationBuilder.RenameIndex(
                name: "ix_org_units_org_unit_type_id",
                schema: "apps",
                table: "org_unit",
                newName: "ix_org_unit_org_unit_type_id");

            migrationBuilder.RenameIndex(
                name: "ix_org_units_code",
                schema: "apps",
                table: "org_unit",
                newName: "ix_org_unit_code");

            migrationBuilder.RenameColumn(
                name: "unit_type_desc_ntv",
                schema: "apps",
                table: "org_unit_type",
                newName: "name_ar");

            migrationBuilder.RenameColumn(
                name: "unit_type_desc_frn",
                schema: "apps",
                table: "org_unit_type",
                newName: "name_en");

            migrationBuilder.RenameColumn(
                name: "unit_type_code",
                schema: "apps",
                table: "org_unit_type",
                newName: "code");

            migrationBuilder.RenameColumn(
                name: "order_num",
                schema: "apps",
                table: "org_unit_type",
                newName: "display_order");

            migrationBuilder.RenameColumn(
                name: "instid",
                schema: "apps",
                table: "org_unit_type",
                newName: "entity_code");

            migrationBuilder.RenameIndex(
                name: "ix_org_unit_types_code",
                schema: "apps",
                table: "org_unit_type",
                newName: "ix_org_unit_type_code");

            migrationBuilder.RenameColumn(
                name: "position_desc_ntv",
                schema: "apps",
                table: "position",
                newName: "name_ar");

            migrationBuilder.RenameColumn(
                name: "position_desc_frn",
                schema: "apps",
                table: "position",
                newName: "name_en");

            migrationBuilder.RenameColumn(
                name: "position_code",
                schema: "apps",
                table: "position",
                newName: "code");

            migrationBuilder.RenameColumn(
                name: "instid",
                schema: "apps",
                table: "position",
                newName: "entity_code");

            migrationBuilder.RenameIndex(
                name: "ix_org_positions_code",
                schema: "apps",
                table: "position",
                newName: "ix_position_code");

            migrationBuilder.RenameColumn(
                name: "value_field",
                schema: "apps",
                table: "lov",
                newName: "value_column");

            migrationBuilder.RenameColumn(
                name: "sqlstmt",
                schema: "apps",
                table: "lov",
                newName: "sql_text");

            migrationBuilder.RenameColumn(
                name: "ref_class_id",
                schema: "apps",
                table: "lov",
                newName: "class_id");

            migrationBuilder.RenameColumn(
                name: "lov_desc_ntv",
                schema: "apps",
                table: "lov",
                newName: "name_ar");

            migrationBuilder.RenameColumn(
                name: "lov_desc_frn",
                schema: "apps",
                table: "lov",
                newName: "name_en");

            migrationBuilder.RenameColumn(
                name: "lov_code",
                schema: "apps",
                table: "lov",
                newName: "code");

            migrationBuilder.RenameColumn(
                name: "instid",
                schema: "apps",
                table: "lov",
                newName: "entity_code");

            migrationBuilder.RenameIndex(
                name: "ix_dd_lovs_data_object_id",
                schema: "apps",
                table: "lov",
                newName: "ix_lov_data_object_id");

            migrationBuilder.RenameIndex(
                name: "ix_dd_lovs_code",
                schema: "apps",
                table: "lov",
                newName: "ix_lov_code");

            migrationBuilder.RenameIndex(
                name: "ix_dd_lovs_class_id",
                schema: "apps",
                table: "lov",
                newName: "ix_lov_class_id");

            migrationBuilder.RenameColumn(
                name: "value",
                schema: "apps",
                table: "domain",
                newName: "code");

            migrationBuilder.RenameColumn(
                name: "orderby",
                schema: "apps",
                table: "domain",
                newName: "display_order");

            migrationBuilder.RenameColumn(
                name: "instid",
                schema: "apps",
                table: "domain",
                newName: "entity_code");

            migrationBuilder.RenameColumn(
                name: "domain_desc_ntv",
                schema: "apps",
                table: "domain",
                newName: "name_ar");

            migrationBuilder.RenameColumn(
                name: "domain_desc_frn",
                schema: "apps",
                table: "domain",
                newName: "name_en");

            migrationBuilder.RenameColumn(
                name: "domain_desc_detail_ntv",
                schema: "apps",
                table: "domain",
                newName: "details_ar");

            migrationBuilder.RenameColumn(
                name: "domain_desc_detail_frn",
                schema: "apps",
                table: "domain",
                newName: "details_en");

            migrationBuilder.RenameColumn(
                name: "cmp_nums",
                schema: "apps",
                table: "domain",
                newName: "companies");

            migrationBuilder.RenameColumn(
                name: "active_flag",
                schema: "apps",
                table: "domain",
                newName: "is_active");

            migrationBuilder.RenameIndex(
                name: "ix_dd_domain_parent_domain_id",
                schema: "apps",
                table: "domain",
                newName: "ix_domain_parent_domain_id");

            migrationBuilder.RenameIndex(
                name: "ix_dd_domain_class_id_code",
                schema: "apps",
                table: "domain",
                newName: "ix_domain_class_id_code");

            migrationBuilder.RenameIndex(
                name: "ix_dd_domain_attributes",
                schema: "apps",
                table: "domain",
                newName: "ix_domain_attributes");

            migrationBuilder.RenameColumn(
                name: "value_field",
                schema: "apps",
                table: "data_scope_binding",
                newName: "column_name");

            migrationBuilder.RenameIndex(
                name: "ix_dd_data_scope_bindings_lov_id",
                schema: "apps",
                table: "data_scope_binding",
                newName: "ix_data_scope_binding_lov_id");

            migrationBuilder.RenameIndex(
                name: "ix_dd_data_scope_bindings_data_object_id",
                schema: "apps",
                table: "data_scope_binding",
                newName: "ix_data_scope_binding_data_object_id");

            migrationBuilder.RenameColumn(
                name: "object_desc_ntv",
                schema: "apps",
                table: "data_object",
                newName: "name_ar");

            migrationBuilder.RenameColumn(
                name: "object_desc_frn",
                schema: "apps",
                table: "data_object",
                newName: "name_en");

            migrationBuilder.RenameColumn(
                name: "object_code",
                schema: "apps",
                table: "data_object",
                newName: "code");

            migrationBuilder.RenameColumn(
                name: "instid",
                schema: "apps",
                table: "data_object",
                newName: "entity_code");

            migrationBuilder.RenameIndex(
                name: "ix_dd_data_objects_code",
                schema: "apps",
                table: "data_object",
                newName: "ix_data_object_code");

            migrationBuilder.RenameColumn(
                name: "virtual_enable_multiple_assign",
                schema: "apps",
                table: "class",
                newName: "allow_multi_assign");

            migrationBuilder.RenameColumn(
                name: "reference_class_id",
                schema: "apps",
                table: "class",
                newName: "parent_class_id");

            migrationBuilder.RenameColumn(
                name: "instid",
                schema: "apps",
                table: "class",
                newName: "entity_code");

            migrationBuilder.RenameColumn(
                name: "class_long_desc_ntv",
                schema: "apps",
                table: "class",
                newName: "description_ar");

            migrationBuilder.RenameColumn(
                name: "class_long_desc_frn",
                schema: "apps",
                table: "class",
                newName: "description_en");

            migrationBuilder.RenameColumn(
                name: "class_desc_ntv",
                schema: "apps",
                table: "class",
                newName: "name_ar");

            migrationBuilder.RenameColumn(
                name: "class_desc_frn",
                schema: "apps",
                table: "class",
                newName: "name_en");

            migrationBuilder.RenameColumn(
                name: "class_code",
                schema: "apps",
                table: "class",
                newName: "code");

            migrationBuilder.RenameIndex(
                name: "ix_dd_classes_parent_class_id",
                schema: "apps",
                table: "class",
                newName: "ix_class_parent_class_id");

            migrationBuilder.RenameIndex(
                name: "ix_dd_classes_code",
                schema: "apps",
                table: "class",
                newName: "ix_class_code");

            migrationBuilder.RenameColumn(
                name: "order_num",
                schema: "apps",
                table: "class_attribute",
                newName: "display_order");

            migrationBuilder.RenameColumn(
                name: "attribute_type",
                schema: "apps",
                table: "class_attribute",
                newName: "data_type");

            migrationBuilder.RenameColumn(
                name: "attribute_label_ntv",
                schema: "apps",
                table: "class_attribute",
                newName: "name_ar");

            migrationBuilder.RenameColumn(
                name: "attribute_label_frn",
                schema: "apps",
                table: "class_attribute",
                newName: "name_en");

            migrationBuilder.RenameColumn(
                name: "attribute_code",
                schema: "apps",
                table: "class_attribute",
                newName: "code");

            migrationBuilder.RenameIndex(
                name: "ix_dd_class_attributes_class_id_code",
                schema: "apps",
                table: "class_attribute",
                newName: "ix_class_attribute_class_id_code");

            migrationBuilder.RenameColumn(
                name: "instid",
                schema: "apps",
                table: "holiday",
                newName: "entity_code");

            migrationBuilder.RenameColumn(
                name: "holiday_desc_ntv",
                schema: "apps",
                table: "holiday",
                newName: "name_ar");

            migrationBuilder.RenameColumn(
                name: "holiday_desc_frn",
                schema: "apps",
                table: "holiday",
                newName: "name_en");

            migrationBuilder.RenameColumn(
                name: "holiday_code",
                schema: "apps",
                table: "holiday",
                newName: "code");

            migrationBuilder.RenameIndex(
                name: "ix_bas_holidays_code",
                schema: "apps",
                table: "holiday",
                newName: "ix_holiday_code");

            migrationBuilder.AddPrimaryKey(
                name: "pk_user_account",
                schema: "apps",
                table: "user_account",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_user_role",
                schema: "apps",
                table: "user_role",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_user_privilege",
                schema: "apps",
                table: "user_privilege",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_user_privilege_grant",
                schema: "apps",
                table: "user_privilege_grant",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_ui_text",
                schema: "apps",
                table: "ui_text",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_setting",
                schema: "apps",
                table: "setting",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_role",
                schema: "apps",
                table: "role",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_privilege",
                schema: "apps",
                table: "privilege",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_role_menu",
                schema: "apps",
                table: "role_menu",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_lov_grant",
                schema: "apps",
                table: "lov_grant",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_resource",
                schema: "apps",
                table: "resource",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_lov_binding",
                schema: "apps",
                table: "lov_binding",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_menu_group",
                schema: "apps",
                table: "menu_group",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_menu",
                schema: "apps",
                table: "menu",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_entity",
                schema: "apps",
                table: "entity",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_installation",
                schema: "apps",
                table: "installation",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_company",
                schema: "apps",
                table: "company",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_branch",
                schema: "apps",
                table: "branch",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_change_area",
                schema: "apps",
                table: "change_area",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_business_app",
                schema: "apps",
                table: "business_app",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_branding_asset",
                schema: "apps",
                table: "branding_asset",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_app",
                schema: "apps",
                table: "app",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_app_branding",
                schema: "apps",
                table: "app_branding",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_user_assignment",
                schema: "apps",
                table: "user_assignment",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_org_unit",
                schema: "apps",
                table: "org_unit",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_org_unit_type",
                schema: "apps",
                table: "org_unit_type",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_position",
                schema: "apps",
                table: "position",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_lov",
                schema: "apps",
                table: "lov",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_domain",
                schema: "apps",
                table: "domain",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_data_scope_binding",
                schema: "apps",
                table: "data_scope_binding",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_data_object",
                schema: "apps",
                table: "data_object",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_class",
                schema: "apps",
                table: "class",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_class_attribute",
                schema: "apps",
                table: "class_attribute",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_holiday",
                schema: "apps",
                table: "holiday",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_class_class_parent_class_id",
                schema: "apps",
                table: "class",
                column: "parent_class_id",
                principalSchema: "apps",
                principalTable: "class",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_class_attribute_class_class_id",
                schema: "apps",
                table: "class_attribute",
                column: "class_id",
                principalSchema: "apps",
                principalTable: "class",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_data_scope_binding_data_object_data_object_id",
                schema: "apps",
                table: "data_scope_binding",
                column: "data_object_id",
                principalSchema: "apps",
                principalTable: "data_object",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_data_scope_binding_lovs_lov_id",
                schema: "apps",
                table: "data_scope_binding",
                column: "lov_id",
                principalSchema: "apps",
                principalTable: "lov",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_domain_class_class_id",
                schema: "apps",
                table: "domain",
                column: "class_id",
                principalSchema: "apps",
                principalTable: "class",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_domain_domain_parent_domain_id",
                schema: "apps",
                table: "domain",
                column: "parent_domain_id",
                principalSchema: "apps",
                principalTable: "domain",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_lov_class_class_id",
                schema: "apps",
                table: "lov",
                column: "class_id",
                principalSchema: "apps",
                principalTable: "class",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_lov_data_object_data_object_id",
                schema: "apps",
                table: "lov",
                column: "data_object_id",
                principalSchema: "apps",
                principalTable: "data_object",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_lov_binding_lov_lov_id",
                schema: "apps",
                table: "lov_binding",
                column: "lov_id",
                principalSchema: "apps",
                principalTable: "lov",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_lov_binding_resource_resource_id",
                schema: "apps",
                table: "lov_binding",
                column: "resource_id",
                principalSchema: "apps",
                principalTable: "resource",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_lov_grant_lov_lov_id",
                schema: "apps",
                table: "lov_grant",
                column: "lov_id",
                principalSchema: "apps",
                principalTable: "lov",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_lov_grant_resource_resource_id",
                schema: "apps",
                table: "lov_grant",
                column: "resource_id",
                principalSchema: "apps",
                principalTable: "resource",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_lov_grant_role_role_id",
                schema: "apps",
                table: "lov_grant",
                column: "role_id",
                principalSchema: "apps",
                principalTable: "role",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_menu_app_app_id",
                schema: "apps",
                table: "menu",
                column: "app_id",
                principalSchema: "apps",
                principalTable: "app",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_menu_menu_group_menu_group_id",
                schema: "apps",
                table: "menu",
                column: "menu_group_id",
                principalSchema: "apps",
                principalTable: "menu_group",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_menu_menu_parent_id",
                schema: "apps",
                table: "menu",
                column: "parent_id",
                principalSchema: "apps",
                principalTable: "menu",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_menu_resource_resource_id",
                schema: "apps",
                table: "menu",
                column: "resource_id",
                principalSchema: "apps",
                principalTable: "resource",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_menu_group_app_app_id",
                schema: "apps",
                table: "menu_group",
                column: "app_id",
                principalSchema: "apps",
                principalTable: "app",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_org_unit_org_unit_parent_id",
                schema: "apps",
                table: "org_unit",
                column: "parent_id",
                principalSchema: "apps",
                principalTable: "org_unit",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_org_unit_org_unit_type_org_unit_type_id",
                schema: "apps",
                table: "org_unit",
                column: "org_unit_type_id",
                principalSchema: "apps",
                principalTable: "org_unit_type",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_privilege_resource_resource_id",
                schema: "apps",
                table: "privilege",
                column: "resource_id",
                principalSchema: "apps",
                principalTable: "resource",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_privilege_role_role_id",
                schema: "apps",
                table: "privilege",
                column: "role_id",
                principalSchema: "apps",
                principalTable: "role",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_resource_app_app_id",
                schema: "apps",
                table: "resource",
                column: "app_id",
                principalSchema: "apps",
                principalTable: "app",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_resource_resource_parent_id",
                schema: "apps",
                table: "resource",
                column: "parent_id",
                principalSchema: "apps",
                principalTable: "resource",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_role_app_app_id",
                schema: "apps",
                table: "role",
                column: "app_id",
                principalSchema: "apps",
                principalTable: "app",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_role_menu_menus_menu_id",
                schema: "apps",
                table: "role_menu",
                column: "menu_id",
                principalSchema: "apps",
                principalTable: "menu",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_role_menu_role_role_id",
                schema: "apps",
                table: "role_menu",
                column: "role_id",
                principalSchema: "apps",
                principalTable: "role",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_setting_app_app_id",
                schema: "apps",
                table: "setting",
                column: "app_id",
                principalSchema: "apps",
                principalTable: "app",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_ui_text_app_app_id",
                schema: "apps",
                table: "ui_text",
                column: "app_id",
                principalSchema: "apps",
                principalTable: "app",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_user_assignment_org_unit_org_unit_id",
                schema: "apps",
                table: "user_assignment",
                column: "org_unit_id",
                principalSchema: "apps",
                principalTable: "org_unit",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_user_assignment_position_position_id",
                schema: "apps",
                table: "user_assignment",
                column: "position_id",
                principalSchema: "apps",
                principalTable: "position",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_user_assignment_user_account_user_id",
                schema: "apps",
                table: "user_assignment",
                column: "user_id",
                principalSchema: "apps",
                principalTable: "user_account",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_user_privilege_resources_resource_id",
                schema: "apps",
                table: "user_privilege",
                column: "resource_id",
                principalSchema: "apps",
                principalTable: "resource",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_user_privilege_user_account_user_id",
                schema: "apps",
                table: "user_privilege",
                column: "user_id",
                principalSchema: "apps",
                principalTable: "user_account",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_user_privilege_grant_lovs_lov_id",
                schema: "apps",
                table: "user_privilege_grant",
                column: "lov_id",
                principalSchema: "apps",
                principalTable: "lov",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_user_privilege_grant_user_privileges_user_privilege_id",
                schema: "apps",
                table: "user_privilege_grant",
                column: "user_privilege_id",
                principalSchema: "apps",
                principalTable: "user_privilege",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_user_role_org_unit_org_unit_id",
                schema: "apps",
                table: "user_role",
                column: "org_unit_id",
                principalSchema: "apps",
                principalTable: "org_unit",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_user_role_roles_role_id",
                schema: "apps",
                table: "user_role",
                column: "role_id",
                principalSchema: "apps",
                principalTable: "role",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_user_role_user_account_user_id",
                schema: "apps",
                table: "user_role",
                column: "user_id",
                principalSchema: "apps",
                principalTable: "user_account",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
