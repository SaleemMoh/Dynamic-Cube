using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Documents.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DocumentCompanyBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "branch_code",
                schema: "doc",
                table: "document",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "company_code",
                schema: "doc",
                table: "document",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // Documents issued before branches applied (ADR-0036 step 6) belong to the default company and its default branch.
            migrationBuilder.Sql("""
                DO $$
                DECLARE company text; branch text;
                BEGIN
                  IF to_regclass('apps.company') IS NOT NULL AND to_regclass('apps.branch') IS NOT NULL THEN
                    SELECT c.code INTO company FROM apps.company c WHERE c.is_active ORDER BY c.display_order, c.code LIMIT 1;
                    SELECT b.code INTO branch FROM apps.branch b WHERE b.company_code = company AND b.is_active
                      ORDER BY b.is_default DESC, b.display_order, b.code LIMIT 1;
                    IF company IS NOT NULL THEN
                      UPDATE doc.document SET company_code = company, branch_code = branch WHERE company_code IS NULL;
                    END IF;
                  END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "branch_code",
                schema: "doc",
                table: "document");

            migrationBuilder.DropColumn(
                name: "company_code",
                schema: "doc",
                table: "document");
        }
    }
}
