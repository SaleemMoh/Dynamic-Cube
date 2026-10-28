using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Services.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "svc");

            migrationBuilder.CreateTable(
                name: "question",
                schema: "svc",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    hint_ar = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    hint_en = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    lov = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    lov_parent_question = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    options = table.Column<string>(type: "jsonb", nullable: false),
                    multiple = table.Column<bool>(type: "boolean", nullable: false),
                    validation = table.Column<string>(type: "jsonb", nullable: false),
                    prefill = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    name_en = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_question", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "service",
                schema: "svc",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    description_ar = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    description_en = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    archetype = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    category_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    icon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    party_kinds = table.Column<string>(type: "jsonb", nullable: false),
                    is_listed = table.Column<bool>(type: "boolean", nullable: false),
                    drafts_allowed = table.Column<bool>(type: "boolean", nullable: false),
                    draft_expiry_days = table.Column<int>(type: "integer", nullable: false),
                    on_new_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    number_prefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("pk_service", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "service_version",
                schema: "svc",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    definition = table.Column<string>(type: "jsonb", nullable: false),
                    resolved = table.Column<string>(type: "jsonb", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_retired = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_service_version", x => x.id);
                    table.ForeignKey(
                        name: "fk_service_version_service_service_id",
                        column: x => x.service_id,
                        principalSchema: "svc",
                        principalTable: "service",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_question_code",
                schema: "svc",
                table: "question",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_code",
                schema: "svc",
                table: "service",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_version_code",
                schema: "svc",
                table: "service_version",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_version_service_id_number",
                schema: "svc",
                table: "service_version",
                columns: new[] { "service_id", "number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "question",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "service_version",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "service",
                schema: "svc");
        }
    }
}
