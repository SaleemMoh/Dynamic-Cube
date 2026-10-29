using System;
using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Parties.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PartyTypesToClassDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Records keep the code of their type instead of a key to the type table.
            foreach (var (table, column) in new[] { ("sanction", "sanction_type_code"), ("relationship", "relationship_code"), ("credential", "credential_type_code") })
            {
                migrationBuilder.AddColumn<string>(
                    name: column,
                    schema: "party",
                    table: table,
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: false,
                    defaultValue: "");
            }

            migrationBuilder.Sql("""
                UPDATE party.sanction s SET sanction_type_code = t.code FROM party.sanction_type t WHERE t.id = s.sanction_type_id;
                UPDATE party.relationship r SET relationship_code = t.code FROM party.relationship_type t WHERE t.id = r.relationship_type_id;
                UPDATE party.credential c SET credential_type_code = t.code FROM party.credential_type t WHERE t.id = c.credential_type_id;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_credential_credential_type_credential_type_id",
                schema: "party",
                table: "credential");

            migrationBuilder.DropForeignKey(
                name: "fk_relationship_relationship_type_relationship_type_id",
                schema: "party",
                table: "relationship");

            migrationBuilder.DropForeignKey(
                name: "fk_sanction_sanction_type_sanction_type_id",
                schema: "party",
                table: "sanction");

            migrationBuilder.DropIndex(
                name: "ix_sanction_sanction_type_id",
                schema: "party",
                table: "sanction");

            migrationBuilder.DropIndex(
                name: "ix_relationship_relationship_type_id",
                schema: "party",
                table: "relationship");

            migrationBuilder.DropIndex(
                name: "ix_credential_credential_type_id",
                schema: "party",
                table: "credential");

            migrationBuilder.DropIndex(
                name: "ix_credential_party_id_credential_type_id_number",
                schema: "party",
                table: "credential");

            migrationBuilder.DropColumn(
                name: "sanction_type_id",
                schema: "party",
                table: "sanction");

            migrationBuilder.DropColumn(
                name: "relationship_type_id",
                schema: "party",
                table: "relationship");

            migrationBuilder.DropColumn(
                name: "credential_type_id",
                schema: "party",
                table: "credential");

            migrationBuilder.CreateIndex(
                name: "ix_credential_party_id_credential_type_code_number",
                schema: "party",
                table: "credential",
                columns: new[] { "party_id", "credential_type_code", "number" },
                unique: true);

            // The types become values of classes PARTY_RELATIONSHIP_TYPE, PARTY_CREDENTIAL_TYPE and PARTY_SANCTION_TYPE.
            migrationBuilder.MoveToClass(
                "party.relationship_type",
                "PARTY_RELATIONSHIP_TYPE",
                "أنواع العلاقات",
                "Relationship types",
                "jsonb_build_object('CAN_ACT', s.can_act, 'EXTERNAL_CODES', s.external_codes)",
                "s.display_order");
            migrationBuilder.MoveToClass(
                "party.credential_type",
                "PARTY_CREDENTIAL_TYPE",
                "أنواع الوثائق",
                "Credential types",
                "jsonb_build_object('PARTY_KIND', upper(s.party_kind), 'ISSUER_AR', s.issuer_ar, 'ISSUER_EN', s.issuer_en, 'HAS_EXPIRY', s.has_expiry)");
            migrationBuilder.MoveToClass(
                "party.sanction_type",
                "PARTY_SANCTION_TYPE",
                "أنواع الجزاءات",
                "Sanction types",
                "jsonb_build_object('BLOCKS_APPLICATIONS', s.blocks_applications, 'DEFAULT_DAYS', s.default_days)");

            migrationBuilder.DropTable(
                name: "credential_type",
                schema: "party");

            migrationBuilder.DropTable(
                name: "relationship_type",
                schema: "party");

            migrationBuilder.DropTable(
                name: "sanction_type",
                schema: "party");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_credential_party_id_credential_type_code_number",
                schema: "party",
                table: "credential");

            migrationBuilder.DropColumn(
                name: "sanction_type_code",
                schema: "party",
                table: "sanction");

            migrationBuilder.DropColumn(
                name: "relationship_code",
                schema: "party",
                table: "relationship");

            migrationBuilder.DropColumn(
                name: "credential_type_code",
                schema: "party",
                table: "credential");

            migrationBuilder.AddColumn<Guid>(
                name: "sanction_type_id",
                schema: "party",
                table: "sanction",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "relationship_type_id",
                schema: "party",
                table: "relationship",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "credential_type_id",
                schema: "party",
                table: "credential",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "credential_type",
                schema: "party",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_app = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    entity_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    has_expiry = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    issuer_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    issuer_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    party_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_credential_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "relationship_type",
                schema: "party",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_app = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    can_act = table.Column<bool>(type: "boolean", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    entity_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    external_codes = table.Column<string>(type: "jsonb", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_relationship_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sanction_type",
                schema: "party",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocks_applications = table.Column<bool>(type: "boolean", nullable: false),
                    business_app = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    default_days = table.Column<int>(type: "integer", nullable: true),
                    entity_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sanction_type", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sanction_sanction_type_id",
                schema: "party",
                table: "sanction",
                column: "sanction_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_relationship_relationship_type_id",
                schema: "party",
                table: "relationship",
                column: "relationship_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_credential_credential_type_id",
                schema: "party",
                table: "credential",
                column: "credential_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_credential_party_id_credential_type_id_number",
                schema: "party",
                table: "credential",
                columns: new[] { "party_id", "credential_type_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_credential_type_code",
                schema: "party",
                table: "credential_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_relationship_type_code",
                schema: "party",
                table: "relationship_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sanction_type_code",
                schema: "party",
                table: "sanction_type",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_credential_credential_type_credential_type_id",
                schema: "party",
                table: "credential",
                column: "credential_type_id",
                principalSchema: "party",
                principalTable: "credential_type",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_relationship_relationship_type_relationship_type_id",
                schema: "party",
                table: "relationship",
                column: "relationship_type_id",
                principalSchema: "party",
                principalTable: "relationship_type",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sanction_sanction_type_sanction_type_id",
                schema: "party",
                table: "sanction",
                column: "sanction_type_id",
                principalSchema: "party",
                principalTable: "sanction_type",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
