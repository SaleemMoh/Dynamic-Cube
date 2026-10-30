using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.AppsControl.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AppKindsAndChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing business apps become Dynamic Cube apps on the internal system and the portal, for every company.
            migrationBuilder.AddColumn<string>(
                name: "base_url",
                schema: "apps",
                table: "business_app",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "branches",
                schema: "apps",
                table: "business_app",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "channels",
                schema: "apps",
                table: "business_app",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{INTERNAL,EXTERNAL}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "companies",
                schema: "apps",
                table: "business_app",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<string>(
                name: "kind",
                schema: "apps",
                table: "business_app",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "DynamicCube");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "base_url",
                schema: "apps",
                table: "business_app");

            migrationBuilder.DropColumn(
                name: "branches",
                schema: "apps",
                table: "business_app");

            migrationBuilder.DropColumn(
                name: "channels",
                schema: "apps",
                table: "business_app");

            migrationBuilder.DropColumn(
                name: "companies",
                schema: "apps",
                table: "business_app");

            migrationBuilder.DropColumn(
                name: "kind",
                schema: "apps",
                table: "business_app");
        }
    }
}
