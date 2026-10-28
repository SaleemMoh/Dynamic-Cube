using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Integration.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "integ");

            migrationBuilder.CreateTable(
                name: "call_log",
                schema: "integ",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    connection_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    app_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duration_ms = table.Column<int>(type: "integer", nullable: false),
                    outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    http_status = table.Column<int>(type: "integer", nullable: true),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    from_cache = table.Column<bool>(type: "boolean", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    request_body = table.Column<string>(type: "text", nullable: true),
                    response_body = table.Column<string>(type: "text", nullable: true),
                    error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_call_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "envelope_profile",
                schema: "integ",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status_code_path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status_message_path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    success_status_codes = table.Column<string>(type: "jsonb", nullable: false),
                    business_code_path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    business_message_path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    success_business_codes = table.Column<string>(type: "jsonb", nullable: false),
                    result_path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    response_codes = table.Column<string>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("pk_envelope_profile", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "provider",
                schema: "integ",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    description_ar = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    description_en = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("pk_provider", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "connection",
                schema: "integ",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    auth_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    secret_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    token_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    api_key_header = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    timeout_seconds = table.Column<int>(type: "integer", nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    circuit_breaker_failures = table.Column<int>(type: "integer", nullable: false),
                    circuit_breaker_seconds = table.Column<int>(type: "integer", nullable: false),
                    headers = table.Column<string>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("pk_connection", x => x.id);
                    table.ForeignKey(
                        name: "fk_connection_provider_provider_id",
                        column: x => x.provider_id,
                        principalSchema: "integ",
                        principalTable: "provider",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "operation",
                schema: "integ",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    envelope_profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description_ar = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    description_en = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    path_template = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    parameters = table.Column<string>(type: "jsonb", nullable: false),
                    outputs = table.Column<string>(type: "jsonb", nullable: false),
                    response_codes = table.Column<string>(type: "jsonb", nullable: false),
                    mask_fields = table.Column<string>(type: "jsonb", nullable: false),
                    cache_seconds = table.Column<int>(type: "integer", nullable: false),
                    is_idempotent = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_operation", x => x.id);
                    table.ForeignKey(
                        name: "fk_operation_connection_connection_id",
                        column: x => x.connection_id,
                        principalSchema: "integ",
                        principalTable: "connection",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_operation_envelope_profile_envelope_profile_id",
                        column: x => x.envelope_profile_id,
                        principalSchema: "integ",
                        principalTable: "envelope_profile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_call_log_operation_code_started_at",
                schema: "integ",
                table: "call_log",
                columns: new[] { "operation_code", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_call_log_started_at",
                schema: "integ",
                table: "call_log",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "ix_connection_code",
                schema: "integ",
                table: "connection",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_connection_provider_id",
                schema: "integ",
                table: "connection",
                column: "provider_id");

            migrationBuilder.CreateIndex(
                name: "ix_envelope_profile_code",
                schema: "integ",
                table: "envelope_profile",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_operation_code",
                schema: "integ",
                table: "operation",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_operation_connection_id",
                schema: "integ",
                table: "operation",
                column: "connection_id");

            migrationBuilder.CreateIndex(
                name: "ix_operation_envelope_profile_id",
                schema: "integ",
                table: "operation",
                column: "envelope_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_provider_code",
                schema: "integ",
                table: "provider",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "call_log",
                schema: "integ");

            migrationBuilder.DropTable(
                name: "operation",
                schema: "integ");

            migrationBuilder.DropTable(
                name: "connection",
                schema: "integ");

            migrationBuilder.DropTable(
                name: "envelope_profile",
                schema: "integ");

            migrationBuilder.DropTable(
                name: "provider",
                schema: "integ");
        }
    }
}
