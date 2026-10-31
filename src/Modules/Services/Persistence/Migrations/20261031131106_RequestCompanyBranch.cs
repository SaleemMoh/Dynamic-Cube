using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Services.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequestCompanyBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "branch_question",
                schema: "svc",
                table: "service",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "company_code",
                schema: "svc",
                table: "service",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "branch_code",
                schema: "svc",
                table: "request",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "company_code",
                schema: "svc",
                table: "request",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_request_company_code_branch_code",
                schema: "svc",
                table: "request",
                columns: new[] { "company_code", "branch_code" });

            // Requests made before branches applied (ADR-0036 step 6) belong to the default company and its default branch.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                  IF to_regclass('apps.company') IS NOT NULL AND to_regclass('apps.branch') IS NOT NULL THEN
                    UPDATE svc.request r SET company_code = d.company, branch_code = d.branch
                    FROM (
                      SELECT c.code AS company,
                             (SELECT b.code FROM apps.branch b WHERE b.company_code = c.code AND b.is_active
                              ORDER BY b.is_default DESC, b.display_order, b.code LIMIT 1) AS branch
                      FROM apps.company c WHERE c.is_active ORDER BY c.display_order, c.code LIMIT 1
                    ) d
                    WHERE r.company_code IS NULL;
                  END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_request_company_code_branch_code",
                schema: "svc",
                table: "request");

            migrationBuilder.DropColumn(
                name: "branch_question",
                schema: "svc",
                table: "service");

            migrationBuilder.DropColumn(
                name: "company_code",
                schema: "svc",
                table: "service");

            migrationBuilder.DropColumn(
                name: "branch_code",
                schema: "svc",
                table: "request");

            migrationBuilder.DropColumn(
                name: "company_code",
                schema: "svc",
                table: "request");
        }
    }
}
