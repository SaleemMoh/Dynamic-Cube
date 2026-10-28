using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Workflow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ServiceOwnedWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "service_code",
                schema: "wf",
                table: "workflow",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_service_code",
                schema: "wf",
                table: "workflow",
                column: "service_code",
                unique: true,
                filter: "service_code IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_workflow_service_code",
                schema: "wf",
                table: "workflow");

            migrationBuilder.DropColumn(
                name: "service_code",
                schema: "wf",
                table: "workflow");
        }
    }
}
