using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.AppsControl.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BusinessAppsAndScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "apps",
                table: "user_account",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "apps",
                table: "user_account",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "apps",
                table: "ui_text",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "apps",
                table: "ui_text",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "apps",
                table: "setting",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "apps",
                table: "setting",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "apps",
                table: "role",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "apps",
                table: "role",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "apps",
                table: "resource",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "apps",
                table: "resource",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "apps",
                table: "position",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "apps",
                table: "position",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "apps",
                table: "org_unit_type",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "apps",
                table: "org_unit_type",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "apps",
                table: "org_unit",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "apps",
                table: "org_unit",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "apps",
                table: "menu_group",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "apps",
                table: "menu_group",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "apps",
                table: "menu",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "apps",
                table: "menu",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "apps",
                table: "lov",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "apps",
                table: "lov",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "apps",
                table: "installation",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "apps",
                table: "installation",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "apps",
                table: "holiday",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "apps",
                table: "holiday",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "apps",
                table: "entity",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "apps",
                table: "entity",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "apps",
                table: "domain",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "apps",
                table: "domain",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "apps",
                table: "data_object",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "apps",
                table: "data_object",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "apps",
                table: "class",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "apps",
                table: "class",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "apps",
                table: "change_area",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "apps",
                table: "change_area",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "apps",
                table: "app",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "apps",
                table: "app",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "business_app",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    icon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description_ar = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    description_en = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    entity_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    business_app = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_business_app", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_business_app_code",
                schema: "apps",
                table: "business_app",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_app",
                schema: "apps");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "apps",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "apps",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "apps",
                table: "ui_text");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "apps",
                table: "ui_text");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "apps",
                table: "setting");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "apps",
                table: "setting");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "apps",
                table: "role");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "apps",
                table: "role");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "apps",
                table: "resource");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "apps",
                table: "resource");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "apps",
                table: "position");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "apps",
                table: "position");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "apps",
                table: "org_unit_type");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "apps",
                table: "org_unit_type");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "apps",
                table: "org_unit");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "apps",
                table: "org_unit");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "apps",
                table: "menu_group");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "apps",
                table: "menu_group");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "apps",
                table: "menu");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "apps",
                table: "menu");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "apps",
                table: "lov");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "apps",
                table: "lov");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "apps",
                table: "installation");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "apps",
                table: "installation");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "apps",
                table: "holiday");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "apps",
                table: "holiday");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "apps",
                table: "entity");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "apps",
                table: "entity");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "apps",
                table: "domain");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "apps",
                table: "domain");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "apps",
                table: "data_object");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "apps",
                table: "data_object");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "apps",
                table: "class");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "apps",
                table: "class");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "apps",
                table: "change_area");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "apps",
                table: "change_area");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "apps",
                table: "app");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "apps",
                table: "app");
        }
    }
}
