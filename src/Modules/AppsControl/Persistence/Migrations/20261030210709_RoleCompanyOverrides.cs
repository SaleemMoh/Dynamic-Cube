using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.AppsControl.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoleCompanyOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_role_menu_role_id_menu_id",
                schema: "apps",
                table: "role_menu");

            migrationBuilder.DropIndex(
                name: "ix_privilege_role_id_resource_id",
                schema: "apps",
                table: "privilege");

            migrationBuilder.DropIndex(
                name: "ix_lov_grant_role_id_lov_id_resource_id",
                schema: "apps",
                table: "lov_grant");

            migrationBuilder.AddColumn<string>(
                name: "company_code",
                schema: "apps",
                table: "role_menu",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "override_companies",
                schema: "apps",
                table: "role",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<string>(
                name: "company_code",
                schema: "apps",
                table: "privilege",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "company_code",
                schema: "apps",
                table: "lov_grant",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_menu_role_id_menu_id_company_code",
                schema: "apps",
                table: "role_menu",
                columns: new[] { "role_id", "menu_id", "company_code" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_privilege_role_id_resource_id_company_code",
                schema: "apps",
                table: "privilege",
                columns: new[] { "role_id", "resource_id", "company_code" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_lov_grant_role_id_lov_id_resource_id_company_code",
                schema: "apps",
                table: "lov_grant",
                columns: new[] { "role_id", "lov_id", "resource_id", "company_code" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_role_menu_role_id_menu_id_company_code",
                schema: "apps",
                table: "role_menu");

            migrationBuilder.DropIndex(
                name: "ix_privilege_role_id_resource_id_company_code",
                schema: "apps",
                table: "privilege");

            migrationBuilder.DropIndex(
                name: "ix_lov_grant_role_id_lov_id_resource_id_company_code",
                schema: "apps",
                table: "lov_grant");

            migrationBuilder.DropColumn(
                name: "company_code",
                schema: "apps",
                table: "role_menu");

            migrationBuilder.DropColumn(
                name: "override_companies",
                schema: "apps",
                table: "role");

            migrationBuilder.DropColumn(
                name: "company_code",
                schema: "apps",
                table: "privilege");

            migrationBuilder.DropColumn(
                name: "company_code",
                schema: "apps",
                table: "lov_grant");

            migrationBuilder.CreateIndex(
                name: "ix_role_menu_role_id_menu_id",
                schema: "apps",
                table: "role_menu",
                columns: new[] { "role_id", "menu_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_privilege_role_id_resource_id",
                schema: "apps",
                table: "privilege",
                columns: new[] { "role_id", "resource_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lov_grant_role_id_lov_id_resource_id",
                schema: "apps",
                table: "lov_grant",
                columns: new[] { "role_id", "lov_id", "resource_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }
    }
}
