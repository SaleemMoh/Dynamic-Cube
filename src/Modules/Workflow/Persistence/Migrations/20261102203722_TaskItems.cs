using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Workflow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaskItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "item_decisions",
                schema: "wf",
                table: "task",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "item_decisions",
                schema: "wf",
                table: "task");
        }
    }
}
