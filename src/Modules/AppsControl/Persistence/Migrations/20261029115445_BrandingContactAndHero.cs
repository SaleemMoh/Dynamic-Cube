using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.AppsControl.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BrandingContactAndHero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "contact",
                schema: "apps",
                table: "entity",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "portal_hero_image_key",
                schema: "apps",
                table: "entity",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "contact",
                schema: "apps",
                table: "entity");

            migrationBuilder.DropColumn(
                name: "portal_hero_image_key",
                schema: "apps",
                table: "entity");
        }
    }
}
