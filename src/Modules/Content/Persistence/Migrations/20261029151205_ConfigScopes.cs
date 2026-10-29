using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Content.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfigScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "cms",
                table: "page",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "cms",
                table: "page",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "cms",
                table: "faq",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "cms",
                table: "faq",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "cms",
                table: "page");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "cms",
                table: "page");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "cms",
                table: "faq");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "cms",
                table: "faq");
        }
    }
}
