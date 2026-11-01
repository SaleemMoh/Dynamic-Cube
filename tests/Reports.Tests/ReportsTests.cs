using System.Text;
using System.Text.Json.Nodes;
using ClosedXML.Excel;
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

/// <summary>Reports (ADR-0045): definitions through change sets, a resource per report, runs, exports, saved filters.</summary>
public sealed class ReportsTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private TestHost _host = null!;
    private string _sector = null!;
    private string _report = null!;

    public async ValueTask InitializeAsync()
    {
        _host = new TestHost(fixture.RequireConnectionString());
        _sector = TestHost.Unique("SECTOR");
        Change Value(string code, string ar, string en, int staff, string? parent = null) =>
            new(AppsControlChangeTypes.Domain, $"{_sector}/{code}", ChangeOperation.Create, new { nameAr = ar, nameEn = en, attributes = new { STAFF = staff }, parentCode = parent });
        await _host.PublishAsync(
            AppsControlAreas.Lookups,
            new Change(AppsControlChangeTypes.Class, _sector, ChangeOperation.Create, new
            {
                nameAr = "قطاعات",
                nameEn = "Sectors",
                isHierarchical = true,
                attributes = new[] { new { code = "STAFF", nameAr = "الموظفون", nameEn = "Staff", dataType = "NUMBER" } },
            }),
            Value("ICT", "تقنية المعلومات", "ICT", 100),
            Value("BUILD", "البناء", "Building", 50),
            Value("SOFTWARE", "برمجيات", "Software", 40, "ICT"),
            Value("NETWORKS", "شبكات", "Networks", 20, "ICT"),
            Value("ROADS", "طرق", "Roads", 30, "BUILD"));
        _report = TestHost.Unique("STAFF_BY_PARENT");
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    private ReportPayload Payload(Action<ReportPayload>? change = null)
    {
        var payload = new ReportPayload
        {
            NameAr = "الموظفون حسب القطاع الأعلى",
            NameEn = "Staff by parent sector",
            CategoryEn = "Sectors",
            Query = JsonNode.Parse($$"""
                { "from": { "kind": "CLASS", "code": "{{_sector}}", "alias": "s" },
                  "where": { "match": "ALL", "conditions": [ { "column": "s.parent_code", "op": "IS_NOT_EMPTY" }, { "column": "s.staff", "op": "GE", "param": "minStaff" } ] } }
                """),
            Columns =
            [
                new() { Name = "parent", LabelAr = "القطاع الأعلى", LabelEn = "Parent", Column = "s.parent_code" },
                new() { Name = "sectors", LabelAr = "القطاعات", LabelEn = "Sectors", Column = "s.code", Aggregate = ReportAggregates.Count },
                new() { Name = "staff", LabelAr = "الموظفون", LabelEn = "Staff", Column = "s.staff", Aggregate = ReportAggregates.Sum },
            ],
            Filters = [new() { Name = "minStaff", LabelAr = "أقل عدد", LabelEn = "Minimum staff", Type = ReportFilterTypes.Number, Default = "10" }],
            Tiles = [new() { Name = "count", LabelAr = "العدد", LabelEn = "Count", Aggregate = ReportAggregates.Count }],
            Sort = [new() { Column = "staff", Descending = true }],
            Totals = true,
            Chart = new ReportChartDef { Type = ReportChartTypes.Bar, Category = "parent", Values = ["staff"] },
            PageSize = 10,
        };
        change?.Invoke(payload);
        return payload;
    }

    private Task<Guid> PublishAsync(ReportPayload payload, ChangeOperation operation = ChangeOperation.Create) =>
        _host.PublishAsync(ReportChangeTypes.Area, new Change(ReportChangeTypes.Report, _report, operation, payload));

    private Task<T> AsAdmin<T>(Func<ReportService, Task<T>> action) =>
        _host.AsAsync("admin", sp => action(sp.GetRequiredService<ReportService>()));

    [Fact]
    public async Task A_chart_of_a_report_grouped_by_more_columns_groups_by_its_own_column()
    {
        await PublishAsync(Payload(p => p.Columns.Insert(1, new() { Name = "sector", LabelAr = "القطاع", LabelEn = "Sector", Column = "s.code" })));

        var run = await AsAdmin(r => r.RunAsync(_report, new RunReportRequest(null), CancellationToken.None));
        run.Rows.Count.ShouldBe(3, "a row per parent and sector");
        run.Chart!.Rows.Select(r => (r["parent"]!.GetValue<string>(), r["staff"]!.GetValue<decimal>())).ShouldBe([("ICT", 60m), ("BUILD", 30m)], "a bar per parent, in the report's sort");
        run.Chart.Rows[0].ContainsKey("sector").ShouldBeFalse();
    }

    [Fact]
    public async Task A_published_report_is_a_resource_runs_grouped_with_totals_tiles_and_chart_and_exports()
    {
        await PublishAsync(Payload());

        var resource = await _host.AsAsync("admin", sp => sp.GetRequiredService<AppsControlDbContext>().Resources.AsNoTracking().SingleAsync(r => r.Code == ReportResources.Of(_report)));
        resource.IsActive.ShouldBeTrue();
        resource.AvailableActions.ShouldBe(["VIEW", "EXPORT"]);
        (await AsAdmin(r => r.MineAsync(CancellationToken.None))).ShouldContain(s => s.Code == _report);

        var view = await AsAdmin(r => r.ViewAsync(_report, CancellationToken.None));
        view.CanExport.ShouldBeTrue();
        view.Filters.ShouldHaveSingleItem().Default.ShouldBe("10");

        var run = await AsAdmin(r => r.RunAsync(_report, new RunReportRequest(new() { ["minStaff"] = "25" }), CancellationToken.None));
        run.Columns.Select(c => (c.Name, c.LabelEn, c.Type)).ShouldBe([("parent", "Parent", "TEXT"), ("sectors", "Sectors", "NUMBER"), ("staff", "Staff", "NUMBER")]);
        run.Rows.Select(r => (r["parent"]!.GetValue<string>(), r["staff"]!.GetValue<decimal>())).ShouldBe([("ICT", 40m), ("BUILD", 30m)]);
        run.Total.ShouldBe(2);
        run.Totals!["staff"]!.GetValue<decimal>().ShouldBe(70m);
        run.Tiles.ShouldHaveSingleItem().Value!.GetValue<decimal>().ShouldBe(2m);
        run.Chart!.Rows.Count.ShouldBe(2);
        run.Filters["minStaff"].ShouldBe("25");
        (await AsAdmin(r => r.RunAsync(_report, new RunReportRequest(null), CancellationToken.None))).Totals!["staff"]!.GetValue<decimal>().ShouldBe(90m, "the default filter: at least 10");

        var excel = await AsAdmin(r => r.ExportAsync(_report, new() { ["minStaff"] = "25" }, "xlsx", arabic: false, CancellationToken.None));
        excel.FileName.ShouldEndWith(".xlsx");
        using (var book = new XLWorkbook(new MemoryStream(excel.Content)))
        {
            var sheet = book.Worksheets.First();
            sheet.Cell(1, 1).GetString().ShouldBe("Staff by parent sector");
            var header = sheet.CellsUsed().First(c => c.GetString() == "Parent");
            sheet.Cell(header.Address.RowNumber + 1, 1).GetString().ShouldBe("ICT");
            sheet.Cell(header.Address.RowNumber + 1, 3).GetValue<decimal>().ShouldBe(40m);
            sheet.Cell(header.Address.RowNumber + 3, 1).GetString().ShouldBe("Total");
            sheet.Cell(header.Address.RowNumber + 3, 3).GetValue<decimal>().ShouldBe(70m);
        }

        var csv = await AsAdmin(r => r.ExportAsync(_report, new() { ["minStaff"] = "25" }, "csv", arabic: true, CancellationToken.None));
        var text = Encoding.UTF8.GetString(csv.Content);
        text[0].ShouldBe('﻿');
        text[1..].Split("\r\n")[0].ShouldBe("القطاع الأعلى,القطاعات,الموظفون");
        text.ShouldContain("ICT,1,40");
        text.ShouldContain("الإجمالي,2,70");
    }

    [Fact]
    public async Task Only_those_granted_the_report_open_it_and_a_deactivated_report_retires_its_resource()
    {
        await PublishAsync(Payload());
        await Should.ThrowAsync<ReportForbiddenException>(() => _host.AsAsync("maker", sp => sp.GetRequiredService<ReportService>().ViewAsync(_report, CancellationToken.None)));
        (await _host.AsAsync("maker", sp => sp.GetRequiredService<ReportService>().MineAsync(CancellationToken.None))).ShouldNotContain(s => s.Code == _report);
        await Should.ThrowAsync<ReportNotFoundException>(() => AsAdmin(r => r.ViewAsync("NOPE", CancellationToken.None)));

        await PublishAsync(Payload(p => p.IsActive = false), ChangeOperation.Update);
        (await _host.AsAsync("admin", sp => sp.GetRequiredService<AppsControlDbContext>().Resources.AsNoTracking().SingleAsync(r => r.Code == ReportResources.Of(_report)))).IsActive.ShouldBeFalse();
        await Should.ThrowAsync<ReportNotFoundException>(() => AsAdmin(r => r.ViewAsync(_report, CancellationToken.None)));
    }

    [Fact]
    public async Task Checks_the_definition_previews_it_unsaved_and_refuses_bad_filter_values()
    {
        var errors = (await Should.ThrowAsync<ChangeValidationException>(() => PublishAsync(Payload(p =>
        {
            p.Columns[1].Aggregate = "SUM";
            p.Columns[1].LabelEn = string.Empty;
            p.Filters.Add(new() { Name = "when", LabelAr = "متى", LabelEn = "When", Type = ReportFilterTypes.Date, Default = "SOMEDAY" });
            p.Filters.Add(new() { Name = "kind", LabelAr = "نوع", LabelEn = "Kind", Type = ReportFilterTypes.List });
            p.Chart = new ReportChartDef { Type = ReportChartTypes.Pie, Category = "nope", Values = ["parent"] };
            p.PageSize = 5;
        })))).Errors.Keys.ToList();
        errors.ShouldContain(k => k.EndsWith("columns[1].aggregate", StringComparison.Ordinal));
        errors.ShouldContain(k => k.EndsWith("columns[1].labelEn", StringComparison.Ordinal));
        errors.ShouldContain(k => k.EndsWith("filters[1].default", StringComparison.Ordinal));
        errors.ShouldContain(k => k.EndsWith("filters[2].lov", StringComparison.Ordinal));
        errors.ShouldContain(k => k.EndsWith("chart.category", StringComparison.Ordinal));
        errors.ShouldContain(k => k.EndsWith("pageSize", StringComparison.Ordinal));

        var preview = await AsAdmin(r => r.PreviewAsync(new PreviewReportRequest(Payload(), new() { ["minStaff"] = "35" }), CancellationToken.None));
        preview.Rows.ShouldHaveSingleItem()["parent"]!.GetValue<string>().ShouldBe("ICT");

        await PublishAsync(Payload());
        (await Should.ThrowAsync<ReportInputException>(() => AsAdmin(r => r.RunAsync(_report, new RunReportRequest(new() { ["minStaff"] = "lots", ["other"] = "x" }), CancellationToken.None))))
            .Errors.Keys.ShouldBe(["other", "minStaff"], ignoreOrder: true);
    }

    [Fact]
    public async Task Saved_filters_belong_to_their_user_and_one_is_the_default()
    {
        await PublishAsync(Payload());
        var first = await AsAdmin(r => r.SaveFilterAsync(_report, new SaveFilterRequest("Large", new() { ["minStaff"] = "35" }, IsDefault: true), CancellationToken.None));
        await AsAdmin(r => r.SaveFilterAsync(_report, new SaveFilterRequest("Small", new() { ["minStaff"] = "5" }, IsDefault: true), CancellationToken.None));
        var view = await AsAdmin(r => r.ViewAsync(_report, CancellationToken.None));
        view.SavedFilters.Select(f => (f.Name, f.IsDefault)).ShouldBe([("Small", true), ("Large", false)]);
        await Should.ThrowAsync<ReportInputException>(() => AsAdmin(r => r.SaveFilterAsync(_report, new SaveFilterRequest("Bad", new() { ["minStaff"] = "x" }, false), CancellationToken.None)));

        await _host.AsAsync("admin", sp => sp.GetRequiredService<ReportService>().DeleteFilterAsync(_report, first.Id, CancellationToken.None));
        (await AsAdmin(r => r.ViewAsync(_report, CancellationToken.None))).SavedFilters.ShouldHaveSingleItem().Name.ShouldBe("Small");
    }

    [Fact]
    public void Relative_dates_resolve_against_today()
    {
        var today = new DateOnly(2026, 11, 1);
        ReportFilters.RelativeDate("TODAY", today).ShouldBe(today);
        ReportFilters.RelativeDate("today-30", today).ShouldBe(new DateOnly(2026, 10, 2));
        ReportFilters.RelativeDate("MONTH_START", today).ShouldBe(new DateOnly(2026, 11, 1));
        ReportFilters.RelativeDate("LAST_MONTH_START", today).ShouldBe(new DateOnly(2026, 10, 1));
        ReportFilters.RelativeDate("LAST_MONTH_END", today).ShouldBe(new DateOnly(2026, 10, 31));
        ReportFilters.RelativeDate("YEAR_START", today).ShouldBe(new DateOnly(2026, 1, 1));
        ReportFilters.RelativeDate("2026-01-01", today).ShouldBeNull();
        var resolved = ReportFilters.Resolve(
            [new() { Name = "from", Type = ReportFilterTypes.Date, Default = "TODAY-7" }, new() { Name = "kinds", Type = ReportFilterTypes.List, Lov = "L", Multiple = true }],
            new Dictionary<string, string?> { ["kinds"] = " A, B ,," },
            today);
        resolved["from"].ShouldBe("2026-10-25");
        resolved["kinds"].ShouldBe("A,B");
    }
}
