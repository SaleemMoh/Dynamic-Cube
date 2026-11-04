using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.AppsControl.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropDomainExternalCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "domain_external_code",
                schema: "apps");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "domain_external_code",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    domain_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    provider_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_domain_external_code", x => x.id);
                    table.ForeignKey(
                        name: "fk_domain_external_code_domain_domain_id",
                        column: x => x.domain_id,
                        principalSchema: "apps",
                        principalTable: "domain",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_domain_external_code_domain_id_provider_code_direction",
                schema: "apps",
                table: "domain_external_code",
                columns: new[] { "domain_id", "provider_code", "direction" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_domain_external_code_provider_code_external_code_direction",
                schema: "apps",
                table: "domain_external_code",
                columns: new[] { "provider_code", "external_code", "direction" });
        }
    }
}
