namespace DynamicCube.Modules.AppsControl.Persistence.Migrations;

/// <summary>
/// Data moved to the legacy keys by <see cref="LegacyCompositeKeys"/>: a company is named <c>instid/cmp_num</c> wherever it
/// is referred to (users' roles and privileges, roles, apps, and the company columns of requests, tasks and documents), and
/// a class value of one company holds that company in its own columns instead of a list.
/// </summary>
internal static class LegacyKeysDataFix
{
    /// <summary>Before the columns change: every company has an instance.</summary>
    public const string BeforeUp = """
        UPDATE apps.sys_company_profile
        SET instid = (SELECT instid FROM apps.sys_instance_profile ORDER BY created_at, instid LIMIT 1)
        WHERE instid IS NULL;
        """;

    /// <summary>After <c>dd_domain.cmp_num</c> is added and before <c>cmp_nums</c> is dropped.</summary>
    public const string Up = """
        -- The class views read the old column; they are recreated at start-up (seeding).
        DO $views$
        DECLARE v record;
        BEGIN
          FOR v IN SELECT viewname FROM pg_views WHERE schemaname = 'apps' AND viewname LIKE 'v\_class\_%' LOOP
            EXECUTE format('DROP VIEW apps.%I', v.viewname);
          END LOOP;
        END $views$;

        -- A value of one company keeps that company; values of several or all companies serve every company.
        UPDATE apps.dd_domain d SET cmp_num = c.cmp_num, instid = c.instid
        FROM apps.sys_company_profile c
        WHERE cardinality(d.cmp_nums) = 1 AND d.cmp_nums[1] = c.cmp_num;

        -- Company references become instid/cmp_num.
        CREATE TEMP TABLE legacy_company_keys ON COMMIT DROP AS
          SELECT cmp_num AS old_code, instid || '/' || cmp_num AS new_code FROM apps.sys_company_profile;

        UPDATE apps.sys_user_roles r SET cmp_num = k.new_code FROM legacy_company_keys k WHERE r.cmp_num = k.old_code;
        UPDATE apps.sys_user_privileges r SET cmp_num = k.new_code FROM legacy_company_keys k WHERE r.cmp_num = k.old_code;
        UPDATE apps.sys_role_menus r SET cmp_num = k.new_code FROM legacy_company_keys k WHERE r.cmp_num = k.old_code;
        UPDATE apps.sys_role_privileges r SET cmp_num = k.new_code FROM legacy_company_keys k WHERE r.cmp_num = k.old_code;
        UPDATE apps.sys_role_menu_lovs r SET cmp_num = k.new_code FROM legacy_company_keys k WHERE r.cmp_num = k.old_code;
        UPDATE apps.sys_users r SET last_cmp_num = k.new_code FROM legacy_company_keys k WHERE r.last_cmp_num = k.old_code;
        UPDATE apps.sys_roles SET override_cmp_nums = ARRAY(
          SELECT coalesce(k.new_code, x) FROM unnest(override_cmp_nums) AS x LEFT JOIN legacy_company_keys k ON k.old_code = x)
        WHERE cardinality(override_cmp_nums) > 0;
        UPDATE apps.sys_business_apps SET cmp_nums = ARRAY(
          SELECT coalesce(k.new_code, x) FROM unnest(cmp_nums) AS x LEFT JOIN legacy_company_keys k ON k.old_code = x)
        WHERE cardinality(cmp_nums) > 0;

        -- The company columns of the other modules (requests, tasks, documents…).
        DO $do$
        DECLARE c record;
        BEGIN
          FOR c IN
            SELECT table_schema, table_name FROM information_schema.columns
            WHERE column_name = 'company_code' AND data_type IN ('text', 'character varying')
              AND table_schema NOT IN ('apps', 'cfg', 'audit', 'platform', 'pg_catalog', 'information_schema')
              AND (table_schema, table_name) IN (SELECT table_schema, table_name FROM information_schema.tables WHERE table_type = 'BASE TABLE')
          LOOP
            EXECUTE format('UPDATE %I.%I t SET company_code = k.new_code FROM legacy_company_keys k WHERE t.company_code = k.old_code', c.table_schema, c.table_name);
          END LOOP;
        END $do$;
        """;

    /// <summary>Back to plain company codes (before <c>cmp_nums</c> is added back… and filled here).</summary>
    public const string Down = """
        -- The class views read the old column; they are recreated at start-up (seeding).
        DO $views$
        DECLARE v record;
        BEGIN
          FOR v IN SELECT viewname FROM pg_views WHERE schemaname = 'apps' AND viewname LIKE 'v\_class\_%' LOOP
            EXECUTE format('DROP VIEW apps.%I', v.viewname);
          END LOOP;
        END $views$;

        UPDATE apps.dd_domain SET cmp_nums = CASE WHEN cmp_num IS NULL THEN '{}'::text[] ELSE ARRAY[cmp_num] END;
        UPDATE apps.sys_user_roles SET cmp_num = split_part(cmp_num, '/', 2) WHERE cmp_num LIKE '%/%';
        UPDATE apps.sys_user_privileges SET cmp_num = split_part(cmp_num, '/', 2) WHERE cmp_num LIKE '%/%';
        UPDATE apps.sys_role_menus SET cmp_num = split_part(cmp_num, '/', 2) WHERE cmp_num LIKE '%/%';
        UPDATE apps.sys_role_privileges SET cmp_num = split_part(cmp_num, '/', 2) WHERE cmp_num LIKE '%/%';
        UPDATE apps.sys_role_menu_lovs SET cmp_num = split_part(cmp_num, '/', 2) WHERE cmp_num LIKE '%/%';
        UPDATE apps.sys_users SET last_cmp_num = split_part(last_cmp_num, '/', 2) WHERE last_cmp_num LIKE '%/%';
        UPDATE apps.sys_roles SET override_cmp_nums = ARRAY(SELECT CASE WHEN x LIKE '%/%' THEN split_part(x, '/', 2) ELSE x END FROM unnest(override_cmp_nums) AS x);
        UPDATE apps.sys_business_apps SET cmp_nums = ARRAY(SELECT CASE WHEN x LIKE '%/%' THEN split_part(x, '/', 2) ELSE x END FROM unnest(cmp_nums) AS x);
        DO $do$
        DECLARE c record;
        BEGIN
          FOR c IN
            SELECT table_schema, table_name FROM information_schema.columns
            WHERE column_name = 'company_code' AND data_type IN ('text', 'character varying')
              AND table_schema NOT IN ('apps', 'cfg', 'audit', 'platform', 'pg_catalog', 'information_schema')
              AND (table_schema, table_name) IN (SELECT table_schema, table_name FROM information_schema.tables WHERE table_type = 'BASE TABLE')
          LOOP
            EXECUTE format('UPDATE %I.%I SET company_code = split_part(company_code, ''/'', 2) WHERE company_code LIKE ''%%/%%''', c.table_schema, c.table_name);
          END LOOP;
        END $do$;
        """;
}
