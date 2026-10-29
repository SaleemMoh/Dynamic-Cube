using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Content.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PortalFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "page_feedback",
                schema: "cms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    voter_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    helpful = table.Column<bool>(type: "boolean", nullable: false),
                    answered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_page_feedback", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "service_rating",
                schema: "cms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    stars = table.Column<int>(type: "integer", nullable: false),
                    rated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_rating", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_page_feedback_page_key_voter_key",
                schema: "cms",
                table: "page_feedback",
                columns: new[] { "page_key", "voter_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_rating_service_code_user_id",
                schema: "cms",
                table: "service_rating",
                columns: new[] { "service_code", "user_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "page_feedback",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "service_rating",
                schema: "cms");
        }
    }
}
