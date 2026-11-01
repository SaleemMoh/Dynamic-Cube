namespace DynamicCube.Modules.Reports.Domain;

/// <summary>Kinds of report filters.</summary>
public static class ReportFilterTypes
{
    public const string Text = "TEXT";
    public const string Number = "NUMBER";
    public const string Date = "DATE";

    /// <summary>Values of a list of values (Apps Control), one or several.</summary>
    public const string List = "LIST";
    public const string YesNo = "YES_NO";

    public static IReadOnlyList<string> All { get; } = [Text, Number, Date, List, YesNo];
}

/// <summary>Chart kinds.</summary>
public static class ReportChartTypes
{
    public const string Bar = "BAR";
    public const string Line = "LINE";
    public const string Pie = "PIE";

    public static IReadOnlyList<string> All { get; } = [Bar, Line, Pie];
}

/// <summary>
/// A report (ADR-0045): a query built in the visual query builder, the columns it shows (grouped when some are totalled),
/// the filters users fill, tiles, a chart and a totals row. Configuration, edited through change sets; each report is a
/// resource in Apps Control (<c>reports.r.{code}</c>) roles grant and menus open.
/// </summary>
public sealed class ReportDefinition : ConfigEntity
{
    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    /// <summary>Groups reports in the reports list (free text, e.g. "Requests").</summary>
    public string? CategoryAr { get; set; }

    public string? CategoryEn { get; set; }

    public string? Icon { get; set; }

    /// <summary>The query (the query builder's definition) as JSON.</summary>
    public string Query { get; set; } = "{}";

    public List<ReportColumnDef> Columns { get; set; } = [];

    public List<ReportFilterDef> Filters { get; set; } = [];

    public List<ReportTileDef> Tiles { get; set; } = [];

    public List<ReportSortDef> Sort { get; set; } = [];

    public bool Totals { get; set; }

    public ReportChartDef? Chart { get; set; }

    public int PageSize { get; set; } = 50;
}

/// <summary>A column: its name, labels, the column it reads (<c>alias.column</c>) and its aggregate (none: grouped by it).</summary>
public sealed class ReportColumnDef
{
    public string Name { get; set; } = string.Empty;

    public string LabelAr { get; set; } = string.Empty;

    public string LabelEn { get; set; } = string.Empty;

    public string Column { get; set; } = string.Empty;

    public string? Aggregate { get; set; }
}

/// <summary>
/// A filter users fill: its name (what the query's conditions compare with), labels, kind, the list of values of a list
/// filter, whether several values may be chosen, whether it must be filled and its default (a value, or <c>TODAY</c>,
/// <c>TODAY-30</c>, <c>MONTH_START</c>, <c>LAST_MONTH_START</c>, <c>LAST_MONTH_END</c>, <c>YEAR_START</c> for dates).
/// </summary>
public sealed class ReportFilterDef
{
    public string Name { get; set; } = string.Empty;

    public string LabelAr { get; set; } = string.Empty;

    public string LabelEn { get; set; } = string.Empty;

    public string Type { get; set; } = ReportFilterTypes.Text;

    public string? Lov { get; set; }

    public bool Multiple { get; set; }

    public bool Required { get; set; }

    public string? Default { get; set; }
}

/// <summary>A number above the report: an aggregate over the filtered rows (no column: their number).</summary>
public sealed class ReportTileDef
{
    public string Name { get; set; } = string.Empty;

    public string LabelAr { get; set; } = string.Empty;

    public string LabelEn { get; set; } = string.Empty;

    public string? Column { get; set; }

    public string Aggregate { get; set; } = Platform.Reports.ReportAggregates.Count;
}

public sealed class ReportSortDef
{
    public string Column { get; set; } = string.Empty;

    public bool Descending { get; set; }
}

/// <summary>A chart of the report's rows: its kind, the column along the axis and the number columns drawn.</summary>
public sealed class ReportChartDef
{
    public string Type { get; set; } = ReportChartTypes.Bar;

    public string Category { get; set; } = string.Empty;

    public List<string> Values { get; set; } = [];
}

/// <summary>Filter values a user saved for a report, to apply again in one click; one may be their default.</summary>
public sealed class SavedReportFilter
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string ReportCode { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public Dictionary<string, string?> Values { get; set; } = [];

    public bool IsDefault { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>How often a schedule sends its report.</summary>
public static class ScheduleFrequencies
{
    public const string Daily = "DAILY";
    public const string Weekly = "WEEKLY";
    public const string Monthly = "MONTHLY";

    public static IReadOnlyList<string> All { get; } = [Daily, Weekly, Monthly];
}

public static class ReportFormats
{
    public const string Xlsx = "XLSX";
    public const string Csv = "CSV";

    public static IReadOnlyList<string> All { get; } = [Xlsx, Csv];
}

/// <summary>
/// A scheduled delivery (ADR-0045): the report, when (daily, a day of the week or of the month, at a time), the file
/// format, the filter values and the recipients — users and the holders of roles. Each recipient gets the report run with
/// their own access, by email. Configuration, edited through change sets.
/// </summary>
public sealed class ReportSchedule : ConfigEntity
{
    public string ReportCode { get; set; } = string.Empty;

    public string Frequency { get; set; } = ScheduleFrequencies.Daily;

    /// <summary>Local time, <c>HH:mm</c>.</summary>
    public string Time { get; set; } = "07:00";

    /// <summary>Weekly: 0 Sunday … 6 Saturday.</summary>
    public int? DayOfWeek { get; set; }

    /// <summary>Monthly: 1 … 28.</summary>
    public int? DayOfMonth { get; set; }

    public string Format { get; set; } = ReportFormats.Xlsx;

    /// <summary>The language of the file and the email: <c>AR</c> or <c>EN</c>.</summary>
    public string Language { get; set; } = "AR";

    public List<string> Users { get; set; } = [];

    public List<string> Roles { get; set; } = [];

    public Dictionary<string, string?> FilterValues { get; set; } = [];

    /// <summary>When it last ran (the slot it covered); the next run is the first slot after it.</summary>
    public DateTimeOffset? LastRunAt { get; set; }
}

/// <summary>What a schedule's run did for each recipient.</summary>
public sealed class ReportDeliveryRecipient
{
    public string UserName { get; set; } = string.Empty;

    /// <summary><c>SENT</c>, <c>SKIPPED</c> (no access, no email, inactive) or <c>FAILED</c>.</summary>
    public string Outcome { get; set; } = string.Empty;

    public string? Message { get; set; }

    public int Rows { get; set; }
}

/// <summary>A run of a schedule, with its recipients' outcomes. Not configuration; written by the scheduler.</summary>
public sealed class ReportDelivery
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string ScheduleCode { get; set; } = string.Empty;

    public string ReportCode { get; set; } = string.Empty;

    public DateTimeOffset DueAt { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public int Sent { get; set; }

    public int Skipped { get; set; }

    public int Failed { get; set; }

    public List<ReportDeliveryRecipient> Recipients { get; set; } = [];

    public string? Error { get; set; }
}
