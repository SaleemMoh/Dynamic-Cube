using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.AppsControl.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExplicitAllAndPersonalGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "All" becomes an explicit choice (ADR-0036): rows that left companies or branches empty meant all of them.
            migrationBuilder.Sql("UPDATE apps.user_role SET company_code = '*' WHERE company_code IS NULL OR company_code = '';");
            migrationBuilder.Sql("UPDATE apps.user_role SET branches = ARRAY['*'] WHERE cardinality(branches) = 0 OR company_code = '*';");
            migrationBuilder.Sql("UPDATE apps.user_privilege SET company_code = '*' WHERE company_code IS NULL OR company_code = '';");
            migrationBuilder.Sql("UPDATE apps.business_app SET companies = ARRAY['*'] WHERE cardinality(companies) = 0;");
            migrationBuilder.Sql("UPDATE apps.business_app SET branches = ARRAY['*'] WHERE cardinality(branches) = 0;");
            migrationBuilder.Sql("UPDATE apps.domain d SET companies = ARRAY['*'] FROM apps.class c WHERE d.class_id = c.id AND c.values_per_company AND cardinality(d.companies) = 0;");

            migrationBuilder.AlterColumn<string>(
                name: "company_code",
                schema: "apps",
                table: "user_role",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "company_code",
                schema: "apps",
                table: "user_privilege",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "user_privilege_grant",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_privilege_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lov_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grant_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    granted_values = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_privilege_grant", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_privilege_grant_lovs_lov_id",
                        column: x => x.lov_id,
                        principalSchema: "apps",
                        principalTable: "lov",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_privilege_grant_user_privileges_user_privilege_id",
                        column: x => x.user_privilege_id,
                        principalSchema: "apps",
                        principalTable: "user_privilege",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_privilege_grant_lov_id",
                schema: "apps",
                table: "user_privilege_grant",
                column: "lov_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_privilege_grant_user_privilege_id_lov_id",
                schema: "apps",
                table: "user_privilege_grant",
                columns: new[] { "user_privilege_id", "lov_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_privilege_grant",
                schema: "apps");

            migrationBuilder.Sql("UPDATE apps.domain SET companies = '{}' WHERE companies = ARRAY['*'];");
            migrationBuilder.Sql("UPDATE apps.business_app SET companies = '{}' WHERE companies = ARRAY['*'];");
            migrationBuilder.Sql("UPDATE apps.business_app SET branches = '{}' WHERE branches = ARRAY['*'];");
            migrationBuilder.Sql("UPDATE apps.user_role SET branches = '{}' WHERE branches = ARRAY['*'];");

            migrationBuilder.AlterColumn<string>(
                name: "company_code",
                schema: "apps",
                table: "user_role",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "company_code",
                schema: "apps",
                table: "user_privilege",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.Sql("UPDATE apps.user_role SET company_code = NULL WHERE company_code = '*';");
            migrationBuilder.Sql("UPDATE apps.user_privilege SET company_code = NULL WHERE company_code = '*';");
        }
    }
}
