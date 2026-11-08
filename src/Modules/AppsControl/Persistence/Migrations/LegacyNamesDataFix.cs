// The two lists below are the renames of the LegacyTableAndColumnNames migration (keep them in step with it).
namespace DynamicCube.Modules.AppsControl.Persistence.Migrations;

/// <summary>
/// Data that names Apps Control tables or columns by hand, moved with the renames of
/// <see cref="LegacyTableAndColumnNames"/>: data objects registered on a renamed table (their table and columns), the lists
/// of values, scope bindings, reports, dashboards and API endpoints that read those data objects (columns written
/// <c>alias.column</c> in their queries).
/// </summary>
internal static class LegacyNamesDataFix
{
    /// <summary>Old table name → new table name.</summary>
    private static readonly (string Old, string New)[] Tables =
    [
        ("holiday", "bas_holidays"),
        ("class_attribute", "dd_class_attributes"),
        ("class", "dd_classes"),
        ("data_object", "dd_data_objects"),
        ("data_scope_binding", "dd_data_scope_bindings"),
        ("domain", "dd_domain"),
        ("lov", "dd_lovs"),
        ("position", "org_positions"),
        ("org_unit_type", "org_unit_types"),
        ("org_unit", "org_units"),
        ("user_assignment", "org_user_assignments"),
        ("app_branding", "sys_apps_branding"),
        ("app", "sys_apps_profile"),
        ("branding_asset", "sys_branding_assets"),
        ("business_app", "sys_business_apps"),
        ("change_area", "sys_change_areas"),
        ("branch", "sys_company_branches"),
        ("company", "sys_company_profile"),
        ("installation", "sys_installation"),
        ("entity", "sys_instance_profile"),
        ("menu", "sys_menu"),
        ("menu_group", "sys_menu_groups"),
        ("lov_binding", "sys_menu_lovs"),
        ("resource", "sys_resources"),
        ("lov_grant", "sys_role_menu_lovs"),
        ("role_menu", "sys_role_menus"),
        ("privilege", "sys_role_privileges"),
        ("role", "sys_roles"),
        ("setting", "sys_settings"),
        ("ui_text", "sys_ui_texts"),
        ("user_privilege_grant", "sys_user_privilege_lovs"),
        ("user_privilege", "sys_user_privileges"),
        ("user_role", "sys_user_roles"),
        ("user_account", "sys_users"),
    ];

    /// <summary>New table name → its renamed columns (old → new).</summary>
    private static readonly (string Table, string Old, string New)[] Columns =
    [
        ("bas_holidays", "code", "holiday_code"),
        ("bas_holidays", "entity_code", "instid"),
        ("bas_holidays", "name_ar", "holiday_desc_ntv"),
        ("bas_holidays", "name_en", "holiday_desc_frn"),
        ("dd_class_attributes", "code", "attribute_code"),
        ("dd_class_attributes", "data_type", "attribute_type"),
        ("dd_class_attributes", "display_order", "order_num"),
        ("dd_class_attributes", "name_ar", "attribute_label_ntv"),
        ("dd_class_attributes", "name_en", "attribute_label_frn"),
        ("dd_classes", "allow_multi_assign", "virtual_enable_multiple_assign"),
        ("dd_classes", "code", "class_code"),
        ("dd_classes", "description_ar", "class_long_desc_ntv"),
        ("dd_classes", "description_en", "class_long_desc_frn"),
        ("dd_classes", "entity_code", "instid"),
        ("dd_classes", "name_ar", "class_desc_ntv"),
        ("dd_classes", "name_en", "class_desc_frn"),
        ("dd_classes", "parent_class_id", "reference_class_id"),
        ("dd_data_objects", "code", "object_code"),
        ("dd_data_objects", "entity_code", "instid"),
        ("dd_data_objects", "name_ar", "object_desc_ntv"),
        ("dd_data_objects", "name_en", "object_desc_frn"),
        ("dd_data_scope_bindings", "column_name", "value_field"),
        ("dd_domain", "code", "value"),
        ("dd_domain", "companies", "cmp_nums"),
        ("dd_domain", "details_ar", "domain_desc_detail_ntv"),
        ("dd_domain", "details_en", "domain_desc_detail_frn"),
        ("dd_domain", "display_order", "orderby"),
        ("dd_domain", "entity_code", "instid"),
        ("dd_domain", "is_active", "active_flag"),
        ("dd_domain", "name_ar", "domain_desc_ntv"),
        ("dd_domain", "name_en", "domain_desc_frn"),
        ("dd_lovs", "class_id", "ref_class_id"),
        ("dd_lovs", "code", "lov_code"),
        ("dd_lovs", "entity_code", "instid"),
        ("dd_lovs", "name_ar", "lov_desc_ntv"),
        ("dd_lovs", "name_en", "lov_desc_frn"),
        ("dd_lovs", "sql_text", "sqlstmt"),
        ("dd_lovs", "value_column", "value_field"),
        ("org_positions", "code", "position_code"),
        ("org_positions", "entity_code", "instid"),
        ("org_positions", "name_ar", "position_desc_ntv"),
        ("org_positions", "name_en", "position_desc_frn"),
        ("org_unit_types", "code", "unit_type_code"),
        ("org_unit_types", "display_order", "order_num"),
        ("org_unit_types", "entity_code", "instid"),
        ("org_unit_types", "name_ar", "unit_type_desc_ntv"),
        ("org_unit_types", "name_en", "unit_type_desc_frn"),
        ("org_units", "code", "unit_code"),
        ("org_units", "entity_code", "instid"),
        ("org_units", "name_ar", "unit_desc_ntv"),
        ("org_units", "name_en", "unit_desc_frn"),
        ("org_units", "parent_id", "parent_unit_id"),
        ("sys_apps_branding", "code", "branding_code"),
        ("sys_apps_branding", "entity_code", "instid"),
        ("sys_apps_branding", "name_ar", "branding_desc_ntv"),
        ("sys_apps_branding", "name_en", "branding_desc_frn"),
        ("sys_apps_profile", "code", "appsid"),
        ("sys_apps_profile", "display_order", "order_num"),
        ("sys_apps_profile", "entity_code", "instid"),
        ("sys_apps_profile", "is_active", "active_flag"),
        ("sys_apps_profile", "name_ar", "apps_des_ntv"),
        ("sys_apps_profile", "name_en", "apps_des_frn"),
        ("sys_business_apps", "code", "business_app_code"),
        ("sys_business_apps", "companies", "cmp_nums"),
        ("sys_business_apps", "description_ar", "business_app_long_desc_ntv"),
        ("sys_business_apps", "description_en", "business_app_long_desc_frn"),
        ("sys_business_apps", "display_order", "order_num"),
        ("sys_business_apps", "entity_code", "instid"),
        ("sys_business_apps", "name_ar", "business_app_desc_ntv"),
        ("sys_business_apps", "name_en", "business_app_desc_frn"),
        ("sys_change_areas", "code", "area_code"),
        ("sys_change_areas", "entity_code", "instid"),
        ("sys_change_areas", "name_ar", "area_desc_ntv"),
        ("sys_change_areas", "name_en", "area_desc_frn"),
        ("sys_company_branches", "code", "branch_code"),
        ("sys_company_branches", "company_code", "cmp_num"),
        ("sys_company_branches", "display_order", "order_num"),
        ("sys_company_branches", "entity_code", "instid"),
        ("sys_company_branches", "name_ar", "branch_desc_ntv"),
        ("sys_company_branches", "name_en", "branch_desc_frn"),
        ("sys_company_profile", "code", "cmp_num"),
        ("sys_company_profile", "display_order", "order_num"),
        ("sys_company_profile", "entity_code", "instid"),
        ("sys_company_profile", "is_active", "active_flag"),
        ("sys_company_profile", "name_ar", "cmp_des_ntv"),
        ("sys_company_profile", "name_en", "cmp_des_frn"),
        ("sys_installation", "code", "install_code"),
        ("sys_installation", "entity_code", "instid"),
        ("sys_installation", "name_ar", "install_desc_ntv"),
        ("sys_installation", "name_en", "install_desc_frn"),
        ("sys_installation", "system_type", "systemtypeid"),
        ("sys_instance_profile", "code", "instid"),
        ("sys_instance_profile", "entity_code", "ref_instid"),
        ("sys_instance_profile", "is_active", "activeflag"),
        ("sys_instance_profile", "logo_file_key", "instlogo"),
        ("sys_instance_profile", "name_ar", "instname_ntv"),
        ("sys_instance_profile", "name_en", "instname_frn"),
        ("sys_menu", "code", "menu_code"),
        ("sys_menu", "description_ar", "menu_long_desc_ntv"),
        ("sys_menu", "description_en", "menu_long_desc_frn"),
        ("sys_menu", "display_order", "order_num"),
        ("sys_menu", "entity_code", "instid"),
        ("sys_menu", "icon", "menu_icon"),
        ("sys_menu", "is_visible", "show_flag"),
        ("sys_menu", "name_ar", "menu_desc_ntv"),
        ("sys_menu", "name_en", "menu_desc_frn"),
        ("sys_menu", "parent_id", "parent_menu_id"),
        ("sys_menu_groups", "code", "menu_group_code"),
        ("sys_menu_groups", "display_order", "menu_group_order"),
        ("sys_menu_groups", "entity_code", "instid"),
        ("sys_menu_groups", "icon", "menu_icon"),
        ("sys_menu_groups", "name_ar", "menu_group_des_ntv"),
        ("sys_menu_groups", "name_en", "menu_group_des_frn"),
        ("sys_resources", "code", "resource_code"),
        ("sys_resources", "entity_code", "instid"),
        ("sys_resources", "name_ar", "resource_desc_ntv"),
        ("sys_resources", "name_en", "resource_desc_frn"),
        ("sys_role_menu_lovs", "company_code", "cmp_num"),
        ("sys_role_menu_lovs", "default_value", "default_value_code"),
        ("sys_role_menu_lovs", "granted_values", "value_codes"),
        ("sys_role_menus", "company_code", "cmp_num"),
        ("sys_role_privileges", "company_code", "cmp_num"),
        ("sys_roles", "code", "role_code"),
        ("sys_roles", "entity_code", "instid"),
        ("sys_roles", "name_ar", "role_desc_ntv"),
        ("sys_roles", "name_en", "role_desc_frn"),
        ("sys_roles", "override_companies", "override_cmp_nums"),
        ("sys_roles", "reference_code", "ref_code"),
        ("sys_settings", "code", "setting_code"),
        ("sys_settings", "description", "setting_long_desc"),
        ("sys_settings", "entity_code", "instid"),
        ("sys_settings", "name_ar", "setting_desc_ntv"),
        ("sys_settings", "name_en", "setting_desc_frn"),
        ("sys_ui_texts", "code", "text_code"),
        ("sys_ui_texts", "entity_code", "instid"),
        ("sys_ui_texts", "name_ar", "text_desc_ntv"),
        ("sys_ui_texts", "name_en", "text_desc_frn"),
        ("sys_user_privilege_lovs", "granted_values", "value_codes"),
        ("sys_user_privileges", "company_code", "cmp_num"),
        ("sys_user_roles", "company_code", "cmp_num"),
        ("sys_users", "code", "userid"),
        ("sys_users", "email", "useremail"),
        ("sys_users", "entity_code", "instid"),
        ("sys_users", "last_company_code", "last_cmp_num"),
        ("sys_users", "last_login_at", "last_login_date"),
        ("sys_users", "mobile", "userphoneno"),
        ("sys_users", "name_ar", "usernamentv"),
        ("sys_users", "name_en", "usernamefrn"),
    ];

    /// <summary>
    /// Columns written <c>alias.column</c> after a query that reads data objects: the table, the query column that names
    /// the data objects and their aliases, and the columns to update (the query itself among them).
    /// </summary>
    private static readonly (string Table, string Query, string[] Targets)[] Queries =
    [
        ("apps.dd_lovs", "query", ["query"]),
        ("rpt.report", "query", ["columns", "filters", "tiles", "sort", "charts", "details", "query"]),
        ("integ.api_endpoint", "query", ["fields", "query"]),
    ];

    /// <summary>The statements moving the data to the new names (<paramref name="up"/>) or back to the old ones.</summary>
    public static string Sql(bool up)
    {
        var tables = Tables.Select(t => up ? t : (Old: t.New, New: t.Old));
        var oldTable = Tables.ToDictionary(t => t.New, t => t.Old, StringComparer.Ordinal);
        var columns = Columns.Select(c => up ? c : (Table: oldTable[c.Table], Old: c.New, New: c.Old));

        var sql = new System.Text.StringBuilder();
        sql.AppendLine("DROP TABLE IF EXISTS pg_temp.legacy_tables, pg_temp.legacy_columns, pg_temp.legacy_objects;");
        sql.AppendLine("CREATE TEMP TABLE legacy_tables (old_name text PRIMARY KEY, new_name text NOT NULL);");
        sql.Append("INSERT INTO legacy_tables VALUES ")
            .AppendJoin(", ", tables.Select(t => $"('{t.Old}', '{t.New}')"))
            .AppendLine(";");
        sql.AppendLine("CREATE TEMP TABLE legacy_columns (table_name text NOT NULL, old_name text NOT NULL, new_name text NOT NULL, PRIMARY KEY (table_name, old_name));");
        sql.Append("INSERT INTO legacy_columns VALUES ")
            .AppendJoin(", ", columns.Select(c => $"('{c.Table}', '{c.Old}', '{c.New}')"))
            .AppendLine(";");
        sql.AppendLine(
            """
            CREATE TEMP TABLE legacy_objects AS
              SELECT o.id, o.object_code AS code, t.new_name AS table_name
              FROM apps.dd_data_objects o JOIN legacy_tables t ON t.old_name = o.db_object
              WHERE o.db_schema = 'apps';

            UPDATE apps.dd_data_objects d SET
              db_object = o.table_name,
              columns = (SELECT COALESCE(jsonb_agg(CASE WHEN c.new_name IS NULL THEN e.item ELSE jsonb_set(e.item, '{name}', to_jsonb(c.new_name)) END ORDER BY e.n), '[]'::jsonb)
                         FROM jsonb_array_elements(d.columns) WITH ORDINALITY AS e(item, n)
                         LEFT JOIN legacy_columns c ON c.table_name = o.table_name AND c.old_name = e.item->>'name')
            FROM legacy_objects o WHERE d.id = o.id;

            UPDATE apps.dd_lovs l SET
              value_field = COALESCE((SELECT c.new_name FROM legacy_columns c WHERE c.table_name = o.table_name AND c.old_name = l.value_field), l.value_field),
              display_ar_column = COALESCE((SELECT c.new_name FROM legacy_columns c WHERE c.table_name = o.table_name AND c.old_name = l.display_ar_column), l.display_ar_column),
              display_en_column = COALESCE((SELECT c.new_name FROM legacy_columns c WHERE c.table_name = o.table_name AND c.old_name = l.display_en_column), l.display_en_column),
              parent_column = COALESCE((SELECT c.new_name FROM legacy_columns c WHERE c.table_name = o.table_name AND c.old_name = l.parent_column), l.parent_column)
            FROM legacy_objects o WHERE l.data_object_id = o.id;

            UPDATE apps.dd_data_scope_bindings b SET
              value_field = COALESCE((SELECT c.new_name FROM legacy_columns c WHERE c.table_name = o.table_name AND c.old_name = b.value_field), b.value_field)
            FROM legacy_objects o WHERE b.data_object_id = o.id;

            CREATE FUNCTION pg_temp.legacy_rename(scope jsonb, doc jsonb) RETURNS jsonb LANGUAGE plpgsql AS $fn$
            DECLARE r record; t text;
            BEGIN
              IF scope IS NULL OR doc IS NULL THEN RETURN doc; END IF;
              t := doc::text;
              FOR r IN
                SELECT DISTINCT s.item->>'alias' AS alias, c.old_name, c.new_name
                FROM jsonb_path_query(scope, 'lax $.**') AS s(item)
                JOIN legacy_objects o ON o.code = s.item->>'code'
                JOIN legacy_columns c ON c.table_name = o.table_name
                WHERE jsonb_typeof(s.item) = 'object' AND s.item->>'kind' = 'DATA_OBJECT' AND coalesce(s.item->>'alias', '') <> ''
              LOOP
                -- The marker keeps a new name from being renamed again by a later pair; it is taken out at the end.
                t := replace(t, '"' || r.alias || '.' || r.old_name || '"', '"' || r.alias || '.' || chr(1) || r.new_name || '"');
              END LOOP;
              RETURN replace(t, chr(1), '')::jsonb;
            END $fn$;
            """);

        foreach (var (table, query, targets) in Queries)
        {
            var parts = table.Split('.');
            sql.AppendLine(
                $$"""
                DO $do$
                DECLARE sets text;
                BEGIN
                  IF to_regclass('{{table}}') IS NULL THEN RETURN; END IF;
                  SELECT string_agg(format('%1$I = pg_temp.legacy_rename({{query}}, %1$I)', column_name), ', ') INTO sets
                  FROM information_schema.columns
                  WHERE table_schema = '{{parts[0]}}' AND table_name = '{{parts[1]}}' AND column_name IN ({{string.Join(", ", targets.Select(c => $"'{c}'"))}});
                  IF sets IS NOT NULL THEN
                    EXECUTE format('UPDATE {{table}} SET %s WHERE {{query}}::text LIKE %L', sets, '%DATA_OBJECT%');
                  END IF;
                END $do$;
                """);
        }

        sql.AppendLine("DROP FUNCTION pg_temp.legacy_rename(jsonb, jsonb);");
        sql.AppendLine("DROP TABLE pg_temp.legacy_tables, pg_temp.legacy_columns, pg_temp.legacy_objects;");
        return sql.ToString();
    }
}
