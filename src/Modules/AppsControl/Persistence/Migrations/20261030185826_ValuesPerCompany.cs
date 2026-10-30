using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.AppsControl.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ValuesPerCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "companies",
                schema: "apps",
                table: "domain",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<bool>(
                name: "values_per_company",
                schema: "apps",
                table: "class",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The generated class views read the column; they are recreated at startup.
            migrationBuilder.Sql("ALTER TABLE apps.domain DROP COLUMN companies CASCADE;");

            migrationBuilder.DropColumn(
                name: "values_per_company",
                schema: "apps",
                table: "class");
        }
    }
}
