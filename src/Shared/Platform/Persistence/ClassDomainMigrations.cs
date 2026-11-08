using Microsoft.EntityFrameworkCore.Migrations;

namespace DynamicCube.Platform.Persistence;

/// <summary>Moves the rows of a module's table into values of a class (Class/Domain, schema <c>apps</c>) in a migration.</summary>
public static class ClassDomainMigrations
{
    /// <summary>
    /// Copies every row of <paramref name="sourceTable"/> into class <paramref name="classCode"/> (created SYSTEM-owned
    /// when missing; the seeder adds its attributes), keeping ids, codes, names, state, level and audit columns. The
    /// attributes come from <paramref name="attributesSql"/>, a <c>jsonb</c> expression over the source row <c>s</c>;
    /// <paramref name="displayOrderSql"/> gives the order. Does nothing before Apps Control's tables exist (a new
    /// database has nothing to move).
    /// </summary>
    public static void MoveToClass(
        this MigrationBuilder migrationBuilder, string sourceTable, string classCode, string classNameAr, string classNameEn, string attributesSql, string displayOrderSql = "0")
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        // Apps Control's tables carry the legacy names (dd_classes, dd_domain…) since they were renamed; a database
        // upgraded from an older version may still run this before that rename, so either set of names is used.
        migrationBuilder.Sql($"""
            DO $$
            BEGIN
              IF to_regclass('{sourceTable}') IS NULL THEN
                RETURN;
              END IF;
              IF to_regclass('apps.dd_classes') IS NOT NULL THEN
                INSERT INTO apps.dd_classes (id, class_code, class_desc_ntv, class_desc_frn, is_active, owner, is_hierarchical, virtual_enable_multiple_assign, created_at, created_by)
                SELECT gen_random_uuid(), '{classCode}', '{classNameAr}', '{classNameEn}', true, 'System', false, false, now(), 'migration'
                WHERE NOT EXISTS (SELECT 1 FROM apps.dd_classes WHERE class_code = '{classCode}');
                INSERT INTO apps.dd_domain (id, class_id, orderby, is_default, attributes, value, domain_desc_ntv, domain_desc_frn, active_flag, owner,
                                            created_at, created_by, updated_at, updated_by, instid, business_app)
                SELECT s.id, k.id, {displayOrderSql}, false, jsonb_strip_nulls({attributesSql}), s.code, s.name_ar, s.name_en, s.is_active, s.owner,
                       s.created_at, s.created_by, s.updated_at, s.updated_by, s.entity_code, s.business_app
                FROM {sourceTable} s CROSS JOIN apps.dd_classes k
                WHERE k.class_code = '{classCode}'
                ON CONFLICT (class_id, value) DO NOTHING;
                RETURN;
              END IF;
              IF to_regclass('apps.class') IS NULL THEN
                RETURN;
              END IF;
              INSERT INTO apps.class (id, code, name_ar, name_en, is_active, owner, is_hierarchical, allow_multi_assign, created_at, created_by)
              SELECT gen_random_uuid(), '{classCode}', '{classNameAr}', '{classNameEn}', true, 'System', false, false, now(), 'migration'
              WHERE NOT EXISTS (SELECT 1 FROM apps.class WHERE code = '{classCode}');
              INSERT INTO apps.domain (id, class_id, display_order, is_default, attributes, code, name_ar, name_en, is_active, owner,
                                       created_at, created_by, updated_at, updated_by, entity_code, business_app)
              SELECT s.id, k.id, {displayOrderSql}, false, jsonb_strip_nulls({attributesSql}), s.code, s.name_ar, s.name_en, s.is_active, s.owner,
                     s.created_at, s.created_by, s.updated_at, s.updated_by, s.entity_code, s.business_app
              FROM {sourceTable} s CROSS JOIN apps.class k
              WHERE k.code = '{classCode}'
              ON CONFLICT (class_id, code) DO NOTHING;
            END $$;
            """);
    }
}
