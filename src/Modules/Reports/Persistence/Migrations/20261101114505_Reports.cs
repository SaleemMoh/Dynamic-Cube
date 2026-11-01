using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Reports.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Reports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "rpt");

            migrationBuilder.CreateTable(
                name: "delivery",
                schema: "rpt",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    report_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sent = table.Column<int>(type: "integer", nullable: false),
                    skipped = table.Column<int>(type: "integer", nullable: false),
                    failed = table.Column<int>(type: "integer", nullable: false),
                    recipients = table.Column<string>(type: "jsonb", nullable: false),
                    error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "report",
                schema: "rpt",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    description_ar = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    description_en = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    category_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    category_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    icon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    query = table.Column<string>(type: "jsonb", nullable: false),
                    columns = table.Column<string>(type: "jsonb", nullable: false),
                    filters = table.Column<string>(type: "jsonb", nullable: false),
                    tiles = table.Column<string>(type: "jsonb", nullable: false),
                    sort = table.Column<string>(type: "jsonb", nullable: false),
                    totals = table.Column<bool>(type: "boolean", nullable: false),
                    chart = table.Column<string>(type: "jsonb", nullable: true),
                    page_size = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_report", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "saved_filter",
                schema: "rpt",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    values = table.Column<string>(type: "jsonb", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saved_filter", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "schedule",
                schema: "rpt",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    time = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: true),
                    day_of_month = table.Column<int>(type: "integer", nullable: true),
                    format = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    users = table.Column<string>(type: "jsonb", nullable: false),
                    roles = table.Column<string>(type: "jsonb", nullable: false),
                    filter_values = table.Column<string>(type: "jsonb", nullable: false),
                    last_run_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_schedule", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_schedule_code_due_at",
                schema: "rpt",
                table: "delivery",
                columns: new[] { "schedule_code", "due_at" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delivery_started_at",
                schema: "rpt",
                table: "delivery",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "ix_report_code",
                schema: "rpt",
                table: "report",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_saved_filter_report_code_user_name_name",
                schema: "rpt",
                table: "saved_filter",
                columns: new[] { "report_code", "user_name", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_schedule_code",
                schema: "rpt",
                table: "schedule",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_schedule_report_code",
                schema: "rpt",
                table: "schedule",
                column: "report_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "delivery",
                schema: "rpt");

            migrationBuilder.DropTable(
                name: "report",
                schema: "rpt");

            migrationBuilder.DropTable(
                name: "saved_filter",
                schema: "rpt");

            migrationBuilder.DropTable(
                name: "schedule",
                schema: "rpt");
        }
    }
}
