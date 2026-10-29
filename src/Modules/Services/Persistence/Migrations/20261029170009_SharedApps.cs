using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Services.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SharedApps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "svc",
                table: "service_version",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "svc",
                table: "service",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "shared_apps",
                schema: "svc",
                table: "question",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "svc",
                table: "service_version");

            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "svc",
                table: "service");

            migrationBuilder.DropColumn(
                name: "shared_apps",
                schema: "svc",
                table: "question");
        }
    }
}
