using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Integration.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfigScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "integ",
                table: "provider",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "integ",
                table: "provider",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "integ",
                table: "operation",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "integ",
                table: "operation",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "integ",
                table: "envelope_profile",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "integ",
                table: "envelope_profile",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "integ",
                table: "connection",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "integ",
                table: "connection",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "integ",
                table: "provider");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "integ",
                table: "provider");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "integ",
                table: "operation");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "integ",
                table: "operation");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "integ",
                table: "envelope_profile");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "integ",
                table: "envelope_profile");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "integ",
                table: "connection");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "integ",
                table: "connection");
        }
    }
}
