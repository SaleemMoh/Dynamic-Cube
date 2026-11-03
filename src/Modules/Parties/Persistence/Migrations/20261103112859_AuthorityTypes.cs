using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Parties.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuthorityTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "authority_type",
                schema: "party",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    description_ar = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    description_en = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    relationship_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    principal_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    principal_identifier_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    number_label_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    number_label_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    requires_expiry = table.Column<bool>(type: "boolean", nullable: false),
                    services = table.Column<string>(type: "jsonb", nullable: false),
                    operation_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    inputs = table.Column<string>(type: "jsonb", nullable: false),
                    valid_output = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    expiry_output = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    principal_name_ar_output = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    principal_name_en_output = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_authority_type", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_authority_type_code",
                schema: "party",
                table: "authority_type",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "authority_type",
                schema: "party");
        }
    }
}
