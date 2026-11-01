using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Integration.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Soap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "protocol",
                schema: "integ",
                table: "operation",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Rest");

            migrationBuilder.AddColumn<string>(
                name: "soap",
                schema: "integ",
                table: "operation",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "password_digest",
                schema: "integ",
                table: "connection",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "protocol",
                schema: "integ",
                table: "operation");

            migrationBuilder.DropColumn(
                name: "soap",
                schema: "integ",
                table: "operation");

            migrationBuilder.DropColumn(
                name: "password_digest",
                schema: "integ",
                table: "connection");
        }
    }
}
