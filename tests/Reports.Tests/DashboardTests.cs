using System.Text;
using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Modules.Reports.Changes;
using DynamicCube.Modules.Reports.Domain;
using DynamicCube.Modules.Reports.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Reports;
using DynamicCube.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Reports.Tests;

/// <summary>Drill-down and dashboards (ADR-0047): the rows behind a group, shared dashboards through change sets, personal ones.</summary>
public sealed class DashboardTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private TestHost _host = null!;
    private string _sector = null!;
    private string _report = null!;
    private string _dashboard = null!;

    public async ValueTask InitializeAsync()
    {
        _host = new TestHost(fixture.RequireConnectionString());
        _sector = TestHost.Unique("SECTOR");
        Change Value(string code, string en, int staff, string? parent = null) =>
            new(AppsControlChangeTypes.Domain, $"{_sector}/{code}", ChangeOperation.Create, new { nameAr = en, nameEn = en, attributes = new { STAFF = staff }, parentCode = parent });
        await _host.PublishAsync(
            AppsControlAreas.DataDictionary,
            new Change(AppsControlChangeTypes.Class, _sector, ChangeOperation.Create, new
            {
                nameAr = "قطاعات",
                nameEn = "Sectors",
                isHierarchical = true,
                attributes = new[] { new { code = "STAFF", nameAr = "الموظفون", nameEn = "Staff", dataType = "NUMBER" } },
            }),
            Value("ICT", "ICT", 100),
            Value("BUILD", "Building", 50),
            Value("SOFTWARE", "Software", 40, "ICT"),
            Value("NETWORKS", "Networks", 20, "ICT"),
            Value("ROADS", "Roads", 30, "BUILD"));
        _report = TestHost.Unique("STAFF");
        _dashboard = TestHost.Unique("BOARD");
        await _host.PublishAsync(ReportChangeTypes.Area, new Change(ReportChangeTypes.Report, _report, ChangeOperation.Create, Report()));
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    /// <summary>Sectors under a parent with at least <c>minStaff</c> staff, grouped by parent: ICT (Software 40, Networks 20), Building (Roads 30).</summary>
    private ReportPayload Report(Action<ReportPayload>? change = null)
    {
        var payload = new ReportPayload
        {
            NameAr = "الموظفون",
            NameEn = "Staff",
            Query = JsonNode.Parse($$"""
                { "from": { "kind": "CLASS", "code": "{{_sector}}", "alias": "s" },
                  "where": { "match": "ALL", "conditions": [ { "column": "s.parent_code", "op": "IS_NOT_EMPTY" }, { "column": "s.staff", "op": "GE", "param": "minStaff" } ] } }
                """),
            Columns =
            [
                new() { Name = "parent", LabelAr = "الأعلى", LabelEn = "Parent", Column = "s.parent_code" },
                new() { Name = "sectors", LabelAr = "القطاعات", LabelEn = "Sectors", Column = "s.code", Aggregate = ReportAggregates.Count },
                new() { Name = "staff", LabelAr = "الموظفون", LabelEn = "Staff", Column = "s.staff", Aggregate = ReportAggregates.Sum },
            ],
            Filters = [new() { Name = "minStaff", LabelAr = "أقل عدد", LabelEn = "Minimum staff", Type = ReportFilterTypes.Number, Default = "10" }],
            Tiles = [new() { Name = "groups", LabelAr = "المجموعات", LabelEn = "Groups", Aggregate = ReportAggregates.Count }],
            Sort = [new() { Column = "staff", Descending = true }],
            Totals = true,
            Charts =
            [
                new ReportChartDef
                {
                    Name = "byParent",
                    TitleAr = "حسب الأعلى",
                    TitleEn = "By parent",
                    X = [new ReportChartField { Column = "s.parent_code" }],
                    Y = [new ReportChartField { Column = "s.staff", Aggregate = ReportAggregates.Sum }],
                },
            ],
            PageSize = 10,
        };
        change?.Invoke(payload);
        return payload;
    }

    private DashboardPayload Dashboard(Action<DashboardPayload>? change = null)
    {
        var payload = new DashboardPayload
        {
            NameAr = "لوحة القطاعات",
            NameEn = "Sectors board",
            Filters = [new() { Name = "minStaff", LabelAr = "أقل عدد", LabelEn = "Minimum staff", Type = ReportFilterTypes.Number, Default = "25" }],
            Widgets =
            [
                new() { Name = "groups", Kind = DashboardWidgetKinds.Tile, Report = _report, Item = "groups", Span = 3 },
                new() { Name = "big", Kind = DashboardWidgetKinds.Tile, Report = _report, Item = "groups", Span = 3, TitleEn = "Big groups", Values = new() { ["minStaff"] = "35" } },
                new() { Name = "chart", Kind = DashboardWidgetKinds.Chart, Report = _report, Item = "byParent", Span = 6 },
                new() { Name = "top", Kind = DashboardWidgetKinds.Table, Report = _report, Span = 12, Rows = 1 },
            ],
        };
        change?.Invoke(payload);
        return payload;
    }

    private Task<T> Reports<T>(Func<ReportService, Task<T>> action, string user = "admin") =>
        _host.AsAsync(user, sp => action(sp.GetRequiredService<ReportService>()));

    private Task<T> Boards<T>(Func<DashboardService, Task<T>> action, string user = "admin") =>
        _host.AsAsync(user, sp => action(sp.GetRequiredService<DashboardService>()));

    [Fact]
    public async Task A_group_of_the_table_a_chart_point_or_a_tile_opens_the_rows_behind_it()
    {
        // A table row: the report's columns read as they are (the counted one left out).
        var ict = await Reports(r => r.DrillAsync(_report, new DrillRequest(null, DrillSources.Table, null, new JsonObject { ["parent"] = "ICT", ["staff"] = 60 }), CancellationToken.None));
        ict.Columns.Select(c => (c.Name, c.LabelEn)).ShouldBe([("parent", "Parent"), ("staff", "Staff")]);
        ict.Rows.Select(r => r["staff"]!.GetValue<decimal>()).ShouldBe([40m, 20m], ignoreOrder: true);
        ict.Total.ShouldBe(2);
        ict.Narrowed.ShouldHaveSingleItem().ShouldSatisfyAllConditions(n => n.LabelEn.ShouldBe("Parent"), n => n.Value!.GetValue<string>().ShouldBe("ICT"));

        // A chart's point, and the report's filter still applies.
        var building = await Reports(r => r.DrillAsync(_report, new DrillRequest(new() { ["minStaff"] = "25" }, DrillSources.Chart, "byParent", new JsonObject { ["x0"] = "BUILD", ["y0"] = 30 }), CancellationToken.None));
        building.Rows.ShouldHaveSingleItem()["staff"]!.GetValue<decimal>().ShouldBe(30m);
        (await Reports(r => r.DrillAsync(_report, new DrillRequest(new() { ["minStaff"] = "25" }, DrillSources.Chart, "byParent", new JsonObject { ["x0"] = "ICT" }), CancellationToken.None)))
            .Total.ShouldBe(1, "Networks has fewer than 25");

        // A tile or the totals row: every row the filters keep; an empty group value matches empty columns.
        (await Reports(r => r.DrillAsync(_report, new DrillRequest(null, DrillSources.Tile, null, null), CancellationToken.None))).Total.ShouldBe(3);
        (await Reports(r => r.DrillAsync(_report, new DrillRequest(null, DrillSources.Table, null, new JsonObject { ["parent"] = null }), CancellationToken.None))).Total.ShouldBe(0);

        // The report's own detail columns, and an export of them.
        await _host.PublishAsync(ReportChangeTypes.Area, new Change(ReportChangeTypes.Report, _report, ChangeOperation.Update, Report(p => p.Details =
        [
            new() { Name = "sector", LabelAr = "القطاع", LabelEn = "Sector", Column = "s.name_en" },
            new() { Name = "staff", LabelAr = "الموظفون", LabelEn = "Staff", Column = "s.staff" },
        ])));
        var details = await Reports(r => r.DrillAsync(_report, new DrillRequest(null, DrillSources.Table, null, new JsonObject { ["parent"] = "ICT" }), CancellationToken.None));
        details.Rows.Select(r => r["sector"]!.GetValue<string>()).ShouldBe(["Networks", "Software"], ignoreOrder: true);
        var csv = await Reports(r => r.ExportDrillAsync(_report, new DrillRequest(null, DrillSources.Table, null, new JsonObject { ["parent"] = "ICT" }), "csv", arabic: false, CancellationToken.None));
        var text = Encoding.UTF8.GetString(csv.Content);
        text.ShouldContain("Sector,Staff");
        text.ShouldContain("Software,40");

        // Refused: a value missing, a chart that does not exist, a viewer without the report.
        (await Should.ThrowAsync<ReportInputException>(() => Reports(r => r.DrillAsync(_report, new DrillRequest(null, DrillSources.Table, null, []), CancellationToken.None))))
            .Errors.Keys.ShouldBe(["values"]);
        (await Should.ThrowAsync<ReportInputException>(() => Reports(r => r.DrillAsync(_report, new DrillRequest(null, DrillSources.Chart, "nope", []), CancellationToken.None))))
            .Errors.Keys.ShouldBe(["chart"]);
        await Should.ThrowAsync<ReportForbiddenException>(() => Reports(r => r.DrillAsync(_report, new DrillRequest(null, DrillSources.Tile, null, null), CancellationToken.None), "maker"));
    }

    [Fact]
    public async Task Bad_detail_columns_are_refused_when_the_report_is_published()
    {
        var errors = (await Should.ThrowAsync<ChangeValidationException>(() => _host.PublishAsync(ReportChangeTypes.Area, new Change(ReportChangeTypes.Report, _report, ChangeOperation.Update, Report(p => p.Details =
        [
            new() { Name = "x", LabelAr = "س", LabelEn = string.Empty, Column = "s.nope" },
            new() { Name = "y", LabelAr = "ص", LabelEn = "Y", Column = "s.staff", Aggregate = ReportAggregates.Sum },
        ]))))).Errors.Keys;
        errors.ShouldBe(["details[0].labelEn", "details[0].column", "details[1].aggregate"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_shared_dashboard_is_a_screen_whose_filters_fill_its_reports_filters()
    {
        await _host.PublishAsync(ReportChangeTypes.Area, new Change(ReportChangeTypes.Dashboard, _dashboard, ChangeOperation.Create, Dashboard()));
        var resource = await _host.AsAsync("admin", sp => sp.GetRequiredService<AppsControlDbContext>().Resources.AsNoTracking().SingleAsync(r => r.Code == ReportResources.Dashboard(_dashboard)));
        resource.IsActive.ShouldBeTrue();
        (await Boards(b => b.MineAsync(CancellationToken.None))).ShouldContain(d => d.Code == _dashboard && !d.Personal);

        var view = await Boards(b => b.SharedAsync(_dashboard, CancellationToken.None));
        view.Widgets.Select(w => (w.Name, w.TitleEn, w.CanDrill)).ShouldBe([("groups", "Groups", true), ("big", "Big groups", true), ("chart", "By parent", true), ("top", "Staff", true)]);
        view.Hidden.ShouldBe(0);

        // The dashboard's default (25) fills the report's filter of the same name; a widget's own value wins.
        var groups = await Boards(b => b.RunSharedAsync(_dashboard, "groups", new RunWidgetRequest(null), CancellationToken.None));
        groups.Tile!.Value!.GetValue<decimal>().ShouldBe(2m);
        groups.Filters["minStaff"].ShouldBe("25");
        (await Boards(b => b.RunSharedAsync(_dashboard, "big", new RunWidgetRequest(null), CancellationToken.None))).Tile!.Value!.GetValue<decimal>().ShouldBe(1m);
        (await Boards(b => b.RunSharedAsync(_dashboard, "groups", new RunWidgetRequest(new() { ["minStaff"] = "45" }), CancellationToken.None))).Tile!.Value!.GetValue<decimal>().ShouldBe(0m);

        var chart = await Boards(b => b.RunSharedAsync(_dashboard, "chart", new RunWidgetRequest(new() { ["minStaff"] = "10" }), CancellationToken.None));
        chart.Chart!.Rows.Count.ShouldBe(2);
        var table = await Boards(b => b.RunSharedAsync(_dashboard, "top", new RunWidgetRequest(null), CancellationToken.None));
        (table.Table!.Rows.Count, table.Table.Total, table.Table.Grouped).ShouldBe((1, 2L, true));
        table.Table.Rows[0]["parent"]!.GetValue<string>().ShouldBe("ICT", "the most staff first");

        // Only those granted the dashboard open it; a report shown on it cannot be deleted.
        await Should.ThrowAsync<ReportForbiddenException>(() => Boards(b => b.SharedAsync(_dashboard, CancellationToken.None), "maker"));
        await Should.ThrowAsync<ChangeSetException>(() => _host.PublishAsync(ReportChangeTypes.Area, new Change(ReportChangeTypes.Report, _report, ChangeOperation.Delete, null)));
    }

    [Fact]
    public async Task A_dashboards_widgets_are_checked_against_their_reports()
    {
        var errors = (await Should.ThrowAsync<ChangeValidationException>(() => _host.PublishAsync(ReportChangeTypes.Area, new Change(ReportChangeTypes.Dashboard, _dashboard, ChangeOperation.Create, Dashboard(d =>
        {
            d.Filters.Add(new() { Name = "label", LabelAr = "نص", LabelEn = "Text", Type = ReportFilterTypes.Text });
            d.Widgets =
            [
                new() { Name = "a", Kind = DashboardWidgetKinds.Tile, Report = _report, Item = "nope", Span = 0 },
                new() { Name = "b", Kind = DashboardWidgetKinds.Chart, Report = "NO_SUCH_REPORT", Item = "x" },
                new() { Name = "c", Kind = DashboardWidgetKinds.Table, Report = _report, Rows = 99, Bind = new() { ["minStaff"] = "label", ["other"] = "minStaff" }, Values = new() { ["minStaff"] = "lots" } },
                new() { Name = "c", Kind = "MAP", Report = _report },
            ];
        }))))).Errors.Keys;
        errors.ShouldBe(
            [
                "widgets[0].item", "widgets[0].span", "widgets[1].report", "widgets[2].rows", "widgets[2].bind.minStaff", "widgets[2].bind.other",
                "widgets[2].values.minStaff", "widgets[3].kind", "widgets",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task People_pin_widgets_to_dashboards_of_their_own()
    {
        var pinned = await Boards(b => b.PinAsync(new PinWidgetRequest(null, "My board", new DashboardWidgetDef { Kind = DashboardWidgetKinds.Chart, Report = _report, Item = "byParent", Span = 0 }), CancellationToken.None));
        pinned.Personal.ShouldBeTrue();
        var id = pinned.Id!.Value;
        pinned.Widgets.ShouldHaveSingleItem().ShouldSatisfyAllConditions(w => w.Name.ShouldBe("byParent"), w => w.Span.ShouldBe(6));

        var again = await Boards(b => b.PinAsync(new PinWidgetRequest(id, null, new DashboardWidgetDef { Kind = DashboardWidgetKinds.Chart, Report = _report, Item = "byParent", Values = new() { ["minStaff"] = "25" } }), CancellationToken.None));
        again.Widgets.Select(w => w.Name).ShouldBe(["byParent", "byParent2"]);
        (await Boards(b => b.RunPersonalAsync(id, "byParent2", new RunWidgetRequest(null), CancellationToken.None))).Filters["minStaff"].ShouldBe("25");

        var mine = await Boards(b => b.MineAsync(CancellationToken.None));
        mine.ShouldContain(d => d.Id == id && d.Personal && d.NameEn == "My board");
        await Should.ThrowAsync<DashboardNotFoundException>(() => Boards(b => b.PersonalAsync(id, CancellationToken.None), "maker"));
        (await Should.ThrowAsync<ChangeValidationException>(() => Boards(b => b.SavePersonalAsync(null, new SavePersonalDashboardRequest("My board", null, again.Definition!.ToList(), false), CancellationToken.None))))
            .Errors.Keys.ShouldBe(["name"]);
        (await Should.ThrowAsync<ChangeValidationException>(() => Boards(b => b.PinAsync(new PinWidgetRequest(null, "Not mine", new DashboardWidgetDef { Kind = DashboardWidgetKinds.Table, Report = _report }), CancellationToken.None), "maker")))
            .Errors.Keys.ShouldBe(["widgets[0].report"]);

        await Boards(async b => { await b.DeletePersonalAsync(id, CancellationToken.None); return 0; });
        (await Boards(b => b.MineAsync(CancellationToken.None))).ShouldNotContain(d => d.Id == id);
    }
}
