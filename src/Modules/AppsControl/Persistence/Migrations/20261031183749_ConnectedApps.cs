using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.AppsControl.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConnectedApps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "client_id",
                schema: "apps",
                table: "business_app",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "redirect_uris",
                schema: "apps",
                table: "business_app",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            // Connected apps registered before step 7 sign in with the default client: dc-app- and their code.
            migrationBuilder.Sql("""
                UPDATE apps.business_app
                SET client_id = 'dc-app-' || lower(code),
                    redirect_uris = CASE WHEN base_url IS NULL THEN '{}'::text[] ELSE ARRAY[rtrim(base_url, '/') || '/*'] END
                WHERE kind = 'Connected';
                """);

            migrationBuilder.CreateIndex(
                name: "ix_business_app_client_id",
                schema: "apps",
                table: "business_app",
                column: "client_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_business_app_client_id",
                schema: "apps",
                table: "business_app");

            migrationBuilder.DropColumn(
                name: "client_id",
                schema: "apps",
                table: "business_app");

            migrationBuilder.DropColumn(
                name: "redirect_uris",
                schema: "apps",
                table: "business_app");
        }
    }
}
