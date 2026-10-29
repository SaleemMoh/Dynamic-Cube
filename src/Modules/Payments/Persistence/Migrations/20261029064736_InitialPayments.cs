using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Payments.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pay");

            migrationBuilder.CreateTable(
                name: "number_sequence",
                schema: "pay",
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
                name: "provider",
                schema: "pay",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    create_operation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    bill_number_output = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    biller_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    due_days = table.Column<int>(type: "integer", nullable: false),
                    pay_url_template = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    instructions_ar = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    instructions_en = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    callback_secret = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    callback = table.Column<string>(type: "jsonb", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
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
                name: "bill",
                schema: "pay",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    provider_bill_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    subject_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    payer_party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payer_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description_ar = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    description_en = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    lines = table.Column<string>(type: "jsonb", nullable: false),
                    net = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    paid_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    payment_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bill", x => x.id);
                    table.ForeignKey(
                        name: "fk_bill_provider_provider_id",
                        column: x => x.provider_id,
                        principalSchema: "pay",
                        principalTable: "provider",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bill_event",
                schema: "pay",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    actor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bill_event", x => x.id);
                    table.ForeignKey(
                        name: "fk_bill_event_bill_bill_id",
                        column: x => x.bill_id,
                        principalSchema: "pay",
                        principalTable: "bill",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bill_number",
                schema: "pay",
                table: "bill",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bill_provider_id_provider_bill_number",
                schema: "pay",
                table: "bill",
                columns: new[] { "provider_id", "provider_bill_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bill_status_due_at",
                schema: "pay",
                table: "bill",
                columns: new[] { "status", "due_at" });

            migrationBuilder.CreateIndex(
                name: "ix_bill_subject_type_subject_id",
                schema: "pay",
                table: "bill",
                columns: new[] { "subject_type", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "ix_bill_event_bill_id_at",
                schema: "pay",
                table: "bill_event",
                columns: new[] { "bill_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_provider_code",
                schema: "pay",
                table: "provider",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bill_event",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "number_sequence",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "bill",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "provider",
                schema: "pay");
        }
    }
}
