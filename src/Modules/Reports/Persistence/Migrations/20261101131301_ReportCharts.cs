using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Reports.Persistence.Migrations
{
    /// <summary>Several charts per report (ADR-0045): the single chart becomes the first of the list.</summary>
    public partial class ReportCharts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "charts",
                schema: "rpt",
                table: "report",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            // A report's single chart (number columns of the report along one of its columns) becomes its first chart:
            // the X axis holds the category column's column, the Y axis the number columns with their totals.
            migrationBuilder.Sql("""
                UPDATE rpt.report r
                SET charts = jsonb_build_array(jsonb_build_object(
                    'name', 'chart1',
                    'titleAr', r.name_ar,
                    'titleEn', r.name_en,
                    'type', coalesce(r.chart->>'type', 'BAR'),
                    'x', jsonb_build_array(jsonb_build_object(
                        'column', (SELECT c->>'column' FROM jsonb_array_elements(r.columns) c WHERE c->>'name' = r.chart->>'category' LIMIT 1),
                        'bucket', NULL,
                        'labelAr', (SELECT c->>'labelAr' FROM jsonb_array_elements(r.columns) c WHERE c->>'name' = r.chart->>'category' LIMIT 1),
                        'labelEn', (SELECT c->>'labelEn' FROM jsonb_array_elements(r.columns) c WHERE c->>'name' = r.chart->>'category' LIMIT 1))),
                    'series', NULL,
                    'y', (SELECT coalesce(jsonb_agg(jsonb_build_object(
                              'column', c->>'column',
                              'aggregate', coalesce(nullif(c->>'aggregate', ''), 'SUM'),
                              'labelAr', c->>'labelAr',
                              'labelEn', c->>'labelEn')), '[]'::jsonb)
                          FROM jsonb_array_elements(r.columns) c
                          WHERE coalesce(r.chart->'values', '[]'::jsonb) ? (c->>'name')),
                    'stacked', false,
                    'sort', 'Y_DESC',
                    'limit', 20,
                    'width', 'FULL'))
                WHERE r.chart IS NOT NULL AND jsonb_typeof(r.chart) = 'object';
                """);

            migrationBuilder.DropColumn(
                name: "chart",
                schema: "rpt",
                table: "report");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "charts",
                schema: "rpt",
                table: "report");

            migrationBuilder.AddColumn<string>(
                name: "chart",
                schema: "rpt",
                table: "report",
                type: "jsonb",
                nullable: true);
        }
    }
}
