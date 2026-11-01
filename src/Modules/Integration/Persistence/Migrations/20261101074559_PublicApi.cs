using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Integration.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PublicApi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "api_call",
                schema: "integ",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    client_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    endpoint_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    path = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duration_ms = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    rows = table.Column<int>(type: "integer", nullable: true),
                    error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    client_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    is_try = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_api_call", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "api_client",
                schema: "integ",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description_ar = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    description_en = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    endpoints = table.Column<string>(type: "jsonb", nullable: false),
                    companies = table.Column<string>(type: "jsonb", nullable: false),
                    branches = table.Column<string>(type: "jsonb", nullable: false),
                    rate_limit_per_minute = table.Column<int>(type: "integer", nullable: false),
                    contact_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("pk_api_client", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "api_endpoint",
                schema: "integ",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description_ar = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    description_en = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    parameters = table.Column<string>(type: "jsonb", nullable: false),
                    query = table.Column<string>(type: "jsonb", nullable: true),
                    fields = table.Column<string>(type: "jsonb", nullable: false),
                    single_row = table.Column<bool>(type: "boolean", nullable: false),
                    page_size = table.Column<int>(type: "integer", nullable: false),
                    function = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    function_inputs = table.Column<string>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("pk_api_endpoint", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_api_call_client_code_started_at",
                schema: "integ",
                table: "api_call",
                columns: new[] { "client_code", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_api_call_started_at",
                schema: "integ",
                table: "api_call",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "ix_api_client_client_id",
                schema: "integ",
                table: "api_client",
                column: "client_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_api_client_code",
                schema: "integ",
                table: "api_client",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_api_endpoint_code",
                schema: "integ",
                table: "api_endpoint",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_api_endpoint_path",
                schema: "integ",
                table: "api_endpoint",
                column: "path",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_call",
                schema: "integ");

            migrationBuilder.DropTable(
                name: "api_client",
                schema: "integ");

            migrationBuilder.DropTable(
                name: "api_endpoint",
                schema: "integ");
        }
    }
}
