using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Services.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequestItemResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "item_results",
                schema: "svc",
                table: "request",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "item_results",
                schema: "svc",
                table: "request");
        }
    }
}
