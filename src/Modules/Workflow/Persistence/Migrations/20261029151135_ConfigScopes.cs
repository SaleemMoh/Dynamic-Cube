using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Workflow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfigScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "wf",
                table: "workflow_version",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "wf",
                table: "workflow_version",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "wf",
                table: "workflow",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "wf",
                table: "workflow",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "wf",
                table: "workflow_version");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "wf",
                table: "workflow_version");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "wf",
                table: "workflow");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "wf",
                table: "workflow");
        }
    }
}
