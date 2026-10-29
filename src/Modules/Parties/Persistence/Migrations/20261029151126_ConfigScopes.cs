using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Parties.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfigScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "party",
                table: "sanction_type",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "party",
                table: "sanction_type",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "party",
                table: "relationship_type",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "party",
                table: "relationship_type",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "party",
                table: "profile_field",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "party",
                table: "profile_field",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "party",
                table: "credential_type",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "party",
                table: "credential_type",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_app",
                schema: "party",
                table: "context_source",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_code",
                schema: "party",
                table: "context_source",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "party",
                table: "sanction_type");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "party",
                table: "sanction_type");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "party",
                table: "relationship_type");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "party",
                table: "relationship_type");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "party",
                table: "profile_field");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "party",
                table: "profile_field");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "party",
                table: "credential_type");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "party",
                table: "credential_type");

            migrationBuilder.DropColumn(
                name: "business_app",
                schema: "party",
                table: "context_source");

            migrationBuilder.DropColumn(
                name: "entity_code",
                schema: "party",
                table: "context_source");
        }
    }
}
