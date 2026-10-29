using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Visits.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialVisits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "vis");

            migrationBuilder.CreateTable(
                name: "number_sequence",
                schema: "vis",
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
                name: "visit_type",
                schema: "vis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description_ar = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    description_en = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    outcomes = table.Column<string>(type: "jsonb", nullable: false),
                    fields = table.Column<string>(type: "jsonb", nullable: false),
                    photos_required = table.Column<int>(type: "integer", nullable: false),
                    require_location = table.Column<bool>(type: "boolean", nullable: false),
                    require_signature = table.Column<bool>(type: "boolean", nullable: false),
                    inspector_role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    due_days = table.Column<int>(type: "integer", nullable: false),
                    external = table.Column<string>(type: "jsonb", nullable: true),
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
                    table.PrimaryKey("pk_visit_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "visit",
                schema: "vis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subject_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    title_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    title_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: true),
                    party_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    site_address = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    instructions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    inspector = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    inspector_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    outcome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    data = table.Column<string>(type: "jsonb", nullable: false),
                    report = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    captured_latitude = table.Column<double>(type: "double precision", nullable: true),
                    captured_longitude = table.Column<double>(type: "double precision", nullable: true),
                    captured_accuracy = table.Column<double>(type: "double precision", nullable: true),
                    signature_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    signed_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    submission_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    external_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    external_status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    next_poll_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_visit", x => x.id);
                    table.ForeignKey(
                        name: "fk_visit_visit_type_type_id",
                        column: x => x.type_id,
                        principalSchema: "vis",
                        principalTable: "visit_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "visit_event",
                schema: "vis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    visit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    actor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_visit_event", x => x.id);
                    table.ForeignKey(
                        name: "fk_visit_event_visit_visit_id",
                        column: x => x.visit_id,
                        principalSchema: "vis",
                        principalTable: "visit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "visit_photo",
                schema: "vis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    visit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    length = table.Column<long>(type: "bigint", nullable: false),
                    caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_visit_photo", x => x.id);
                    table.ForeignKey(
                        name: "fk_visit_photo_visit_visit_id",
                        column: x => x.visit_id,
                        principalSchema: "vis",
                        principalTable: "visit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_visit_inspector_status",
                schema: "vis",
                table: "visit",
                columns: new[] { "inspector", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_visit_number",
                schema: "vis",
                table: "visit",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_visit_status_next_poll_at",
                schema: "vis",
                table: "visit",
                columns: new[] { "status", "next_poll_at" });

            migrationBuilder.CreateIndex(
                name: "ix_visit_subject_type_subject_id",
                schema: "vis",
                table: "visit",
                columns: new[] { "subject_type", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "ix_visit_type_id_external_reference",
                schema: "vis",
                table: "visit",
                columns: new[] { "type_id", "external_reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_visit_event_visit_id_at",
                schema: "vis",
                table: "visit_event",
                columns: new[] { "visit_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_visit_photo_visit_id",
                schema: "vis",
                table: "visit_photo",
                column: "visit_id");

            migrationBuilder.CreateIndex(
                name: "ix_visit_type_code",
                schema: "vis",
                table: "visit_type",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "number_sequence",
                schema: "vis");

            migrationBuilder.DropTable(
                name: "visit_event",
                schema: "vis");

            migrationBuilder.DropTable(
                name: "visit_photo",
                schema: "vis");

            migrationBuilder.DropTable(
                name: "visit",
                schema: "vis");

            migrationBuilder.DropTable(
                name: "visit_type",
                schema: "vis");
        }
    }
}
