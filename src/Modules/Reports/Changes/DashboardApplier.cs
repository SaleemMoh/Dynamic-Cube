using System.Text.RegularExpressions;
using DynamicCube.Modules.Reports.Domain;
using DynamicCube.Modules.Reports.Persistence;
using DynamicCube.Modules.Reports.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Configuration;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Reports.Changes;

public sealed class DashboardPayload : ConfigPayload
{
    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    public string? Icon { get; set; }

    public List<ReportFilterDef> Filters { get; set; } = [];

    public List<DashboardWidgetDef> Widgets { get; set; } = [];
}

/// <summary>Shared dashboards (ADR-0047), edited through change sets; their widgets are checked against their reports.</summary>
internal sealed class DashboardApplier(ReportsDbContext db)
    : ConfigApplier<ReportsDbContext, ConfigRefs<ReportsDbContext>, DashboardDefinition, DashboardPayload>(db)
{
    public override string EntityType => ReportChangeTypes.Dashboard;

    public override string Area => ReportChangeTypes.Area;

    public override string ResourceCode => ReportResources.Dashboards;

    protected override Regex CodePattern => ValidationErrors.IdentifierCode();

    protected override ConfigRefs<ReportsDbContext> CreateRefs(ReportsDbContext context, ChangeSet? draft) => new(context, draft);

    protected override IQueryable<DashboardDefinition> Query(ReportsDbContext context) => context.Dashboards;

    protected override async Task ApplyAsync(DashboardDefinition entity, DashboardPayload payload, ConfigRefs<ReportsDbContext> refs, ValidationErrors errors, DashboardDefinition? existing, CancellationToken cancellationToken)
    {
        errors.MaxLength("descriptionAr", payload.DescriptionAr, 2000);
        errors.MaxLength("descriptionEn", payload.DescriptionEn, 2000);
        await DashboardChecks.CheckAsync(payload.Filters, payload.Widgets, refs.Context, code => refs.IsPendingCreate(ReportChangeTypes.Report, code), errors, cancellationToken);
        entity.DescriptionAr = Blank(payload.DescriptionAr);
        entity.DescriptionEn = Blank(payload.DescriptionEn);
        entity.Icon = Blank(payload.Icon);
        entity.Filters = DashboardChecks.Clean(payload.Filters);
        entity.Widgets = DashboardChecks.Clean(payload.Widgets);
    }

    protected override Task<DashboardPayload> ToPayloadAsync(ReportsDbContext context, DashboardDefinition entity, CancellationToken cancellationToken) =>
        Task.FromResult(new DashboardPayload
        {
            DescriptionAr = entity.DescriptionAr,
            DescriptionEn = entity.DescriptionEn,
            Icon = entity.Icon,
            Filters = entity.Filters,
            Widgets = entity.Widgets,
        });

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>The checks of a dashboard's filters and widgets, shared by shared and personal dashboards.</summary>
internal static partial class DashboardChecks
{
    public const int MaxWidgets = 24;

    /// <summary>
    /// Filters as a report's; widgets: a unique name, a kind, an active report (or one created in the same change set),
    /// the tile or chart it names, a width of 1–12 twelfths, 1–50 rows for a table, and filters bound to dashboard
    /// filters of the same kind (a list filter to the same list) or given values that fit.
    /// </summary>
    public static async Task CheckAsync(
        IReadOnlyList<ReportFilterDef> filters,
        IReadOnlyList<DashboardWidgetDef> widgets,
        ReportsDbContext context,
        Func<string, bool> pendingReport,
        ValidationErrors errors,
        CancellationToken cancellationToken)
    {
        ReportApplier.CheckFilters(filters, errors);
        if (widgets.Count == 0)
        {
            errors.Add("widgets", "Add a tile, a chart or a table.");
        }

        if (widgets.Count > MaxWidgets)
        {
            errors.Add("widgets", $"Use at most {MaxWidgets} widgets.");
        }

        ChildSync.RequireUnique(widgets, w => w.Name, "widgets", errors);
        var codes = widgets.Select(w => w.Report).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.Ordinal).ToList();
        var reports = await context.Reports.AsNoTracking().Where(r => codes.Contains(r.Code)).ToDictionaryAsync(r => r.Code, StringComparer.Ordinal, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var (widget, i) in widgets.Select((w, i) => (w, i)))
        {
            var path = $"widgets[{i}]";
            if (!Name().IsMatch(widget.Name ?? string.Empty))
            {
                errors.Add($"{path}.name", "Use a name of English letters, digits and _, starting with a letter.");
            }

            if (!DashboardWidgetKinds.All.Contains(widget.Kind))
            {
                errors.Add($"{path}.kind", "A tile, a chart or a table.");
            }

            if (widget.Span is < 1 or > 12)
            {
                errors.Add($"{path}.span", "A width of 1 to 12 twelfths of the row.");
            }

            if (widget.Kind == DashboardWidgetKinds.Table && widget.Rows is < 1 or > 50)
            {
                errors.Add($"{path}.rows", "Between 1 and 50 rows.");
            }

            errors.MaxLength($"{path}.titleAr", widget.TitleAr, 200);
            errors.MaxLength($"{path}.titleEn", widget.TitleEn, 200);
            if (string.IsNullOrWhiteSpace(widget.Report))
            {
                errors.Add($"{path}.report", "Choose the report.");
                continue;
            }

            if (!reports.TryGetValue(widget.Report, out var report) || !report.IsActive)
            {
                if (report is null && pendingReport(widget.Report))
                {
                    continue;
                }

                errors.Add($"{path}.report", $"Report '{widget.Report}' does not exist or is not active.");
                continue;
            }

            switch (widget.Kind)
            {
                case DashboardWidgetKinds.Tile when report.Tiles.All(t => t.Name != widget.Item):
                    errors.Add($"{path}.item", $"Report '{report.Code}' has no tile '{widget.Item}'.");
                    break;
                case DashboardWidgetKinds.Chart when report.Charts.All(c => c.Name != widget.Item):
                    errors.Add($"{path}.item", $"Report '{report.Code}' has no chart '{widget.Item}'.");
                    break;
            }

            // An empty target leaves the report filter to its default even when a dashboard filter has its name.
            foreach (var (name, target) in widget.Bind.Where(b => !string.IsNullOrEmpty(b.Value)))
            {
                var own = report.Filters.FirstOrDefault(f => f.Name == name);
                var source = filters.FirstOrDefault(f => f.Name == target);
                if (own is null)
                {
                    errors.Add($"{path}.bind.{name}", $"Report '{report.Code}' has no filter '{name}'.");
                }
                else if (source is null)
                {
                    errors.Add($"{path}.bind.{name}", $"The dashboard has no filter '{target}'.");
                }
                else if (!Fits(source, own))
                {
                    errors.Add($"{path}.bind.{name}", $"The dashboard filter '{target}' is not of the same kind as the report's filter '{name}'.");
                }
            }

            foreach (var (name, value) in widget.Values.Where(v => !string.IsNullOrWhiteSpace(v.Value)))
            {
                var own = report.Filters.FirstOrDefault(f => f.Name == name);
                if (own is null)
                {
                    errors.Add($"{path}.values.{name}", $"Report '{report.Code}' has no filter '{name}'.");
                }
                else if (ReportFilters.Check(own, value!, today) is { } problem)
                {
                    errors.Add($"{path}.values.{name}", problem);
                }
            }
        }
    }

    /// <summary>Whether a dashboard filter can fill a report's filter: the same kind, a list one of the same list, one value into one.</summary>
    public static bool Fits(ReportFilterDef source, ReportFilterDef target) =>
        source.Type == target.Type
        && (source.Type != ReportFilterTypes.List || (source.Lov == target.Lov && (!source.Multiple || target.Multiple)));

    public static List<ReportFilterDef> Clean(List<ReportFilterDef> filters) =>
        [.. filters.Select(f => { f.Default = Blank(f.Default); f.Lov = f.Type == ReportFilterTypes.List ? Blank(f.Lov) : null; return f; })];

    public static List<DashboardWidgetDef> Clean(List<DashboardWidgetDef> widgets) =>
        [
            .. widgets.Select(w =>
            {
                w.Item = w.Kind == DashboardWidgetKinds.Table ? null : Blank(w.Item);
                w.TitleAr = Blank(w.TitleAr);
                w.TitleEn = Blank(w.TitleEn);
                w.Values = w.Values.Where(v => !string.IsNullOrWhiteSpace(v.Value)).ToDictionary(v => v.Key, v => (string?)v.Value!.Trim(), StringComparer.Ordinal);
                return w;
            }),
        ];

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{0,63}$")]
    private static partial Regex Name();
}
