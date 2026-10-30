using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Content.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PageVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "published_version",
                schema: "cms",
                table: "page",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "page_version",
                schema: "cms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    design = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    entity_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    business_app = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    shared_apps = table.Column<List<string>>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_page_version", x => x.id);
                    table.ForeignKey(
                        name: "fk_page_version_page_page_id",
                        column: x => x.page_id,
                        principalSchema: "cms",
                        principalTable: "page",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_page_version_code",
                schema: "cms",
                table: "page_version",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_page_version_page_id_number",
                schema: "cms",
                table: "page_version",
                columns: new[] { "page_id", "number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "page_version",
                schema: "cms");

            migrationBuilder.DropColumn(
                name: "published_version",
                schema: "cms",
                table: "page");
        }
    }
}
