using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.AppsControl.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SharedApps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "apps",
                table: "user_account",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "apps",
                table: "ui_text",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "apps",
                table: "setting",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "apps",
                table: "role",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "apps",
                table: "resource",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "apps",
                table: "position",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "apps",
                table: "org_unit_type",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "apps",
                table: "org_unit",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "apps",
                table: "menu_group",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "apps",
                table: "menu",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "apps",
                table: "lov",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "apps",
                table: "installation",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "apps",
                table: "holiday",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "apps",
                table: "entity",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "apps",
                table: "domain",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "apps",
                table: "data_object",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "apps",
                table: "class",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "apps",
                table: "change_area",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "apps",
                table: "business_app",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "apps",
                table: "app",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "apps",
                table: "user_account");

            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "apps",
                table: "ui_text");

            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "apps",
                table: "setting");

            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "apps",
                table: "role");

            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "apps",
                table: "resource");

            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "apps",
                table: "position");

            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "apps",
                table: "org_unit_type");

            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "apps",
                table: "org_unit");

            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "apps",
                table: "menu_group");

            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "apps",
                table: "menu");

            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "apps",
                table: "lov");

            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "apps",
                table: "installation");

            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "apps",
                table: "holiday");

            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "apps",
                table: "entity");

            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "apps",
                table: "domain");

            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "apps",
                table: "data_object");

            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "apps",
                table: "class");

            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "apps",
                table: "change_area");

            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "apps",
                table: "business_app");

            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "apps",
                table: "app");
        }
    }
}
