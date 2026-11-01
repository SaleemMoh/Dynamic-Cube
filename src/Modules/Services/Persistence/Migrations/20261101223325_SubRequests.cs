using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Services.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SubRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "link_kind",
                schema: "svc",
                table: "request",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_request_id",
                schema: "svc",
                table: "request",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "parent_step",
                schema: "svc",
                table: "request",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_request_parent_request_id",
                schema: "svc",
                table: "request",
                column: "parent_request_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_request_parent_request_id",
                schema: "svc",
                table: "request");

            migrationBuilder.DropColumn(
                name: "link_kind",
                schema: "svc",
                table: "request");

            migrationBuilder.DropColumn(
                name: "parent_request_id",
                schema: "svc",
                table: "request");

            migrationBuilder.DropColumn(
                name: "parent_step",
                schema: "svc",
                table: "request");
        }
    }
}
