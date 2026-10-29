using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Payments.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfigScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "pay",
                table: "provider",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "pay",
                table: "provider",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "pay",
                table: "provider");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "pay",
                table: "provider");
        }
    }
}
