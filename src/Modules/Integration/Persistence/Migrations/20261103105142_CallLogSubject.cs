using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Integration.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CallLogSubject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "subject_id",
                schema: "integ",
                table: "call_log",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "subject_type",
                schema: "integ",
                table: "call_log",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_call_log_subject_type_subject_id",
                schema: "integ",
                table: "call_log",
                columns: new[] { "subject_type", "subject_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_call_log_subject_type_subject_id",
                schema: "integ",
                table: "call_log");

            migrationBuilder.DropColumn(
                name: "subject_id",
                schema: "integ",
                table: "call_log");

            migrationBuilder.DropColumn(
                name: "subject_type",
                schema: "integ",
                table: "call_log");
        }
    }
}
