using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Decisions.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SharedApps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "dec",
                table: "rule_set_version",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "dec",
                table: "rule_set",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "dec",
                table: "rule_set_version");

            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "dec",
                table: "rule_set");
        }
    }
}
