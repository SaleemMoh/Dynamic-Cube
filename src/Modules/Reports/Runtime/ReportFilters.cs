using System.Globalization;
using DynamicCube.Modules.Reports.Domain;
using DynamicCube.Platform.Reports;

namespace DynamicCube.Modules.Reports.Runtime;

/// <summary>
/// Filter values (ADR-0045): checked against the filter's kind, defaults resolved (relative dates such as <c>TODAY-30</c>
/// or <c>MONTH_START</c>), several list values joined for the query's "one of" conditions.
/// </summary>
internal static class ReportFilters
{
    private static readonly string[] Relative = ["TODAY", "MONTH_START", "LAST_MONTH_START", "LAST_MONTH_END", "YEAR_START"];

    /// <summary>What is wrong with a value of the filter (a default may be a relative date); null when it fits.</summary>
    public static string? Check(ReportFilterDef filter, string value, DateOnly today) => filter.Type switch
    {
        ReportFilterTypes.Number when !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _) => "A number.",
        ReportFilterTypes.Date when RelativeDate(value, today) is null && !DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) =>
            "A date (YYYY-MM-DD) or TODAY, TODAY-30, MONTH_START, LAST_MONTH_START, LAST_MONTH_END, YEAR_START.",
        ReportFilterTypes.YesNo when value is not ("true" or "false") => "true or false.",
        _ => null,
    };

    /// <summary>
    /// The inputs of a run: every filter, its given value or else its default (relative dates resolved for
    /// <paramref name="today"/>); a required filter left empty and values that do not fit are refused.
    /// </summary>
    public static Dictionary<string, string?> Resolve(IReadOnlyList<ReportFilterDef> filters, IReadOnlyDictionary<string, string?>? given, DateOnly today)
    {
        var inputs = new Dictionary<string, string?>(StringComparer.Ordinal);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var name in (given ?? new Dictionary<string, string?>()).Keys.Where(k => filters.All(f => f.Name != k)))
        {
            errors[name] = ["The report has no such filter."];
        }

        foreach (var filter in filters)
        {
            var value = given?.GetValueOrDefault(filter.Name);
            value = string.IsNullOrWhiteSpace(value) ? filter.Default : value.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                if (filter.Required)
                {
                    errors[filter.Name] = ["Required."];
                }

                inputs[filter.Name] = null;
                continue;
            }

            if (Check(filter, value, today) is { } problem)
            {
                errors[filter.Name] = [problem];
                continue;
            }

            inputs[filter.Name] = filter.Type == ReportFilterTypes.Date
                ? (RelativeDate(value, today) ?? DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : filter.Multiple ? string.Join(',', value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) : value;
        }

        return errors.Count > 0 ? throw new ReportInputException(errors) : inputs;
    }

    /// <summary>A relative date: <c>TODAY</c>, <c>TODAY-n</c>, <c>TODAY+n</c> (days), month and year starts.</summary>
    public static DateOnly? RelativeDate(string value, DateOnly today)
    {
        var text = value.Trim().ToUpperInvariant();
        if (text.StartsWith("TODAY", StringComparison.Ordinal) && text.Length > 5 && text[5] is '+' or '-'
            && int.TryParse(text[6..], NumberStyles.None, CultureInfo.InvariantCulture, out var days) && days <= 3660)
        {
            return today.AddDays(text[5] == '-' ? -days : days);
        }

        var month = new DateOnly(today.Year, today.Month, 1);
        return !Relative.Contains(text) ? null : text switch
        {
            "TODAY" => today,
            "MONTH_START" => month,
            "LAST_MONTH_START" => month.AddMonths(-1),
            "LAST_MONTH_END" => month.AddDays(-1),
            _ => new DateOnly(today.Year, 1, 1),
        };
    }
}

/// <summary>A report's definition as the query engine's shape.</summary>
internal static class ReportShapes
{
    public static ReportShape Of(IReadOnlyList<ReportColumnDef> columns, IReadOnlyList<ReportTileDef> tiles, IReadOnlyList<ReportSortDef> sort, bool totals) => new(
        [.. columns.Select(c => new ReportColumn(c.Name, c.Column, string.IsNullOrWhiteSpace(c.Aggregate) ? null : c.Aggregate))],
        [.. tiles.Select(t => new ReportTile(t.Name, string.IsNullOrWhiteSpace(t.Column) ? null : t.Column, t.Aggregate))],
        [.. sort.Select(s => new Platform.Reports.ReportSort(s.Column, s.Descending))],
        totals);

    public static ReportShape Of(ReportDefinition report) => Of(report.Columns, report.Tiles, report.Sort, report.Totals);

    /// <summary>A chart drawing its rows themselves (no totals on either axis).</summary>
    public const string Rows = "ROWS";

    private static bool Totalled(ReportChartField field) => !string.IsNullOrWhiteSpace(field.Aggregate);

    /// <summary>
    /// The axis holding the column the rows are grouped by: <c>X</c> or <c>Y</c> (one field, not totalled, while every
    /// field of the other axis is totalled), <see cref="Rows"/> (one field on each axis, neither totalled), or null when
    /// the axes fit neither.
    /// </summary>
    public static string? CategoryAxis(ReportChartDef chart)
    {
        bool Plain(List<ReportChartField> axis) => axis.Count == 1 && !Totalled(axis[0]);
        bool Totals(List<ReportChartField> axis) => axis.Count > 0 && axis.All(Totalled);
        return (Plain(chart.X), Plain(chart.Y)) switch
        {
            (true, true) => Rows,
            (true, false) when Totals(chart.Y) => "X",
            (false, true) when Totals(chart.X) => "Y",
            _ => null,
        };
    }

    /// <summary>
    /// A chart as the query engine's shape: the X fields (<c>x0</c>, <c>x1</c>, …), the Y fields (<c>y0</c>, …) and the
    /// series (<c>s</c>), grouped by the fields not totalled (none totalled: the rows as they are), in the chart's order;
    /// with the field each column stands for.
    /// </summary>
    public static (ReportShape Shape, IReadOnlyList<string> Paths) Chart(ReportChartDef chart)
    {
        static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        var columns = new List<ReportColumn>();
        var paths = new List<string>();
        foreach (var (field, i) in chart.X.Select((f, i) => (f, i)))
        {
            columns.Add(new($"x{i}", field.Column ?? string.Empty, Text(field.Aggregate), Text(field.Bucket)));
            paths.Add($"x[{i}]");
        }

        foreach (var (field, i) in chart.Y.Select((f, i) => (f, i)))
        {
            columns.Add(new($"y{i}", field.Column ?? string.Empty, Text(field.Aggregate), Text(field.Bucket)));
            paths.Add($"y[{i}]");
        }

        if (chart.Series is { } series)
        {
            columns.Add(new("s", series.Column ?? string.Empty, Text(series.Aggregate), Text(series.Bucket)));
            paths.Add("series");
        }

        var sort = chart.Sort switch
        {
            ReportChartSorts.XDescending => new Platform.Reports.ReportSort("x0", true),
            ReportChartSorts.YAscending => new Platform.Reports.ReportSort("y0"),
            ReportChartSorts.YDescending => new Platform.Reports.ReportSort("y0", true),
            _ => new Platform.Reports.ReportSort("x0"),
        };
        List<Platform.Reports.ReportSort> order = chart.Series is null ? [sort] : [sort, new("s")];
        return (new ReportShape(columns, [], order, Totals: false), paths);
    }
}
