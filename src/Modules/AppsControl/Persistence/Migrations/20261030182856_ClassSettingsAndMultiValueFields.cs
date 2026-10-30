using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.AppsControl.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClassSettingsAndMultiValueFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_multiple",
                schema: "apps",
                table: "class_attribute",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ref_lov_code",
                schema: "apps",
                table: "class_attribute",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "settings",
                schema: "apps",
                table: "class",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_multiple",
                schema: "apps",
                table: "class_attribute");

            migrationBuilder.DropColumn(
                name: "ref_lov_code",
                schema: "apps",
                table: "class_attribute");

            migrationBuilder.DropColumn(
                name: "settings",
                schema: "apps",
                table: "class");
        }
    }
}
