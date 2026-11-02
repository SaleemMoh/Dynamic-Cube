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

/// <summary>A dashboard in the dashboards list: shared ones by code, the user's own by id.</summary>
public sealed record DashboardSummary(string? Code, Guid? Id, string NameAr, string NameEn, string? DescriptionAr, string? DescriptionEn, string? Icon, bool Personal, bool IsDefault);

/// <summary>A widget as the viewer lays it out: its report, item, title, width, rows, and whether its groups open and it exports.</summary>
public sealed record DashboardWidgetView(
    string Name,
    string Kind,
    string Report,
    string ReportNameAr,
    string ReportNameEn,
    string? Item,
    string TitleAr,
    string TitleEn,
    int Span,
    int Rows,
    bool CanDrill,
    bool CanExport);

/// <summary>
/// A dashboard to show: its texts, filters and the widgets the user may see (those of reports they may view);
/// <paramref name="Hidden"/> counts the others. A personal dashboard also returns its definition to edit.
/// </summary>
public sealed record DashboardView(
    string? Code,
    Guid? Id,
    string NameAr,
    string NameEn,
    string? DescriptionAr,
    string? DescriptionEn,
    IReadOnlyList<ReportFilterDef> Filters,
    IReadOnlyList<DashboardWidgetView> Widgets,
    bool Personal,
    int Hidden,
    IReadOnlyList<DashboardWidgetDef>? Definition = null,
    bool IsDefault = false);

/// <summary>Running one widget with the dashboard's filter values.</summary>
public sealed record RunWidgetRequest(Dictionary<string, string?>? Filters);

/// <summary>The first rows of a report on a dashboard: columns, rows, how many there are, the totals row and whether the rows are groups.</summary>
public sealed record WidgetTable(IReadOnlyList<RunColumn> Columns, IReadOnlyList<JsonObject> Rows, long Total, JsonObject? Totals, bool Grouped);

/// <summary>A widget's data with the report filter values it ran with (a drill-down from it uses them).</summary>
public sealed record WidgetData(string Name, string Kind, string Report, IReadOnlyDictionary<string, string?> Filters, TileValue? Tile, ChartData? Chart, WidgetTable? Table);

/// <summary>Saving a personal dashboard: its name, filters, widgets and whether it opens first.</summary>
public sealed record SavePersonalDashboardRequest(string Name, List<ReportFilterDef>? Filters, List<DashboardWidgetDef>? Widgets, bool IsDefault);

/// <summary>Pinning a report's tile, chart or table to one of the user's dashboards, or to a new one named <paramref name="NewName"/>.</summary>
public sealed record PinWidgetRequest(Guid? Dashboard, string? NewName, DashboardWidgetDef Widget);

public sealed class DashboardNotFoundException(string key) : Exception($"Dashboard '{key}' does not exist.");

/// <summary>
/// Dashboards for the signed-in user (ADR-0047): shared ones they may view, their own, and each widget run with the
/// dashboard's filters through its report — within the user's access to that report, its data permissions, company and
/// branches.
/// </summary>
internal sealed class DashboardService(ReportsDbContext context, ReportService reports, IReportQueries queries, ICurrentUser user, TimeProvider clock)
{
    public const int MaxPersonal = 20;

    public async Task<IReadOnlyList<DashboardSummary>> MineAsync(CancellationToken cancellationToken)
    {
        var list = new List<DashboardSummary>();
        var own = await context.PersonalDashboards.AsNoTracking().Where(d => d.UserName == UserName)
            .OrderByDescending(d => d.IsDefault).ThenBy(d => d.Name).ToListAsync(cancellationToken);
        list.AddRange(own.Select(d => new DashboardSummary(null, d.Id, d.Name, d.Name, null, null, "dashboard_customize", true, d.IsDefault)));
        var shared = await context.Dashboards.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.NameEn).ToListAsync(cancellationToken);
        foreach (var dashboard in shared)
        {
            if (await queries.CanAsync(ReportViewer.Current, ReportResources.Dashboard(dashboard.Code), CatalogActions.View, cancellationToken))
            {
                list.Add(new DashboardSummary(dashboard.Code, null, dashboard.NameAr, dashboard.NameEn, dashboard.DescriptionAr, dashboard.DescriptionEn, dashboard.Icon, false, false));
            }
        }

        return list;
    }

    public async Task<DashboardView> SharedAsync(string code, CancellationToken cancellationToken)
    {
        var dashboard = await OpenSharedAsync(code, cancellationToken);
        var (widgets, hidden) = await WidgetsAsync(dashboard.Widgets, cancellationToken);
        return new DashboardView(dashboard.Code, null, dashboard.NameAr, dashboard.NameEn, dashboard.DescriptionAr, dashboard.DescriptionEn, dashboard.Filters, widgets, false, hidden);
    }

    public async Task<WidgetData> RunSharedAsync(string code, string widget, RunWidgetRequest request, CancellationToken cancellationToken)
    {
        var dashboard = await OpenSharedAsync(code, cancellationToken);
        return await RunAsync(dashboard.Filters, Widget(dashboard.Widgets, widget), request, cancellationToken);
    }

    public async Task<DashboardView> PersonalAsync(Guid id, CancellationToken cancellationToken)
    {
        var dashboard = await OpenPersonalAsync(id, cancellationToken);
        var (widgets, hidden) = await WidgetsAsync(dashboard.Widgets, cancellationToken);
        return new DashboardView(null, dashboard.Id, dashboard.Name, dashboard.Name, null, null, dashboard.Filters, widgets, true, hidden, dashboard.Widgets, dashboard.IsDefault);
    }

    public async Task<WidgetData> RunPersonalAsync(Guid id, string widget, RunWidgetRequest request, CancellationToken cancellationToken)
    {
        var dashboard = await OpenPersonalAsync(id, cancellationToken);
        return await RunAsync(dashboard.Filters, Widget(dashboard.Widgets, widget), request, cancellationToken);
    }

    /// <summary>Creates (no <paramref name="id"/>) or replaces one of the user's dashboards, checked as a shared one is.</summary>
    public async Task<DashboardView> SavePersonalAsync(Guid? id, SavePersonalDashboardRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var mine = await context.PersonalDashboards.Where(d => d.UserName == UserName).ToListAsync(cancellationToken);
        var entry = id is null ? null : mine.FirstOrDefault(d => d.Id == id) ?? throw new DashboardNotFoundException(id.Value.ToString());
        var name = request.Name?.Trim() ?? string.Empty;
        var filters = request.Filters ?? [];
        var widgets = request.Widgets ?? [];
        var errors = new ValidationErrors();
        if (name.Length is 0 or > 200)
        {
            errors.Add("name", "Give the dashboard a name (up to 200 characters).");
        }
        else if (mine.Any(d => d != entry && d.Name == name))
        {
            errors.Add("name", "You already have a dashboard with this name.");
        }

        if (entry is null && mine.Count >= MaxPersonal)
        {
            errors.Add("name", $"At most {MaxPersonal} dashboards of your own.");
        }

        await DashboardChecks.CheckAsync(filters, widgets, context, _ => false, errors, cancellationToken);
        foreach (var (widget, i) in widgets.Select((w, i) => (w, i)))
        {
            if (!string.IsNullOrWhiteSpace(widget.Report) && !await queries.CanAsync(ReportViewer.Current, ReportResources.Of(widget.Report), CatalogActions.View, cancellationToken))
            {
                errors.Add($"widgets[{i}].report", $"You may not open report '{widget.Report}'.");
            }
        }

        errors.ThrowIfAny();
        var now = clock.GetUtcNow();
        if (entry is null)
        {
            entry = new PersonalDashboard { UserName = UserName, CreatedAt = now };
            context.PersonalDashboards.Add(entry);
        }

        entry.Name = name;
        entry.Filters = DashboardChecks.Clean(filters);
        entry.Widgets = DashboardChecks.Clean(widgets);
        entry.IsDefault = request.IsDefault;
        entry.UpdatedAt = now;
        if (request.IsDefault)
        {
            foreach (var other in mine.Where(d => d != entry))
            {
                other.IsDefault = false;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return await PersonalAsync(entry.Id, cancellationToken);
    }

    /// <summary>Adds a report's tile, chart or table to one of the user's dashboards (a new one when none is given).</summary>
    public async Task<DashboardView> PinAsync(PinWidgetRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Widget);
        PersonalDashboard? target = null;
        if (request.Dashboard is { } id)
        {
            target = await context.PersonalDashboards.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id && d.UserName == UserName, cancellationToken)
                ?? throw new DashboardNotFoundException(id.ToString());
        }

        var widgets = target?.Widgets ?? [];
        var widget = request.Widget;
        var taken = widgets.Select(w => w.Name).ToHashSet(StringComparer.Ordinal);
        var stem = new string([.. (widget.Item ?? widget.Report ?? "w").Where(char.IsAsciiLetterOrDigit)]);
        stem = stem.Length == 0 || !char.IsAsciiLetter(stem[0]) ? "w" + stem : stem;
        stem = stem[..Math.Min(stem.Length, 50)];
        var name = stem;
        for (var n = 2; taken.Contains(name); n++)
        {
            name = $"{stem}{n}";
        }

        widget.Name = name;
        if (widget.Span is < 1 or > 12)
        {
            widget.Span = widget.Kind switch
            {
                DashboardWidgetKinds.Tile => 3,
                DashboardWidgetKinds.Table => 12,
                _ => 6,
            };
        }

        return await SavePersonalAsync(
            target?.Id,
            new SavePersonalDashboardRequest(target?.Name ?? request.NewName ?? string.Empty, target?.Filters, [.. widgets, widget], target?.IsDefault ?? false),
            cancellationToken);
    }

    public async Task DeletePersonalAsync(Guid id, CancellationToken cancellationToken)
    {
        var entry = await context.PersonalDashboards.SingleOrDefaultAsync(d => d.Id == id && d.UserName == UserName, cancellationToken);
        if (entry is not null)
        {
            context.PersonalDashboards.Remove(entry);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// One widget for the user: its report must be active and theirs to view; its filters take the widget's values, the
    /// bound dashboard filters (by default the one of the same name) or the report's defaults.
    /// </summary>
    private async Task<WidgetData> RunAsync(IReadOnlyList<ReportFilterDef> filters, DashboardWidgetDef widget, RunWidgetRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var report = await context.Reports.AsNoTracking().SingleOrDefaultAsync(r => r.Code == widget.Report && r.IsActive, cancellationToken)
            ?? throw new ReportNotFoundException(widget.Report);
        var resource = ReportResources.Of(report.Code);
        if (!await queries.CanAsync(ReportViewer.Current, resource, CatalogActions.View, cancellationToken))
        {
            throw new ReportForbiddenException(report.Code);
        }

        var given = ReportFilters.Resolve(filters, request.Filters, reports.Today);
        var values = Inputs(report.Filters, widget, filters, given);
        switch (widget.Kind)
        {
            case DashboardWidgetKinds.Tile:
                {
                    var tile = report.Tiles.FirstOrDefault(t => t.Name == widget.Item) ?? throw new ReportNotFoundException($"{report.Code}/{widget.Item}");
                    var (result, inputs) = await reports.ResultAsync(report, values, ReportViewer.Current, resource, 1, 0, withSummary: true, cancellationToken);
                    return new WidgetData(widget.Name, widget.Kind, report.Code, inputs, new TileValue(tile.Name, tile.LabelAr, tile.LabelEn, result.Tiles?[tile.Name]?.DeepClone()), null, null);
                }

            case DashboardWidgetKinds.Chart:
                {
                    var chart = report.Charts.FirstOrDefault(c => c.Name == widget.Item) ?? throw new ReportNotFoundException($"{report.Code}/{widget.Item}");
                    var inputs = ReportFilters.Resolve(report.Filters, values, reports.Today);
                    return new WidgetData(widget.Name, widget.Kind, report.Code, inputs, null, await reports.ChartAsync(report.Query, chart, inputs, ReportViewer.Current, resource, cancellationToken), null);
                }

            default:
                {
                    var (result, inputs) = await reports.ResultAsync(report, values, ReportViewer.Current, resource, Math.Clamp(widget.Rows, 1, 50), 0, withSummary: true, cancellationToken);
                    var columns = result.Columns.Select(col => report.Columns.FirstOrDefault(d => d.Name == col.Name) is { } def
                        ? new RunColumn(col.Name, def.LabelAr, def.LabelEn, col.Type, col.Aggregate)
                        : new RunColumn(col.Name, col.Name, col.Name, col.Type, col.Aggregate)).ToList();
                    return new WidgetData(widget.Name, widget.Kind, report.Code, inputs, null, null, new WidgetTable(columns, result.Rows, result.Total, result.Totals, ReportDrills.Grouped(report)));
                }
        }
    }

    /// <summary>A widget's report filter values: its own, else the bound dashboard filter's (the same name by default).</summary>
    internal static Dictionary<string, string?> Inputs(IReadOnlyList<ReportFilterDef> own, DashboardWidgetDef widget, IReadOnlyList<ReportFilterDef> dashboard, IReadOnlyDictionary<string, string?> given)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var filter in own)
        {
            if (widget.Values.GetValueOrDefault(filter.Name) is { Length: > 0 } fixedValue)
            {
                values[filter.Name] = fixedValue;
                continue;
            }

            var bound = widget.Bind.TryGetValue(filter.Name, out var target)
                ? target
                : dashboard.FirstOrDefault(d => d.Name == filter.Name && DashboardChecks.Fits(d, filter))?.Name;
            if (!string.IsNullOrEmpty(bound) && given.GetValueOrDefault(bound) is { Length: > 0 } value)
            {
                values[filter.Name] = value;
            }
        }

        return values;
    }

    /// <summary>The widgets of reports the user may view (with titles), and how many others are hidden.</summary>
    private async Task<(IReadOnlyList<DashboardWidgetView> Widgets, int Hidden)> WidgetsAsync(List<DashboardWidgetDef> widgets, CancellationToken cancellationToken)
    {
        var codes = widgets.Select(w => w.Report).Distinct(StringComparer.Ordinal).ToList();
        var defs = await context.Reports.AsNoTracking().Where(r => codes.Contains(r.Code) && r.IsActive).ToDictionaryAsync(r => r.Code, StringComparer.Ordinal, cancellationToken);
        var rights = new Dictionary<string, (bool View, bool Export)>(StringComparer.Ordinal);
        foreach (var code in defs.Keys)
        {
            var resource = ReportResources.Of(code);
            rights[code] = (
                await queries.CanAsync(ReportViewer.Current, resource, CatalogActions.View, cancellationToken),
                await queries.CanAsync(ReportViewer.Current, resource, "EXPORT", cancellationToken));
        }

        var shown = new List<DashboardWidgetView>();
        foreach (var widget in widgets)
        {
            if (!defs.TryGetValue(widget.Report, out var report) || !rights[report.Code].View)
            {
                continue;
            }

            var (titleAr, titleEn, drill) = widget.Kind switch
            {
                DashboardWidgetKinds.Tile => report.Tiles.FirstOrDefault(t => t.Name == widget.Item) is { } t ? (t.LabelAr, t.LabelEn, true) : (report.NameAr, report.NameEn, false),
                DashboardWidgetKinds.Chart => report.Charts.FirstOrDefault(c => c.Name == widget.Item) is { } c
                    ? (c.TitleAr, c.TitleEn, ReportShapes.CategoryAxis(c) is "X" or "Y")
                    : (report.NameAr, report.NameEn, false),
                _ => (report.NameAr, report.NameEn, ReportDrills.Grouped(report)),
            };
            shown.Add(new DashboardWidgetView(
                widget.Name,
                widget.Kind,
                report.Code,
                report.NameAr,
                report.NameEn,
                widget.Item,
                widget.TitleAr ?? titleAr,
                widget.TitleEn ?? titleEn,
                widget.Span,
                widget.Rows,
                drill && ReportDrills.Columns(report).Count > 0,
                rights[report.Code].Export));
        }

        return (shown, widgets.Count - shown.Count);
    }

    private static DashboardWidgetDef Widget(IReadOnlyList<DashboardWidgetDef> widgets, string name) =>
        widgets.FirstOrDefault(w => w.Name == name) ?? throw new DashboardNotFoundException(name);

    private async Task<DashboardDefinition> OpenSharedAsync(string code, CancellationToken cancellationToken)
    {
        var dashboard = await context.Dashboards.AsNoTracking().SingleOrDefaultAsync(d => d.Code == code && d.IsActive, cancellationToken)
            ?? throw new DashboardNotFoundException(code);
        return await queries.CanAsync(ReportViewer.Current, ReportResources.Dashboard(dashboard.Code), CatalogActions.View, cancellationToken)
            ? dashboard
            : throw new ReportForbiddenException(code);
    }

    private async Task<PersonalDashboard> OpenPersonalAsync(Guid id, CancellationToken cancellationToken) =>
        await context.PersonalDashboards.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id && d.UserName == UserName, cancellationToken)
        ?? throw new DashboardNotFoundException(id.ToString());

    private string UserName => user.UserName ?? user.UserId;
}

/// <summary>
/// In the change set's transaction: each published dashboard is a resource of Apps Control (<c>reports.d.{code}</c>,
/// VIEW) under the reports list; a deleted or deactivated dashboard's resource is retired.
/// </summary>
internal sealed class DashboardResourceSync(IResourceRegistry registry) : IChangeSetPublishHook<ReportsDbContext>
{
    public async Task OnPublishedAsync(ReportsDbContext context, ChangeSet changeSet, CancellationToken cancellationToken)
    {
        var codes = changeSet.Items.Where(i => i.EntityType == ReportChangeTypes.Dashboard).Select(i => i.EntityCode).Distinct(StringComparer.Ordinal).ToList();
        if (codes.Count == 0)
        {
            return;
        }

        var dashboards = await context.Dashboards.AsNoTracking().Where(d => codes.Contains(d.Code)).ToDictionaryAsync(d => d.Code, cancellationToken);
        foreach (var code in codes)
        {
            if (dashboards.TryGetValue(code, out var dashboard) && dashboard.IsActive)
            {
                await registry.SaveAsync(
                    new CatalogResource(ReportResources.Dashboard(code), dashboard.NameAr, dashboard.NameEn, "INTERNAL", ReportResources.Root, [CatalogActions.View], CatalogResourceTypes.Report),
                    cancellationToken);
            }
            else
            {
                await registry.RetireAsync(ReportResources.Dashboard(code), cancellationToken);
            }
        }
    }
}
