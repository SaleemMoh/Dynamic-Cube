using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Services.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NafathConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "nafath_check_id",
                schema: "svc",
                table: "request",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "nafath_confirmed_at",
                schema: "svc",
                table: "request",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "nafath_check_id",
                schema: "svc",
                table: "request");

            migrationBuilder.DropColumn(
                name: "nafath_confirmed_at",
                schema: "svc",
                table: "request");
        }
    }
}
