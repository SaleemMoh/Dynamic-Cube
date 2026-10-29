using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Documents.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SharedApps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "doc",
                table: "template",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "doc",
                table: "document_type",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "doc",
                table: "template");

            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "doc",
                table: "document_type");
        }
    }
}
