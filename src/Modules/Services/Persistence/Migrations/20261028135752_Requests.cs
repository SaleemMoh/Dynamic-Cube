using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Services.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Requests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "number_sequence",
                schema: "svc",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_number_sequence", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "request",
                schema: "svc",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    service_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    level_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    submitter_party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitter_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    beneficiary_party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    beneficiary_name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    beneficiary_name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status_category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    display_status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false),
                    current_step = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    fees = table.Column<string>(type: "jsonb", nullable: false),
                    fee_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    draft_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_request", x => x.id);
                    table.ForeignKey(
                        name: "fk_request_service_version_service_version_id",
                        column: x => x.service_version_id,
                        principalSchema: "svc",
                        principalTable: "service_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "request_event",
                schema: "svc",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    from_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    to_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_request_event", x => x.id);
                    table.ForeignKey(
                        name: "fk_request_event_request_request_id",
                        column: x => x.request_id,
                        principalSchema: "svc",
                        principalTable: "request",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_request_beneficiary_party_id_status_category",
                schema: "svc",
                table: "request",
                columns: new[] { "beneficiary_party_id", "status_category" });

            migrationBuilder.CreateIndex(
                name: "ix_request_number",
                schema: "svc",
                table: "request",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_request_service_code_status_category",
                schema: "svc",
                table: "request",
                columns: new[] { "service_code", "status_category" });

            migrationBuilder.CreateIndex(
                name: "ix_request_service_version_id",
                schema: "svc",
                table: "request",
                column: "service_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_event_request_id_at",
                schema: "svc",
                table: "request_event",
                columns: new[] { "request_id", "at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "number_sequence",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "request_event",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "request",
                schema: "svc");
        }
    }
}
