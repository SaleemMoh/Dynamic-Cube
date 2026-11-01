using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Integration.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Nafath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "nafath_request",
                schema: "integ",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    service = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    subject_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: true),
                    protected_national_id = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    trans_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    random = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    protected_state = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    hashed_state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    client_redirect_uri = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    client_state = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    client_nonce = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    code_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    protected_person = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nafath_request", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_nafath_request_code_hash",
                schema: "integ",
                table: "nafath_request",
                column: "code_hash");

            migrationBuilder.CreateIndex(
                name: "ix_nafath_request_hashed_state",
                schema: "integ",
                table: "nafath_request",
                column: "hashed_state");

            migrationBuilder.CreateIndex(
                name: "ix_nafath_request_subject_type_subject_id",
                schema: "integ",
                table: "nafath_request",
                columns: new[] { "subject_type", "subject_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nafath_request",
                schema: "integ");
        }
    }
}
