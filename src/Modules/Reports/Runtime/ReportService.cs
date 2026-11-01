using System.Text.Json.Nodes;
using DynamicCube.Modules.Reports.Changes;
using DynamicCube.Modules.Reports.Domain;
using DynamicCube.Modules.Reports.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Configuration;
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

/// <summary>What the viewer needs to show a report: its texts, filters, columns, tiles, chart, page size and the user's saved filters.</summary>
public sealed record ReportView(
    string Code,
    string NameAr,
    string NameEn,
    string? DescriptionAr,
    string? DescriptionEn,
    IReadOnlyList<ReportFilterDef> Filters,
    IReadOnlyList<ReportColumnDef> Columns,
    IReadOnlyList<ReportTileDef> Tiles,
    ReportChartDef? Chart,
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

/// <summary>The chart's rows: up to 100 rows of the report (for a grouped report, the groups) in its sort.</summary>
public sealed record ChartData(string Type, string Category, IReadOnlyList<string> Values, IReadOnlyList<JsonObject> Rows);

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
    ChartData? Chart,
    IReadOnlyDictionary<string, string?> Filters);

public sealed record RunColumn(string Name, string LabelAr, string LabelEn, string Type, string? Aggregate);

/// <summary>Saving filter values under a name; the default ones apply when the report opens.</summary>
public sealed record SaveFilterRequest(string Name, Dictionary<string, string?> Values, bool IsDefault);

public sealed class ReportNotFoundException(string code) : Exception($"Report '{code}' does not exist or is not active.");

public sealed class ReportForbiddenException(string code) : Exception($"You may not open report '{code}'.");

/// <summary>Reports for the signed-in user (ADR-0045): the list they may open, a report's view, runs, exports and saved filters.</summary>
internal sealed class ReportService(ReportsDbContext context, IReportQueries queries, ICurrentUser user, TimeProvider clock, Microsoft.Extensions.Options.IOptions<ReportsOptions> options)
{
    public const int ChartRows = 100;

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
            report.Chart,
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
        ChartData? chart = null;
        if (report.Chart is { } c)
        {
            // Grouped by more than the chart's column: the chart groups by its column alone (each total recomputed, so an
            // average stays an average); otherwise it draws the report's own first rows.
            var regroup = report.Columns.Any(x => !string.IsNullOrWhiteSpace(x.Aggregate))
                && report.Columns.Any(x => string.IsNullOrWhiteSpace(x.Aggregate) && x.Name != c.Category);
            IReadOnlyList<JsonObject> rows;
            if (regroup)
            {
                var columns = report.Columns.Where(x => x.Name == c.Category || c.Values.Contains(x.Name)).ToList();
                var sort = report.Sort.Where(s => columns.Any(x => x.Name == s.Column)).ToList();
                var shape = ReportShapes.Of(columns, [], sort, totals: false);
                rows = (await queries.RunAsync(JsonNode.Parse(report.Query), shape, inputs, viewer, resource, ChartRows, 0, withSummary: false, cancellationToken)).Rows;
            }
            else
            {
                rows = page == 1 && size >= ChartRows
                    ? [.. result.Rows.Take(ChartRows)]
                    : (await queries.RunAsync(JsonNode.Parse(report.Query), ReportShapes.Of(report), inputs, viewer, resource, ChartRows, 0, withSummary: false, cancellationToken)).Rows;
            }

            chart = new ChartData(c.Type, c.Category, c.Values, [.. rows.Select(r => r.DeepClone().AsObject())]);
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
            chart,
            inputs);
    }

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
