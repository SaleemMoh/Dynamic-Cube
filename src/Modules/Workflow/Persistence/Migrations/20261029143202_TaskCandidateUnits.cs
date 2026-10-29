using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Workflow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaskCandidateUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "candidate_org_units",
                schema: "wf",
                table: "task",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            // Open and finished tasks routed to a unit keep matching it.
            migrationBuilder.Sql("UPDATE wf.task SET candidate_org_units = ARRAY[candidate_org_unit] WHERE candidate_org_unit IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "candidate_org_units",
                schema: "wf",
                table: "task");
        }
    }
}
