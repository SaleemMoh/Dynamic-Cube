using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.AppsControl.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccessByCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "branches",
                schema: "apps",
                table: "user_role",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<string>(
                name: "company_code",
                schema: "apps",
                table: "user_role",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "default_roles",
                schema: "apps",
                table: "org_unit",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.CreateTable(
                name: "user_privilege",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actions = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_privilege", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_privilege_resources_resource_id",
                        column: x => x.resource_id,
                        principalSchema: "apps",
                        principalTable: "resource",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_privilege_user_account_user_id",
                        column: x => x.user_id,
                        principalSchema: "apps",
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_privilege_resource_id",
                schema: "apps",
                table: "user_privilege",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_privilege_user_id",
                schema: "apps",
                table: "user_privilege",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_privilege",
                schema: "apps");

            migrationBuilder.DropColumn(
                name: "branches",
                schema: "apps",
                table: "user_role");

            migrationBuilder.DropColumn(
                name: "company_code",
                schema: "apps",
                table: "user_role");

            migrationBuilder.DropColumn(
                name: "default_roles",
                schema: "apps",
                table: "org_unit");
        }
    }
}
