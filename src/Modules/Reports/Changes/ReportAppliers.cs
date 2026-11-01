using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DynamicCube.Modules.Reports.Domain;
using DynamicCube.Modules.Reports.Persistence;
using DynamicCube.Modules.Reports.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.PublicApi;
using DynamicCube.Platform.Reports;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Reports.Changes;

public sealed class ReportPayload : ConfigPayload
{
    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    public string? CategoryAr { get; set; }

    public string? CategoryEn { get; set; }

    public string? Icon { get; set; }

    public JsonNode? Query { get; set; }

    public List<ReportColumnDef> Columns { get; set; } = [];

    public List<ReportFilterDef> Filters { get; set; } = [];

    public List<ReportTileDef> Tiles { get; set; } = [];

    public List<ReportSortDef> Sort { get; set; } = [];

    public bool Totals { get; set; }

    public ReportChartDef? Chart { get; set; }

    public int PageSize { get; set; } = 50;
}

internal sealed partial class ReportApplier(ReportsDbContext db, IReportQueries queries)
    : ConfigApplier<ReportsDbContext, ConfigRefs<ReportsDbContext>, ReportDefinition, ReportPayload>(db)
{
    public const int MaxFilters = 12;

    public override string EntityType => ReportChangeTypes.Report;

    public override string Area => ReportChangeTypes.Area;

    public override string ResourceCode => ReportResources.Config;

    protected override Regex CodePattern => ValidationErrors.IdentifierCode();

    protected override ConfigRefs<ReportsDbContext> CreateRefs(ReportsDbContext context, ChangeSet? draft) => new(context, draft);

    protected override IQueryable<ReportDefinition> Query(ReportsDbContext context) => context.Reports;

    protected override async Task ApplyAsync(ReportDefinition entity, ReportPayload payload, ConfigRefs<ReportsDbContext> refs, ValidationErrors errors, ReportDefinition? existing, CancellationToken cancellationToken)
    {
        await CheckAsync(payload, errors, queries, cancellationToken);
        Fill(entity, payload);
    }

    /// <summary>The checks of a report's own fields and query (also made before an unsaved report is previewed).</summary>
    internal static async Task CheckAsync(ReportPayload payload, ValidationErrors errors, IReportQueries queries, CancellationToken cancellationToken)
    {
        errors.MaxLength("descriptionAr", payload.DescriptionAr, 2000);
        errors.MaxLength("descriptionEn", payload.DescriptionEn, 2000);
        errors.MaxLength("categoryAr", payload.CategoryAr, 200);
        errors.MaxLength("categoryEn", payload.CategoryEn, 200);
        if (payload.PageSize is < 10 or > 500)
        {
            errors.Add("pageSize", "Between 10 and 500 rows a page.");
        }

        foreach (var (column, i) in payload.Columns.Select((c, i) => (c, i)))
        {
            errors.Required($"columns[{i}].labelAr", column.LabelAr);
            errors.Required($"columns[{i}].labelEn", column.LabelEn);
        }

        // Filters: names the query's conditions compare with.
        if (payload.Filters.Count > MaxFilters)
        {
            errors.Add("filters", $"Use at most {MaxFilters} filters.");
        }

        ChildSync.RequireUnique(payload.Filters, f => f.Name, "filters", errors);
        foreach (var (filter, i) in payload.Filters.Select((f, i) => (f, i)))
        {
            var path = $"filters[{i}]";
            if (!FilterName().IsMatch(filter.Name ?? string.Empty))
            {
                errors.Add($"{path}.name", "Use a name of English letters, digits and _, starting with a letter.");
            }

            errors.Required($"{path}.labelAr", filter.LabelAr);
            errors.Required($"{path}.labelEn", filter.LabelEn);
            if (!ReportFilterTypes.All.Contains(filter.Type))
            {
                errors.Add($"{path}.type", "Text, number, date, a list of values or yes/no.");
            }

            if (filter.Type == ReportFilterTypes.List)
            {
                errors.Required($"{path}.lov", filter.Lov);
            }
            else if (filter.Multiple)
            {
                errors.Add($"{path}.multiple", "Only a list filter takes several values.");
            }

            if (!string.IsNullOrWhiteSpace(filter.Default) && ReportFilters.Check(filter, filter.Default, DateOnly.FromDateTime(DateTime.UtcNow)) is { } problem)
            {
                errors.Add($"{path}.default", problem);
            }
        }

        foreach (var (tile, i) in payload.Tiles.Select((t, i) => (t, i)))
        {
            errors.Required($"tiles[{i}].labelAr", tile.LabelAr);
            errors.Required($"tiles[{i}].labelEn", tile.LabelEn);
        }

        var shape = ReportShapes.Of(payload.Columns, payload.Tiles, payload.Sort, payload.Totals);
        var inputs = payload.Filters.Select(f => f.Name).ToList();
        var problems = await queries.CheckAsync(payload.Query, shape, inputs, cancellationToken);
        foreach (var (field, messages) in problems)
        {
            foreach (var message in messages)
            {
                errors.Add(field, message);
            }
        }

        // The chart draws number columns along one of the report's columns.
        if (payload.Chart is { } chart)
        {
            if (!ReportChartTypes.All.Contains(chart.Type))
            {
                errors.Add("chart.type", "Bars, a line or a pie.");
            }

            var names = payload.Columns.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
            if (!names.Contains(chart.Category))
            {
                errors.Add("chart.category", "Choose one of the report's columns.");
            }

            if (chart.Values.Count == 0 || (chart.Type == ReportChartTypes.Pie && chart.Values.Count > 1))
            {
                errors.Add("chart.values", chart.Type == ReportChartTypes.Pie ? "A pie draws one number column." : "Choose the number columns to draw.");
            }

            if (problems.Count == 0)
            {
                var types = (await queries.DescribeAsync(payload.Query, shape, inputs, cancellationToken)).ToDictionary(c => c.Name, c => c.Type, StringComparer.Ordinal);
                foreach (var value in chart.Values.Where(v => types.GetValueOrDefault(v) != DataTypes.Number))
                {
                    errors.Add("chart.values", $"'{value}' is not a number column of the report.");
                }
            }
        }
    }

    internal static void Fill(ReportDefinition entity, ReportPayload payload)
    {
        entity.DescriptionAr = Blank(payload.DescriptionAr);
        entity.DescriptionEn = Blank(payload.DescriptionEn);
        entity.CategoryAr = Blank(payload.CategoryAr);
        entity.CategoryEn = Blank(payload.CategoryEn);
        entity.Icon = Blank(payload.Icon);
        entity.Query = payload.Query?.ToJsonString() ?? "{}";
        entity.Columns = payload.Columns;
        entity.Filters = [.. payload.Filters.Select(f => { f.Default = Blank(f.Default); f.Lov = f.Type == ReportFilterTypes.List ? Blank(f.Lov) : null; return f; })];
        entity.Tiles = payload.Tiles;
        entity.Sort = payload.Sort;
        entity.Totals = payload.Totals;
        entity.Chart = payload.Chart;
        entity.PageSize = payload.PageSize;
    }

    protected override Task<ReportPayload> ToPayloadAsync(ReportsDbContext context, ReportDefinition entity, CancellationToken cancellationToken) =>
        Task.FromResult(new ReportPayload
        {
            DescriptionAr = entity.DescriptionAr,
            DescriptionEn = entity.DescriptionEn,
            CategoryAr = entity.CategoryAr,
            CategoryEn = entity.CategoryEn,
            Icon = entity.Icon,
            Query = JsonNode.Parse(entity.Query),
            Columns = entity.Columns,
            Filters = entity.Filters,
            Tiles = entity.Tiles,
            Sort = entity.Sort,
            Totals = entity.Totals,
            Chart = entity.Chart,
            PageSize = entity.PageSize,
        });

    protected override async Task BeforeDeleteAsync(ReportsDbContext context, ReportDefinition entity, CancellationToken cancellationToken)
    {
        if (await context.Schedules.AnyAsync(s => s.ReportCode == entity.Code, cancellationToken))
        {
            throw new ChangeSetException($"Report '{entity.Code}' still has schedules.");
        }
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{0,63}$")]
    private static partial Regex FilterName();
}

public sealed class ReportSchedulePayload : ConfigPayload
{
    public string? ReportCode { get; set; }

    public string Frequency { get; set; } = ScheduleFrequencies.Daily;

    public string Time { get; set; } = "07:00";

    public int? DayOfWeek { get; set; }

    public int? DayOfMonth { get; set; }

    public string Format { get; set; } = ReportFormats.Xlsx;

    public string Language { get; set; } = "AR";

    public List<string> Users { get; set; } = [];

    public List<string> Roles { get; set; } = [];

    public Dictionary<string, string?> FilterValues { get; set; } = [];
}

internal sealed partial class ReportScheduleApplier(ReportsDbContext db)
    : ConfigApplier<ReportsDbContext, ConfigRefs<ReportsDbContext>, ReportSchedule, ReportSchedulePayload>(db)
{
    public override string EntityType => ReportChangeTypes.Schedule;

    public override string Area => ReportChangeTypes.Area;

    public override string ResourceCode => ReportResources.Schedules;

    protected override Regex CodePattern => ValidationErrors.IdentifierCode();

    protected override ConfigRefs<ReportsDbContext> CreateRefs(ReportsDbContext context, ChangeSet? draft) => new(context, draft);

    protected override IQueryable<ReportSchedule> Query(ReportsDbContext context) => context.Schedules;

    protected override async Task ApplyAsync(ReportSchedule entity, ReportSchedulePayload payload, ConfigRefs<ReportsDbContext> refs, ValidationErrors errors, ReportSchedule? existing, CancellationToken cancellationToken)
    {
        errors.Required("reportCode", payload.ReportCode);
        var report = string.IsNullOrWhiteSpace(payload.ReportCode)
            ? null
            : await refs.Context.Reports.AsNoTracking().SingleOrDefaultAsync(r => r.Code == payload.ReportCode, cancellationToken);
        if (!string.IsNullOrWhiteSpace(payload.ReportCode) && report is null && !refs.IsPendingCreate(ReportChangeTypes.Report, payload.ReportCode))
        {
            errors.Add("reportCode", $"Report '{payload.ReportCode}' does not exist.");
        }

        if (!ScheduleFrequencies.All.Contains(payload.Frequency))
        {
            errors.Add("frequency", "Daily, weekly or monthly.");
        }

        if (!TimePattern().IsMatch(payload.Time ?? string.Empty))
        {
            errors.Add("time", "A time such as 07:30 (24 hours).");
        }

        if (payload.Frequency == ScheduleFrequencies.Weekly && payload.DayOfWeek is not (>= 0 and <= 6))
        {
            errors.Add("dayOfWeek", "Choose the day of the week.");
        }

        if (payload.Frequency == ScheduleFrequencies.Monthly && payload.DayOfMonth is not (>= 1 and <= 28))
        {
            errors.Add("dayOfMonth", "A day from 1 to 28.");
        }

        if (!ReportFormats.All.Contains(payload.Format))
        {
            errors.Add("format", "Excel (XLSX) or CSV.");
        }

        if (payload.Language is not ("AR" or "EN"))
        {
            errors.Add("language", "AR or EN.");
        }

        var users = payload.Users.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).Distinct(StringComparer.Ordinal).ToList();
        var roles = payload.Roles.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).Distinct(StringComparer.Ordinal).ToList();
        if (users.Count + roles.Count == 0)
        {
            errors.Add("users", "Choose who receives the report: users or roles.");
        }

        if (users.Count > 200 || roles.Count > 20)
        {
            errors.Add("users", "At most 200 users and 20 roles.");
        }

        if (report is not null)
        {
            var names = report.Filters.ToDictionary(f => f.Name, StringComparer.Ordinal);
            foreach (var (name, value) in payload.FilterValues)
            {
                if (!names.TryGetValue(name, out var filter))
                {
                    errors.Add($"filterValues.{name}", $"The report has no filter '{name}'.");
                }
                else if (!string.IsNullOrWhiteSpace(value) && ReportFilters.Check(filter, value, DateOnly.FromDateTime(DateTime.UtcNow)) is { } problem)
                {
                    errors.Add($"filterValues.{name}", problem);
                }
            }
        }

        entity.ReportCode = payload.ReportCode?.Trim() ?? string.Empty;
        entity.Frequency = payload.Frequency;
        entity.Time = payload.Time ?? "07:00";
        entity.DayOfWeek = payload.Frequency == ScheduleFrequencies.Weekly ? payload.DayOfWeek : null;
        entity.DayOfMonth = payload.Frequency == ScheduleFrequencies.Monthly ? payload.DayOfMonth : null;
        entity.Format = payload.Format;
        entity.Language = payload.Language;
        entity.Users = users;
        entity.Roles = roles;
        entity.FilterValues = payload.FilterValues.Where(v => !string.IsNullOrWhiteSpace(v.Value)).ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);
    }

    protected override Task<ReportSchedulePayload> ToPayloadAsync(ReportsDbContext context, ReportSchedule entity, CancellationToken cancellationToken) =>
        Task.FromResult(new ReportSchedulePayload
        {
            ReportCode = entity.ReportCode,
            Frequency = entity.Frequency,
            Time = entity.Time,
            DayOfWeek = entity.DayOfWeek,
            DayOfMonth = entity.DayOfMonth,
            Format = entity.Format,
            Language = entity.Language,
            Users = entity.Users,
            Roles = entity.Roles,
            FilterValues = entity.FilterValues,
        });

    [GeneratedRegex("^([01][0-9]|2[0-3]):[0-5][0-9]$")]
    private static partial Regex TimePattern();
}
