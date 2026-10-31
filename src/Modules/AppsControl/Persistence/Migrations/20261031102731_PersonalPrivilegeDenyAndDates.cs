using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.AppsControl.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersonalPrivilegeDenyAndDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "denied_actions",
                schema: "apps",
                table: "user_privilege",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<DateOnly>(
                name: "effective_from",
                schema: "apps",
                table: "user_privilege",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "effective_to",
                schema: "apps",
                table: "user_privilege",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "denied_actions",
                schema: "apps",
                table: "user_privilege");

            migrationBuilder.DropColumn(
                name: "effective_from",
                schema: "apps",
                table: "user_privilege");

            migrationBuilder.DropColumn(
                name: "effective_to",
                schema: "apps",
                table: "user_privilege");
        }
    }
}
