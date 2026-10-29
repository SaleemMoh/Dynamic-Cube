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
        migrationBuilder.Sql($"""
            DO $$
            BEGIN
              IF to_regclass('apps.class') IS NULL OR to_regclass('{sourceTable}') IS NULL THEN
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
