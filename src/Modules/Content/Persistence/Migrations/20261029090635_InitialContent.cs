using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Content.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cms");

            migrationBuilder.CreateTable(
                name: "faq",
                schema: "cms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    answer_ar = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    answer_en = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    topic_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    topic_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    service_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    order = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_faq", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "page",
                schema: "cms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    body_ar = table.Column<string>(type: "text", nullable: false),
                    body_en = table.Column<string>(type: "text", nullable: false),
                    show_in_footer = table.Column<bool>(type: "boolean", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_page", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_faq_code",
                schema: "cms",
                table: "faq",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_faq_service_code",
                schema: "cms",
                table: "faq",
                column: "service_code");

            migrationBuilder.CreateIndex(
                name: "ix_page_code",
                schema: "cms",
                table: "page",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "faq",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "page",
                schema: "cms");
        }
    }
}
