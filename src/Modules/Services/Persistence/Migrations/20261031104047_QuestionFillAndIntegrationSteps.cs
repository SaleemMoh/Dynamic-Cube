using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Services.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QuestionFillAndIntegrationSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "integrations",
                schema: "svc",
                table: "request",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<bool>(
                name: "allow_manual_entry",
                schema: "svc",
                table: "question",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "fill",
                schema: "svc",
                table: "question",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "integration",
                schema: "svc",
                table: "question",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "service_code",
                schema: "svc",
                table: "question",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // Questions with a prefill path were filled from the party's data.
            migrationBuilder.Sql("UPDATE svc.question SET fill = 'Party' WHERE prefill IS NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "ix_question_service_code",
                schema: "svc",
                table: "question",
                column: "service_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_question_service_code",
                schema: "svc",
                table: "question");

            migrationBuilder.DropColumn(
                name: "integrations",
                schema: "svc",
                table: "request");

            migrationBuilder.DropColumn(
                name: "allow_manual_entry",
                schema: "svc",
                table: "question");

            migrationBuilder.DropColumn(
                name: "fill",
                schema: "svc",
                table: "question");

            migrationBuilder.DropColumn(
                name: "integration",
                schema: "svc",
                table: "question");

            migrationBuilder.DropColumn(
                name: "service_code",
                schema: "svc",
                table: "question");
        }
    }
}
