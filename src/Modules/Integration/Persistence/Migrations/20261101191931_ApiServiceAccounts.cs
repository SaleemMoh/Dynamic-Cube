using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Integration.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApiServiceAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "acts_for_user",
                schema: "integ",
                table: "api_endpoint",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "account",
                schema: "integ",
                table: "api_client",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "pending_access",
                schema: "integ",
                table: "api_client",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "acting_for",
                schema: "integ",
                table: "api_call",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            // Each client acts as its own service account from now on (ADR-0042); what it was given moves to that account's
            // role when the migrator next runs (PublicApiBackfill).
            migrationBuilder.Sql("""
                UPDATE integ.api_client
                SET account = 'svc-' || replace(lower(code), '_', '-'),
                    pending_access = jsonb_build_object('endpoints', endpoints, 'companies', companies, 'branches', branches);
                """);

            migrationBuilder.DropColumn(
                name: "branches",
                schema: "integ",
                table: "api_client");

            migrationBuilder.DropColumn(
                name: "companies",
                schema: "integ",
                table: "api_client");

            migrationBuilder.DropColumn(
                name: "endpoints",
                schema: "integ",
                table: "api_client");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "acts_for_user",
                schema: "integ",
                table: "api_endpoint");

            migrationBuilder.DropColumn(
                name: "account",
                schema: "integ",
                table: "api_client");

            migrationBuilder.DropColumn(
                name: "pending_access",
                schema: "integ",
                table: "api_client");

            migrationBuilder.DropColumn(
                name: "acting_for",
                schema: "integ",
                table: "api_call");

            migrationBuilder.AddColumn<string>(
                name: "branches",
                schema: "integ",
                table: "api_client",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "companies",
                schema: "integ",
                table: "api_client",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "endpoints",
                schema: "integ",
                table: "api_client",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }
    }
}
