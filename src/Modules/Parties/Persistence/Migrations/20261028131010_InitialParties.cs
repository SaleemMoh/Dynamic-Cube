using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Parties.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialParties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "party");

            migrationBuilder.CreateTable(
                name: "context_source",
                schema: "party",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    input_parameter = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    items_path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    principal_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    principal_identifier_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    identifier_path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_ar_path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en_path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    relation_code_path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    relation_provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    default_relationship_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status_path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    active_statuses = table.Column<string>(type: "jsonb", nullable: false),
                    valid_from_path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    valid_to_path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    services_path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    refresh_minutes = table.Column<int>(type: "integer", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_context_source", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "credential_type",
                schema: "party",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    issuer_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    issuer_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    has_expiry = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_credential_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "party",
                schema: "party",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    contexts_refreshed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_party", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "profile_field",
                schema: "party",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    data_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    operation_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    output_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    inputs = table.Column<string>(type: "jsonb", nullable: false),
                    refresh_hours = table.Column<int>(type: "integer", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_profile_field", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "relationship_type",
                schema: "party",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    can_act = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    external_codes = table.Column<string>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("pk_relationship_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sanction_type",
                schema: "party",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocks_applications = table.Column<bool>(type: "boolean", nullable: false),
                    default_days = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("pk_sanction_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "credential",
                schema: "party",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    credential_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    issued_on = table.Column<DateOnly>(type: "date", nullable: true),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: true),
                    source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    refreshed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_credential", x => x.id);
                    table.ForeignKey(
                        name: "fk_credential_credential_type_credential_type_id",
                        column: x => x.credential_type_id,
                        principalSchema: "party",
                        principalTable: "credential_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_credential_party_party_id",
                        column: x => x.party_id,
                        principalSchema: "party",
                        principalTable: "party",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "party_identifier",
                schema: "party",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_party_identifier", x => x.id);
                    table.ForeignKey(
                        name: "fk_party_identifier_party_party_id",
                        column: x => x.party_id,
                        principalSchema: "party",
                        principalTable: "party",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project",
                schema: "party",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    client_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    contract_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    start_on = table.Column<DateOnly>(type: "date", nullable: true),
                    end_on = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: true),
                    source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project", x => x.id);
                    table.ForeignKey(
                        name: "fk_project_party_party_id",
                        column: x => x.party_id,
                        principalSchema: "party",
                        principalTable: "party",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "profile_value",
                schema: "party",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "jsonb", nullable: true),
                    source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    refreshed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_profile_value", x => x.id);
                    table.ForeignKey(
                        name: "fk_profile_value_party_party_id",
                        column: x => x.party_id,
                        principalSchema: "party",
                        principalTable: "party",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_profile_value_profile_field_field_id",
                        column: x => x.field_id,
                        principalSchema: "party",
                        principalTable: "profile_field",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "relationship",
                schema: "party",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    individual_id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relationship_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    services = table.Column<string>(type: "jsonb", nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: true),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_relationship", x => x.id);
                    table.ForeignKey(
                        name: "fk_relationship_party_individual_id",
                        column: x => x.individual_id,
                        principalSchema: "party",
                        principalTable: "party",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_relationship_party_principal_id",
                        column: x => x.principal_id,
                        principalSchema: "party",
                        principalTable: "party",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_relationship_relationship_type_relationship_type_id",
                        column: x => x.relationship_type_id,
                        principalSchema: "party",
                        principalTable: "relationship_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sanction",
                schema: "party",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sanction_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    lifted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lifted_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    lift_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sanction", x => x.id);
                    table.ForeignKey(
                        name: "fk_sanction_party_party_id",
                        column: x => x.party_id,
                        principalSchema: "party",
                        principalTable: "party",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sanction_sanction_type_sanction_type_id",
                        column: x => x.sanction_type_id,
                        principalSchema: "party",
                        principalTable: "sanction_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_context_source_code",
                schema: "party",
                table: "context_source",
                column: "code",
                unique: true);

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
                name: "ix_party_identifier_party_id",
                schema: "party",
                table: "party_identifier",
                column: "party_id");

            migrationBuilder.CreateIndex(
                name: "ix_party_identifier_type_value",
                schema: "party",
                table: "party_identifier",
                columns: new[] { "type", "value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_profile_field_code",
                schema: "party",
                table: "profile_field",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_profile_value_field_id",
                schema: "party",
                table: "profile_value",
                column: "field_id");

            migrationBuilder.CreateIndex(
                name: "ix_profile_value_party_id_field_id",
                schema: "party",
                table: "profile_value",
                columns: new[] { "party_id", "field_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_party_id",
                schema: "party",
                table: "project",
                column: "party_id");

            migrationBuilder.CreateIndex(
                name: "ix_relationship_individual_id_principal_id",
                schema: "party",
                table: "relationship",
                columns: new[] { "individual_id", "principal_id" });

            migrationBuilder.CreateIndex(
                name: "ix_relationship_principal_id",
                schema: "party",
                table: "relationship",
                column: "principal_id");

            migrationBuilder.CreateIndex(
                name: "ix_relationship_relationship_type_id",
                schema: "party",
                table: "relationship",
                column: "relationship_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_relationship_type_code",
                schema: "party",
                table: "relationship_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sanction_party_id",
                schema: "party",
                table: "sanction",
                column: "party_id");

            migrationBuilder.CreateIndex(
                name: "ix_sanction_sanction_type_id",
                schema: "party",
                table: "sanction",
                column: "sanction_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_sanction_type_code",
                schema: "party",
                table: "sanction_type",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "context_source",
                schema: "party");

            migrationBuilder.DropTable(
                name: "credential",
                schema: "party");

            migrationBuilder.DropTable(
                name: "party_identifier",
                schema: "party");

            migrationBuilder.DropTable(
                name: "profile_value",
                schema: "party");

            migrationBuilder.DropTable(
                name: "project",
                schema: "party");

            migrationBuilder.DropTable(
                name: "relationship",
                schema: "party");

            migrationBuilder.DropTable(
                name: "sanction",
                schema: "party");

            migrationBuilder.DropTable(
                name: "credential_type",
                schema: "party");

            migrationBuilder.DropTable(
                name: "profile_field",
                schema: "party");

            migrationBuilder.DropTable(
                name: "relationship_type",
                schema: "party");

            migrationBuilder.DropTable(
                name: "party",
                schema: "party");

            migrationBuilder.DropTable(
                name: "sanction_type",
                schema: "party");
        }
    }
}
