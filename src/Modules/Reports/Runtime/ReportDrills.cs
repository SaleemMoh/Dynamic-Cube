using System.Text.Json.Nodes;
using DynamicCube.Modules.Reports.Domain;
using DynamicCube.Platform.Reports;

namespace DynamicCube.Modules.Reports.Runtime;

/// <summary>Where a drill-down starts (ADR-0047): a row of the grouped table, a point of a chart, a tile or the totals row.</summary>
public static class DrillSources
{
    public const string Table = "TABLE";
    public const string Chart = "CHART";
    public const string Tile = "TILE";
    public const string Totals = "TOTALS";

    public static IReadOnlyList<string> All { get; } = [Table, Chart, Tile, Totals];
}

/// <summary>
/// Opening the rows behind a group: the report's filter values, where the group was clicked (<see cref="DrillSources"/>),
/// the chart's name for a point, the group's values by their key in the clicked row (a table row's columns, a chart
/// point's <c>x0</c> / <c>y0</c> and <c>s</c>), and the page.
/// </summary>
public sealed record DrillRequest(Dictionary<string, string?>? Filters, string Source, string? Chart, JsonObject? Values, int Page = 1, int? PageSize = null);

/// <summary>A value the rows behind a group are narrowed by, with the label of its column.</summary>
public sealed record DrillValue(string LabelAr, string LabelEn, JsonNode? Value, string? Bucket);

/// <summary>A page of the rows behind a group: their columns, rows, number, the page, the filters used and the group's values.</summary>
public sealed record DrillRun(
    IReadOnlyList<RunColumn> Columns,
    IReadOnlyList<JsonObject> Rows,
    long Total,
    int Page,
    int PageSize,
    bool HasMore,
    IReadOnlyDictionary<string, string?> Filters,
    IReadOnlyList<DrillValue> Narrowed);

/// <summary>
/// The rows behind a group (ADR-0047): which columns they show, and the conditions a clicked group adds to the report's
/// query. The columns and conditions come from the report's definition only; the request names the group's values by
/// their key, so nothing but values (always parameters) reaches the query.
/// </summary>
internal static class ReportDrills
{
    /// <summary>Totals whose values read as they are still mean something row by row.</summary>
    private static readonly string[] Readable = [ReportAggregates.Sum, ReportAggregates.Avg, ReportAggregates.Min, ReportAggregates.Max];

    /// <summary>The columns of the rows behind a group: the report's details, or its columns read as they are.</summary>
    public static IReadOnlyList<ReportColumnDef> Columns(ReportDefinition report)
    {
        if (report.Details.Count > 0)
        {
            return report.Details;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        return
        [
            .. report.Columns
                .Where(c => !string.IsNullOrWhiteSpace(c.Column) && (string.IsNullOrWhiteSpace(c.Aggregate) || Readable.Contains(c.Aggregate)))
                .Where(c => seen.Add(c.Column))
                .Select(c => new ReportColumnDef { Name = c.Name, LabelAr = c.LabelAr, LabelEn = c.LabelEn, Column = c.Column }),
        ];
    }

    /// <summary>Whether the report's table has groups to open (some column totalled).</summary>
    public static bool Grouped(ReportDefinition report) => report.Columns.Any(c => !string.IsNullOrWhiteSpace(c.Aggregate));

    /// <summary>
    /// The conditions the clicked group adds, with their labels; throws <see cref="ReportInputException"/> when the group
    /// cannot be opened (a table that is not grouped, a chart drawing rows, a value missing).
    /// </summary>
    public static (IReadOnlyList<ReportMatch> Matches, IReadOnlyList<DrillValue> Narrowed) Matches(ReportDefinition report, DrillRequest request)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(request);
        var values = request.Values ?? [];
        var matches = new List<ReportMatch>();
        var narrowed = new List<DrillValue>();

        JsonNode? Value(string key) =>
            values.TryGetPropertyValue(key, out var value)
                ? value?.DeepClone()
                : throw new ReportInputException(new Dictionary<string, string[]> { ["values"] = [$"The value of '{key}' is missing."] });

        switch (request.Source?.ToUpperInvariant())
        {
            case DrillSources.Table:
                if (!Grouped(report))
                {
                    throw new ReportInputException(new Dictionary<string, string[]> { ["source"] = ["The report's rows are not grouped; each row is already a record."] });
                }

                foreach (var column in report.Columns.Where(c => string.IsNullOrWhiteSpace(c.Aggregate) && !string.IsNullOrWhiteSpace(c.Column)))
                {
                    var value = Value(column.Name);
                    matches.Add(new ReportMatch(column.Column, null, value));
                    narrowed.Add(new DrillValue(column.LabelAr, column.LabelEn, value, null));
                }

                break;

            case DrillSources.Chart:
                var chart = report.Charts.FirstOrDefault(c => c.Name == request.Chart)
                    ?? throw new ReportInputException(new Dictionary<string, string[]> { ["chart"] = ["The report has no such chart."] });
                var axis = ReportShapes.CategoryAxis(chart);
                if (axis is not ("X" or "Y"))
                {
                    throw new ReportInputException(new Dictionary<string, string[]> { ["chart"] = ["Each point of this chart is already a record."] });
                }

                var category = axis == "X" ? chart.X[0] : chart.Y[0];
                Add(category, axis == "X" ? "x0" : "y0");
                if (chart.Series is { } series)
                {
                    Add(series, "s");
                }

                void Add(ReportChartField field, string key)
                {
                    var value = Value(key);
                    var bucket = string.IsNullOrWhiteSpace(field.Bucket) ? null : field.Bucket.Trim();
                    matches.Add(new ReportMatch(field.Column ?? string.Empty, bucket, value));
                    var column = report.Columns.FirstOrDefault(c => c.Column == field.Column && string.IsNullOrWhiteSpace(c.Aggregate));
                    var tail = field.Column is null ? string.Empty : field.Column[(field.Column.IndexOf('.', StringComparison.Ordinal) + 1)..];
                    narrowed.Add(new DrillValue(
                        Text(field.LabelAr) ?? column?.LabelAr ?? tail,
                        Text(field.LabelEn) ?? column?.LabelEn ?? tail,
                        value,
                        bucket));
                }

                break;

            case DrillSources.Tile:
            case DrillSources.Totals:
                // All the rows the filters keep.
                break;

            default:
                throw new ReportInputException(new Dictionary<string, string[]> { ["source"] = ["A table row, a chart point, a tile or the totals row."] });
        }

        return (matches, narrowed);
    }

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
