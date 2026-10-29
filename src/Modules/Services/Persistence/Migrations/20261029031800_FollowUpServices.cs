using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Services.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FollowUpServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "document_type",
                schema: "svc",
                table: "service",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_document_id",
                schema: "svc",
                table: "request",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_document_number",
                schema: "svc",
                table: "request",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_request_source_document_id_service_id",
                schema: "svc",
                table: "request",
                columns: new[] { "source_document_id", "service_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_request_source_document_id_service_id",
                schema: "svc",
                table: "request");

            migrationBuilder.DropColumn(
                name: "document_type",
                schema: "svc",
                table: "service");

            migrationBuilder.DropColumn(
                name: "source_document_id",
                schema: "svc",
                table: "request");

            migrationBuilder.DropColumn(
                name: "source_document_number",
                schema: "svc",
                table: "request");
        }
    }
}
