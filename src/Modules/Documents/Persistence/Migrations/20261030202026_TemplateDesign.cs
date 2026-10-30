using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Documents.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TemplateDesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "design",
                schema: "doc",
                table: "template",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "design",
                schema: "doc",
                table: "template");
        }
    }
}
