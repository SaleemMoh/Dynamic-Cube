using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Documents.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "doc");

            migrationBuilder.CreateTable(
                name: "number_sequence",
                schema: "doc",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_number_sequence", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "template",
                schema: "doc",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    html = table.Column<string>(type: "text", nullable: false),
                    page_size = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    landscape = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_template", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_type",
                schema: "doc",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    description_ar = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    description_en = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    number_prefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    validity_months = table.Column<int>(type: "integer", nullable: true),
                    one_per_holder = table.Column<bool>(type: "boolean", nullable: false),
                    amend_resets_validity = table.Column<bool>(type: "boolean", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    alert_days_before = table.Column<int[]>(type: "integer[]", nullable: false),
                    fields = table.Column<string>(type: "jsonb", nullable: false),
                    line_fields = table.Column<string>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("pk_document_type", x => x.id);
                    table.ForeignKey(
                        name: "fk_document_type_template_template_id",
                        column: x => x.template_id,
                        principalSchema: "doc",
                        principalTable: "template",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document",
                schema: "doc",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    holder_party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    holder_name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    holder_name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    current_version = table.Column<int>(type: "integer", nullable: false),
                    verification_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    alerts_sent = table.Column<int[]>(type: "integer[]", nullable: false),
                    status_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document", x => x.id);
                    table.ForeignKey(
                        name: "fk_document_document_type_document_type_id",
                        column: x => x.document_type_id,
                        principalSchema: "doc",
                        principalTable: "document_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "event",
                schema: "doc",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    actor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: true),
                    comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event", x => x.id);
                    table.ForeignKey(
                        name: "fk_event_document_document_id",
                        column: x => x.document_id,
                        principalSchema: "doc",
                        principalTable: "document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "version",
                schema: "doc",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false),
                    lines = table.Column<string>(type: "jsonb", nullable: false),
                    holder = table.Column<string>(type: "jsonb", nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    issued_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    source_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    file_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    rendered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    render_error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_version", x => x.id);
                    table.ForeignKey(
                        name: "fk_version_document_document_id",
                        column: x => x.document_id,
                        principalSchema: "doc",
                        principalTable: "document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_document_document_type_id",
                schema: "doc",
                table: "document",
                column: "document_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_holder_party_id_document_type_id",
                schema: "doc",
                table: "document",
                columns: new[] { "holder_party_id", "document_type_id" });

            migrationBuilder.CreateIndex(
                name: "ix_document_number",
                schema: "doc",
                table: "document",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_status_expires_at",
                schema: "doc",
                table: "document",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_document_verification_code",
                schema: "doc",
                table: "document",
                column: "verification_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_type_code",
                schema: "doc",
                table: "document_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_type_template_id",
                schema: "doc",
                table: "document_type",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_document_id_at",
                schema: "doc",
                table: "event",
                columns: new[] { "document_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_template_code",
                schema: "doc",
                table: "template",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_version_document_id_version",
                schema: "doc",
                table: "version",
                columns: new[] { "document_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_version_source_type_source_id",
                schema: "doc",
                table: "version",
                columns: new[] { "source_type", "source_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event",
                schema: "doc");

            migrationBuilder.DropTable(
                name: "number_sequence",
                schema: "doc");

            migrationBuilder.DropTable(
                name: "version",
                schema: "doc");

            migrationBuilder.DropTable(
                name: "document",
                schema: "doc");

            migrationBuilder.DropTable(
                name: "document_type",
                schema: "doc");

            migrationBuilder.DropTable(
                name: "template",
                schema: "doc");
        }
    }
}
