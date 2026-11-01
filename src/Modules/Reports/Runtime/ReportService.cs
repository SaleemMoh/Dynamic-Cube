using System.Text.Json.Nodes;
using DynamicCube.Modules.Reports.Changes;
using DynamicCube.Modules.Reports.Domain;
using DynamicCube.Modules.Reports.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Configuration;
using DynamicCube.Platform.PublicApi;
using DynamicCube.Platform.Reports;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Reports.Runtime;

/// <summary>Reports settings (<c>Reports</c>): the time zone filters' relative dates, file times and schedules use.</summary>
public sealed class ReportsOptions
{
    public const string Section = "Reports";

    public string TimeZone { get; set; } = "Asia/Riyadh";

    public TimeZoneInfo Zone => TimeZoneInfo.FindSystemTimeZoneById(TimeZone);

    public DateTimeOffset Now(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Zone);
    }
}

/// <summary>A report in the reports list.</summary>
public sealed record ReportSummary(string Code, string NameAr, string NameEn, string? DescriptionAr, string? DescriptionEn, string? CategoryAr, string? CategoryEn, string? Icon);

/// <summary>What the viewer needs to show a report: its texts, filters, columns, tiles, charts, page size and the user's saved filters.</summary>
public sealed record ReportView(
    string Code,
    string NameAr,
    string NameEn,
    string? DescriptionAr,
    string? DescriptionEn,
    IReadOnlyList<ReportFilterDef> Filters,
    IReadOnlyList<ReportColumnDef> Columns,
    IReadOnlyList<ReportTileDef> Tiles,
    IReadOnlyList<ReportChartDef> Charts,
    int PageSize,
    bool CanExport,
    IReadOnlyList<SavedFilterView> SavedFilters);

public sealed record SavedFilterView(Guid Id, string Name, IReadOnlyDictionary<string, string?> Values, bool IsDefault);

/// <summary>Running a report: the filter values, the page and its size.</summary>
public sealed record RunReportRequest(Dictionary<string, string?>? Filters, int Page = 1, int? PageSize = null);

/// <summary>An unsaved report (the designer's preview) and the filter values to try it with.</summary>
public sealed record PreviewReportRequest(ReportPayload Report, Dictionary<string, string?>? Filters);

/// <summary>A tile with its labels and value.</summary>
public sealed record TileValue(string Name, string LabelAr, string LabelEn, JsonNode? Value);

/// <summary>An unsaved report's chart previewed in the designer, with the filter values to try it with.</summary>
public sealed record PreviewChartRequest(ReportPayload Report, string Chart, Dictionary<string, string?>? Filters);

/// <summary>
/// A chart with its rows, one per point (and series): the X fields in <c>x0</c>, <c>x1</c>, …, the Y fields in
/// <c>y0</c>, <c>y1</c>, …, the series in <c>s</c>, in the chart's order. <paramref name="Category"/> names the axis the
/// rows are grouped by (<c>X</c> or <c>Y</c>), or <c>ROWS</c> when the rows themselves are drawn.
/// </summary>
public sealed record ChartData(
    string Name,
    string TitleAr,
    string TitleEn,
    string Type,
    bool Stacked,
    string Width,
    string Category,
    IReadOnlyList<ChartFieldData> X,
    IReadOnlyList<ChartFieldData> Y,
    ChartFieldData? Series,
    IReadOnlyList<JsonObject> Rows,
    bool HasMore);

/// <summary>A field of a chart: its key in the rows, labels, the kind of value, its total and the period of a date.</summary>
public sealed record ChartFieldData(string Name, string LabelAr, string LabelEn, string Type, string? Aggregate, string? Bucket);

/// <summary>A page of a report: the columns (labels, kinds), rows, the number of rows, totals, tiles and the chart's data.</summary>
public sealed record ReportRun(
    IReadOnlyList<RunColumn> Columns,
    IReadOnlyList<JsonObject> Rows,
    long Total,
    int Page,
    int PageSize,
    bool HasMore,
    JsonObject? Totals,
    IReadOnlyList<TileValue> Tiles,
    IReadOnlyList<ChartData> Charts,
    IReadOnlyDictionary<string, string?> Filters);

public sealed record RunColumn(string Name, string LabelAr, string LabelEn, string Type, string? Aggregate);

/// <summary>Saving filter values under a name; the default ones apply when the report opens.</summary>
public sealed record SaveFilterRequest(string Name, Dictionary<string, string?> Values, bool IsDefault);

public sealed class ReportNotFoundException(string code) : Exception($"Report '{code}' does not exist or is not active.");

public sealed class ReportForbiddenException(string code) : Exception($"You may not open report '{code}'.");

/// <summary>Reports for the signed-in user (ADR-0045): the list they may open, a report's view, runs, exports and saved filters.</summary>
internal sealed class ReportService(ReportsDbContext context, IReportQueries queries, ICurrentUser user, TimeProvider clock, Microsoft.Extensions.Options.IOptions<ReportsOptions> options)
{

    public async Task<IReadOnlyList<ReportSummary>> MineAsync(CancellationToken cancellationToken)
    {
        var reports = await context.Reports.AsNoTracking().Where(r => r.IsActive).OrderBy(r => r.CategoryEn).ThenBy(r => r.NameEn).ToListAsync(cancellationToken);
        var mine = new List<ReportSummary>();
        foreach (var report in reports)
        {
            if (await queries.CanAsync(ReportViewer.Current, ReportResources.Of(report.Code), CatalogActions.View, cancellationToken))
            {
                mine.Add(new ReportSummary(report.Code, report.NameAr, report.NameEn, report.DescriptionAr, report.DescriptionEn, report.CategoryAr, report.CategoryEn, report.Icon));
            }
        }

        return mine;
    }

    public async Task<ReportView> ViewAsync(string code, CancellationToken cancellationToken)
    {
        var report = await OpenAsync(code, CatalogActions.View, cancellationToken);
        var saved = await context.SavedFilters.AsNoTracking()
            .Where(f => f.ReportCode == report.Code && f.UserName == UserName)
            .OrderByDescending(f => f.IsDefault).ThenBy(f => f.Name)
            .Select(f => new SavedFilterView(f.Id, f.Name, f.Values, f.IsDefault))
            .ToListAsync(cancellationToken);
        return new ReportView(
            report.Code,
            report.NameAr,
            report.NameEn,
            report.DescriptionAr,
            report.DescriptionEn,
            report.Filters,
            report.Columns,
            report.Tiles,
            report.Charts,
            report.PageSize,
            await queries.CanAsync(ReportViewer.Current, ReportResources.Of(report.Code), "EXPORT", cancellationToken),
            saved);
    }

    public async Task<ReportRun> RunAsync(string code, RunReportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var report = await OpenAsync(code, CatalogActions.View, cancellationToken);
        return await RunAsync(report, request, ReportViewer.Current, ReportResources.Of(report.Code), cancellationToken);
    }

    /// <summary>An unsaved report run as the designer (checked as saving would check it), within their own access.</summary>
    public async Task<ReportRun> PreviewAsync(PreviewReportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new ValidationErrors();
        await ReportApplier.CheckAsync(request.Report, errors, queries, cancellationToken);
        errors.ThrowIfAny();
        var report = new ReportDefinition { Code = "PREVIEW", NameAr = request.Report.NameAr, NameEn = request.Report.NameEn };
        ReportApplier.Fill(report, request.Report);
        return await RunAsync(report, new RunReportRequest(request.Filters, 1, Math.Min(report.PageSize, 50)), ReportViewer.Current, null, cancellationToken);
    }

    /// <summary>One chart of an unsaved report (the designer's live preview): its query and the chart checked, run within the designer's access.</summary>
    public async Task<ChartData> PreviewChartAsync(PreviewChartRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var chart = ReportApplier.ChartsOf(request.Report).FirstOrDefault(c => c.Name == request.Chart)
            ?? throw new ChangeValidationException(new Dictionary<string, string[]> { ["chart"] = ["The report has no such chart."] });
        var errors = new ValidationErrors();
        var inputs = request.Report.Filters.Select(f => f.Name).ToList();
        await ReportApplier.CheckChartsAsync([chart], request.Report.Query, inputs, errors, queries, cancellationToken);
        errors.ThrowIfAny();
        var values = ReportFilters.Resolve(request.Report.Filters, request.Filters, Today);
        return await ChartAsync(request.Report.Query?.ToJsonString() ?? "{}", chart, values, ReportViewer.Current, null, cancellationToken);
    }

    /// <summary>The report's rows (up to <see cref="ReportExports.MaxRows"/>) as Excel or CSV, in the user's language.</summary>
    public async Task<ReportFile> ExportAsync(string code, Dictionary<string, string?>? filters, string format, bool arabic, CancellationToken cancellationToken)
    {
        var report = await OpenAsync(code, "EXPORT", cancellationToken);
        var (result, inputs) = await ResultAsync(report, filters, ReportViewer.Current, ReportResources.Of(report.Code), ReportExports.MaxRows, 0, withSummary: true, cancellationToken);
        return ReportExports.Write(report, result, inputs, string.Equals(format, ReportFormats.Csv, StringComparison.OrdinalIgnoreCase) ? ReportFormats.Csv : ReportFormats.Xlsx, arabic, options.Value.Now(clock));
    }

    public async Task<SavedFilterView> SaveFilterAsync(string code, SaveFilterRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var report = await OpenAsync(code, CatalogActions.View, cancellationToken);
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 200)
        {
            throw new ReportInputException(new Dictionary<string, string[]> { ["name"] = ["Give the saved filters a name (up to 200 characters)."] });
        }

        _ = ReportFilters.Resolve(report.Filters, request.Values, Today);
        var mine = await context.SavedFilters.Where(f => f.ReportCode == report.Code && f.UserName == UserName).ToListAsync(cancellationToken);
        var entry = mine.FirstOrDefault(f => f.Name == name);
        if (entry is null)
        {
            if (mine.Count >= 50)
            {
                throw new ReportInputException(new Dictionary<string, string[]> { ["name"] = ["At most 50 saved filters a report."] });
            }

            entry = new SavedReportFilter { ReportCode = report.Code, UserName = UserName, Name = name, CreatedAt = clock.GetUtcNow() };
            context.SavedFilters.Add(entry);
        }

        entry.Values = request.Values.Where(v => !string.IsNullOrWhiteSpace(v.Value)).ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);
        entry.IsDefault = request.IsDefault;
        if (request.IsDefault)
        {
            foreach (var other in mine.Where(f => f != entry))
            {
                other.IsDefault = false;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return new SavedFilterView(entry.Id, entry.Name, entry.Values, entry.IsDefault);
    }

    public async Task DeleteFilterAsync(string code, Guid id, CancellationToken cancellationToken)
    {
        var entry = await context.SavedFilters.SingleOrDefaultAsync(f => f.Id == id && f.ReportCode == code && f.UserName == UserName, cancellationToken);
        if (entry is not null)
        {
            context.SavedFilters.Remove(entry);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>A page of the report for a viewer (the signed-in user, or a schedule's recipient), with the chart's rows.</summary>
    internal async Task<ReportRun> RunAsync(ReportDefinition report, RunReportRequest request, ReportViewer viewer, string? resource, CancellationToken cancellationToken)
    {
        var size = Math.Clamp(request.PageSize ?? report.PageSize, 1, 500);
        var page = Math.Max(1, request.Page);
        var (result, inputs) = await ResultAsync(report, request.Filters, viewer, resource, size, (page - 1) * size, withSummary: true, cancellationToken);
        var charts = new List<ChartData>();
        foreach (var chart in report.Charts)
        {
            charts.Add(await ChartAsync(report.Query, chart, inputs, viewer, resource, cancellationToken));
        }

        return new ReportRun(
            [.. result.Columns.Select(col => report.Columns.FirstOrDefault(d => d.Name == col.Name) is { } def
                ? new RunColumn(col.Name, def.LabelAr, def.LabelEn, col.Type, col.Aggregate)
                : new RunColumn(col.Name, col.Name, col.Name, col.Type, col.Aggregate))],
            result.Rows,
            result.Total,
            page,
            size,
            result.HasMore,
            result.Totals,
            [.. report.Tiles.Select(t => new TileValue(t.Name, t.LabelAr, t.LabelEn, result.Tiles?[t.Name]?.DeepClone()))],
            charts,
            inputs);
    }

    /// <summary>A chart's rows for the viewer: the filtered rows grouped by its axes (or as they are), in its order, up to its limit.</summary>
    private async Task<ChartData> ChartAsync(string query, ReportChartDef chart, IReadOnlyDictionary<string, string?> inputs, ReportViewer viewer, string? resource, CancellationToken cancellationToken)
    {
        var (shape, _) = ReportShapes.Chart(chart);
        var result = await queries.RunAsync(JsonNode.Parse(query), shape, inputs, viewer, resource, Math.Clamp(chart.Limit, 1, 500), 0, withSummary: false, cancellationToken);
        ChartFieldData Field(string name, ReportChartField field)
        {
            var column = Tail(field.Column);
            var fallbackAr = string.IsNullOrEmpty(column) ? "العدد" : column;
            var fallbackEn = string.IsNullOrEmpty(column) ? "Count" : column;
            return new ChartFieldData(
                name,
                string.IsNullOrWhiteSpace(field.LabelAr) ? fallbackAr : field.LabelAr,
                string.IsNullOrWhiteSpace(field.LabelEn) ? fallbackEn : field.LabelEn,
                result.Columns.FirstOrDefault(c => c.Name == name)?.Type ?? DataTypes.Text,
                Blank(field.Aggregate),
                Blank(field.Bucket));
        }

        return new ChartData(
            chart.Name,
            chart.TitleAr,
            chart.TitleEn,
            chart.Type,
            chart.Stacked,
            chart.Width,
            ReportShapes.CategoryAxis(chart) ?? ReportShapes.Rows,
            [.. chart.X.Select((f, i) => Field($"x{i}", f))],
            [.. chart.Y.Select((f, i) => Field($"y{i}", f))],
            chart.Series is { } s ? Field("s", s) : null,
            result.Rows,
            result.HasMore);
    }

    private static string Tail(string? column) => column is null ? string.Empty : column[(column.IndexOf('.', StringComparison.Ordinal) + 1)..];

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal async Task<(ReportResult Result, Dictionary<string, string?> Inputs)> ResultAsync(
        ReportDefinition report, IReadOnlyDictionary<string, string?>? filters, ReportViewer viewer, string? resource, int limit, int offset, bool withSummary, CancellationToken cancellationToken)
    {
        var inputs = ReportFilters.Resolve(report.Filters, filters, Today);
        var result = await queries.RunAsync(JsonNode.Parse(report.Query), ReportShapes.Of(report), inputs, viewer, resource, limit, offset, withSummary, cancellationToken);
        return (result, inputs);
    }

    private async Task<ReportDefinition> OpenAsync(string code, string action, CancellationToken cancellationToken)
    {
        var report = await context.Reports.AsNoTracking().SingleOrDefaultAsync(r => r.Code == code && r.IsActive, cancellationToken)
            ?? throw new ReportNotFoundException(code);
        return await queries.CanAsync(ReportViewer.Current, ReportResources.Of(report.Code), action, cancellationToken) ? report : throw new ReportForbiddenException(code);
    }

    private string UserName => user.UserName ?? user.UserId;

    private DateOnly Today => DateOnly.FromDateTime(options.Value.Now(clock).DateTime);
}

/// <summary>
/// In the change set's transaction: each published report is a resource of Apps Control (<c>reports.r.{code}</c>, VIEW and
/// EXPORT) under the reports list; a deleted or deactivated report's resource is deactivated.
/// </summary>
internal sealed class ReportResourceSync(IResourceRegistry registry) : IChangeSetPublishHook<ReportsDbContext>
{
    public async Task OnPublishedAsync(ReportsDbContext context, ChangeSet changeSet, CancellationToken cancellationToken)
    {
        var codes = changeSet.Items.Where(i => i.EntityType == ReportChangeTypes.Report).Select(i => i.EntityCode).Distinct(StringComparer.Ordinal).ToList();
        if (codes.Count == 0)
        {
            return;
        }

        var reports = await context.Reports.AsNoTracking().Where(r => codes.Contains(r.Code)).ToDictionaryAsync(r => r.Code, cancellationToken);
        foreach (var code in codes)
        {
            if (reports.TryGetValue(code, out var report) && report.IsActive)
            {
                await registry.SaveAsync(
                    new CatalogResource(ReportResources.Of(code), report.NameAr, report.NameEn, "INTERNAL", ReportResources.Root, [CatalogActions.View, "EXPORT"], CatalogResourceTypes.Report),
                    cancellationToken);
            }
            else
            {
                await registry.RetireAsync(ReportResources.Of(code), cancellationToken);
            }
        }
    }
}
