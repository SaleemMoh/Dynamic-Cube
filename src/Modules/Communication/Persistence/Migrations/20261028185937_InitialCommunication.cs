using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Communication.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCommunication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "com");

            migrationBuilder.CreateTable(
                name: "message",
                schema: "com",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    rule_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recipient_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recipient = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    recipient_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    address = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    subject_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    title = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    body = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_message", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "template",
                schema: "com",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email_subject_ar = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    email_subject_en = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    email_body_ar = table.Column<string>(type: "text", nullable: true),
                    email_body_en = table.Column<string>(type: "text", nullable: true),
                    sms_ar = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    sms_en = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    in_app_title_ar = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    in_app_title_en = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    in_app_body_ar = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    in_app_body_en = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    link = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                name: "notification",
                schema: "com",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    event_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    title_ar = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    title_en = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    body_ar = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    body_en = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    link = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    subject_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_message_message_id",
                        column: x => x.message_id,
                        principalSchema: "com",
                        principalTable: "message",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rule",
                schema: "com",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    condition = table.Column<string>(type: "jsonb", nullable: true),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channels = table.Column<string[]>(type: "text[]", nullable: false),
                    recipients = table.Column<string>(type: "jsonb", nullable: false),
                    language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
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
                    table.PrimaryKey("pk_rule", x => x.id);
                    table.ForeignKey(
                        name: "fk_rule_template_template_id",
                        column: x => x.template_id,
                        principalSchema: "com",
                        principalTable: "template",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_message_event_id_rule_code_channel_recipient",
                schema: "com",
                table: "message",
                columns: new[] { "event_id", "rule_code", "channel", "recipient" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_message_status_created_at",
                schema: "com",
                table: "message",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_message_subject_type_subject_id",
                schema: "com",
                table: "message",
                columns: new[] { "subject_type", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_message_id",
                schema: "com",
                table: "notification",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_party_id_created_at",
                schema: "com",
                table: "notification",
                columns: new[] { "party_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_user_name_created_at",
                schema: "com",
                table: "notification",
                columns: new[] { "user_name", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_rule_code",
                schema: "com",
                table: "rule",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rule_event_code",
                schema: "com",
                table: "rule",
                column: "event_code");

            migrationBuilder.CreateIndex(
                name: "ix_rule_template_id",
                schema: "com",
                table: "rule",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "ix_template_code",
                schema: "com",
                table: "template",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification",
                schema: "com");

            migrationBuilder.DropTable(
                name: "rule",
                schema: "com");

            migrationBuilder.DropTable(
                name: "message",
                schema: "com");

            migrationBuilder.DropTable(
                name: "template",
                schema: "com");
        }
    }
}
