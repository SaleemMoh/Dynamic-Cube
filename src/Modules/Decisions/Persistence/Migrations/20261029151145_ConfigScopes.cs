using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Decisions.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfigScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "dec",
                table: "rule_set_version",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "dec",
                table: "rule_set_version",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "dec",
                table: "rule_set",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "dec",
                table: "rule_set",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "dec",
                table: "rule_set_version");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "dec",
                table: "rule_set_version");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "dec",
                table: "rule_set");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "dec",
                table: "rule_set");
        }
    }
}
