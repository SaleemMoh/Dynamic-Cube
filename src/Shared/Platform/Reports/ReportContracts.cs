using System.Text.Json.Nodes;

namespace DynamicCube.Platform.Reports;

/// <summary>How a report column is totalled: none (the rows are grouped by it when another column is totalled) or one of these.</summary>
public static class ReportAggregates
{
    public const string Count = "COUNT";
    public const string CountDistinct = "COUNT_DISTINCT";
    public const string Sum = "SUM";
    public const string Avg = "AVG";
    public const string Min = "MIN";
    public const string Max = "MAX";

    public static IReadOnlyList<string> All { get; } = [Count, CountDistinct, Sum, Avg, Min, Max];
}

/// <summary>Periods a date column is grouped by (a chart's X axis: requests per month).</summary>
public static class ReportBuckets
{
    public const string Day = "DAY";
    public const string Week = "WEEK";
    public const string Month = "MONTH";
    public const string Quarter = "QUARTER";
    public const string Year = "YEAR";

    public static IReadOnlyList<string> All { get; } = [Day, Week, Month, Quarter, Year];
}

/// <summary>
/// A column of a report: its name in the result, the column (<c>alias.column</c>) it reads, how it is totalled, and for a
/// date column grouped by, the period it is grouped by (<see cref="ReportBuckets"/>). Counting rows needs no column.
/// </summary>
public sealed record ReportColumn(string Name, string Column, string? Aggregate = null, string? Bucket = null);

/// <summary>A number shown above the report: an aggregate over all the rows the filters keep (no column: the number of rows).</summary>
public sealed record ReportTile(string Name, string? Column, string Aggregate);

/// <summary>A sort on a column of the result.</summary>
public sealed record ReportSort(string Column, bool Descending = false);

/// <summary>
/// A condition a drill-down adds (ADR-0047): the column (<c>alias.column</c>), grouped by a period when
/// <paramref name="Bucket"/> is set, equals the value of the group clicked (null: the column is empty).
/// </summary>
public sealed record ReportMatch(string Column, string? Bucket, JsonNode? Value);

/// <summary>
/// What a report returns over its query: columns (grouped when one is totalled), tiles, the sort and a totals row; a
/// drill-down adds the group's values as <paramref name="Matches"/>.
/// </summary>
public sealed record ReportShape(IReadOnlyList<ReportColumn> Columns, IReadOnlyList<ReportTile> Tiles, IReadOnlyList<ReportSort> Sort, bool Totals, IReadOnlyList<ReportMatch>? Matches = null);

/// <summary>
/// Who a report runs for: the signed-in user (null <paramref name="UserName"/>), or a stored user (a scheduled report sent
/// to them), in a company (null: their usual one). Their data permissions and company and branches always apply.
/// </summary>
public sealed record ReportViewer(string? UserName = null, string? Company = null)
{
    public static ReportViewer Current { get; } = new();
}

/// <summary>A column of the result with the kind of value it holds (<see cref="PublicApi.DataTypes"/>).</summary>
public sealed record ReportResultColumn(string Name, string Type, string? Aggregate);

/// <summary>One page of a report: its rows, whether more follow, how many there are, the totals row and the tiles.</summary>
public sealed record ReportResult(
    IReadOnlyList<ReportResultColumn> Columns,
    IReadOnlyList<JsonObject> Rows,
    bool HasMore,
    long Total,
    JsonObject? Totals,
    JsonObject? Tiles);

/// <summary>
/// Report queries (implemented by Apps Control, ADR-0045): definitions built in the visual query builder over registered
/// tables and classes, returning chosen columns grouped and totalled. Conditions may compare with the report's filters
/// (inputs) and the user's data; the viewer's data permissions and company and branches apply. Runs are read-only.
/// </summary>
public interface IReportQueries
{
    /// <summary>What is wrong with the definition (field → messages); empty when it can run.</summary>
    Task<IReadOnlyDictionary<string, string[]>> CheckAsync(JsonNode? query, ReportShape shape, IReadOnlyCollection<string> inputs, CancellationToken cancellationToken);

    /// <summary>The columns of the result with their kinds; empty when the definition has problems.</summary>
    Task<IReadOnlyList<ReportResultColumn>> DescribeAsync(JsonNode? query, ReportShape shape, IReadOnlyCollection<string> inputs, CancellationToken cancellationToken);

    /// <summary>
    /// One page of rows for the viewer, with the number of rows, the totals and the tiles when asked; throws
    /// <see cref="ReportInputException"/> when a filter's value does not fit its column and
    /// <see cref="ReportAccessException"/> when a stored viewer cannot be found or is not active.
    /// </summary>
    Task<ReportResult> RunAsync(
        JsonNode? query,
        ReportShape shape,
        IReadOnlyDictionary<string, string?> inputs,
        ReportViewer viewer,
        string? resource,
        int limit,
        int offset,
        bool withSummary,
        CancellationToken cancellationToken);

    /// <summary>Whether the viewer holds the action on the resource (a report's screen).</summary>
    Task<bool> CanAsync(ReportViewer viewer, string resource, string action, CancellationToken cancellationToken);
}

/// <summary>Filter values a report cannot use (field → messages).</summary>
public sealed class ReportInputException : Exception
{
    public ReportInputException()
        : this(new Dictionary<string, string[]>())
    {
    }

    public ReportInputException(string message)
        : this(new Dictionary<string, string[]> { ["filters"] = [message] })
    {
    }

    public ReportInputException(string message, Exception innerException)
        : base(message, innerException)
    {
        Errors = new Dictionary<string, string[]> { ["filters"] = [message] };
    }

    public ReportInputException(IReadOnlyDictionary<string, string[]> errors)
        : base("The filters are invalid.")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

/// <summary>The viewer a report runs for cannot be found or is not active.</summary>
public sealed class ReportAccessException : Exception
{
    public ReportAccessException()
    {
    }

    public ReportAccessException(string message)
        : base(message)
    {
    }

    public ReportAccessException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
