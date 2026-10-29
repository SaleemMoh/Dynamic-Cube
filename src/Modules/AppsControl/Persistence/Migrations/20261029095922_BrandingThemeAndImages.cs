using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.AppsControl.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BrandingThemeAndImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "footer_image_key",
                schema: "apps",
                table: "entity",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "internal_theme_color",
                schema: "apps",
                table: "entity",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "login_image_key",
                schema: "apps",
                table: "entity",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "portal_theme_color",
                schema: "apps",
                table: "entity",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "footer_image_key",
                schema: "apps",
                table: "entity");

            migrationBuilder.DropColumn(
                name: "internal_theme_color",
                schema: "apps",
                table: "entity");

            migrationBuilder.DropColumn(
                name: "login_image_key",
                schema: "apps",
                table: "entity");

            migrationBuilder.DropColumn(
                name: "portal_theme_color",
                schema: "apps",
                table: "entity");
        }
    }
}
