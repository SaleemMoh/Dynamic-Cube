using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Services.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfigScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "svc",
                table: "service_version",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "svc",
                table: "service_version",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "svc",
                table: "service",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "svc",
                table: "service",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "svc",
                table: "question",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "svc",
                table: "question",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "svc",
                table: "category",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "svc",
                table: "category",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "svc",
                table: "service_version");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "svc",
                table: "service_version");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "svc",
                table: "service");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "svc",
                table: "service");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "svc",
                table: "question");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "svc",
                table: "question");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "svc",
                table: "category");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "svc",
                table: "category");
        }
    }
}
