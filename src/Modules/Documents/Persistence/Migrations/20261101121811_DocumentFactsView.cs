using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Documents.Persistence.Migrations
{
    /// <summary>
    /// A read-only view for reports (ADR-0045): each document's current version as one row per simple value of its data
    /// (e.g. <c>grade</c>, <c>specialty</c>), with the document's type, number, status, dates, company and branch, so a
    /// report can group certificates by any of their values without code.
    /// </summary>
    public partial class DocumentFactsView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE VIEW doc.v_document_fact AS
                SELECT d.id AS document_id,
                       d.type_code,
                       d.number,
                       d.status,
                       d.holder_name_ar,
                       d.holder_name_en,
                       d.issued_at,
                       d.expires_at,
                       d.company_code,
                       d.branch_code,
                       f.key AS fact,
                       f.value #>> '{}' AS value
                FROM doc.document d
                JOIN doc.version v ON v.document_id = d.id AND v.version = d.current_version
                CROSS JOIN LATERAL jsonb_each(CASE WHEN jsonb_typeof(v.data) = 'object' THEN v.data ELSE '{}'::jsonb END) f
                WHERE jsonb_typeof(f.value) IN ('string', 'number', 'boolean');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS doc.v_document_fact;");
        }
    }
}
