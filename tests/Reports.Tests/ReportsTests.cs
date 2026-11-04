using System.Text;
using System.Text.Json.Nodes;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
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
            AppsControlAreas.DataDictionary,
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
            Charts =
            [
                new ReportChartDef
                {
                    Name = "staffByParent",
                    TitleAr = "الموظفون حسب القطاع الأعلى",
                    TitleEn = "Staff by parent",
                    Type = ReportChartTypes.Bar,
                    X = [new ReportChartField { Column = "s.parent_code", LabelEn = "Parent" }],
                    Y = [new ReportChartField { Column = "s.staff", Aggregate = ReportAggregates.Sum, LabelEn = "Staff" }],
                    Sort = ReportChartSorts.YDescending,
                },
            ],
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
        var chart = run.Charts.ShouldHaveSingleItem();
        chart.Category.ShouldBe("X");
        chart.Rows.Select(r => (r["x0"]!.GetValue<string>(), r["y0"]!.GetValue<decimal>())).ShouldBe([("ICT", 60m), ("BUILD", 30m)], "a bar per parent, the most staff first");
        chart.Rows[0].ContainsKey("s").ShouldBeFalse();
        (chart.X[0].LabelEn, chart.X[0].LabelAr, chart.Y[0].LabelEn).ShouldBe(("Parent", "parent_code", "Staff"), "a label left out is the column's name");
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
        run.Charts.ShouldHaveSingleItem().Rows.Count.ShouldBe(2);
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
            p.Charts =
            [
                new ReportChartDef
                {
                    Name = "pie",
                    TitleEn = "Pie",
                    Type = ReportChartTypes.Pie,
                    X = [new ReportChartField { Column = "s.parent_code" }],
                    Y = [new ReportChartField { Column = "s.staff", Aggregate = ReportAggregates.Sum }, new ReportChartField { Aggregate = ReportAggregates.Count }],
                },
                new ReportChartDef
                {
                    Name = "bad",
                    TitleAr = "سيئ",
                    TitleEn = "Bad",
                    X = [new ReportChartField { Column = "s.nope", Bucket = "MONTH" }],
                    Y = [new ReportChartField { Column = "s.name_en", Aggregate = ReportAggregates.Avg }],
                    Limit = 0,
                },
            ];
            p.PageSize = 5;
        })))).Errors.Keys.ToList();
        errors.ShouldContain(k => k.EndsWith("columns[1].aggregate", StringComparison.Ordinal));
        errors.ShouldContain(k => k.EndsWith("columns[1].labelEn", StringComparison.Ordinal));
        errors.ShouldContain(k => k.EndsWith("filters[1].default", StringComparison.Ordinal));
        errors.ShouldContain(k => k.EndsWith("filters[2].lov", StringComparison.Ordinal));
        errors.ShouldContain(k => k.EndsWith("charts[0].titleAr", StringComparison.Ordinal));
        errors.ShouldContain(k => k.EndsWith("charts[0].type", StringComparison.Ordinal), "a pie draws one value");
        errors.ShouldContain(k => k.EndsWith("charts[1].limit", StringComparison.Ordinal));
        errors.ShouldContain(k => k.EndsWith("charts[1].x[0].column", StringComparison.Ordinal));
        errors.ShouldContain(k => k.EndsWith("charts[1].y[0].aggregate", StringComparison.Ordinal), "text is not averaged");
        errors.ShouldContain(k => k.EndsWith("pageSize", StringComparison.Ordinal));

        var preview = await AsAdmin(r => r.PreviewAsync(new PreviewReportRequest(Payload(), new() { ["minStaff"] = "35" }), CancellationToken.None));
        preview.Rows.ShouldHaveSingleItem()["parent"]!.GetValue<string>().ShouldBe("ICT");

        await PublishAsync(Payload());
        (await Should.ThrowAsync<ReportInputException>(() => AsAdmin(r => r.RunAsync(_report, new RunReportRequest(new() { ["minStaff"] = "lots", ["other"] = "x" }), CancellationToken.None))))
            .Errors.Keys.ShouldBe(["other", "minStaff"], ignoreOrder: true);
    }

    [Fact]
    public async Task Several_charts_put_anything_on_either_axis_a_chart_previews_alone_and_an_older_chart_is_read()
    {
        await PublishAsync(Payload(p =>
        {
            // The groups down the Y axis, a count and an average across: horizontal bars.
            p.Charts.Add(new ReportChartDef
            {
                Name = "sectors",
                TitleAr = "القطاعات",
                TitleEn = "Sectors",
                Type = ReportChartTypes.Bar,
                Y = [new ReportChartField { Column = "s.parent_code" }],
                X = [new ReportChartField { Aggregate = ReportAggregates.Count }, new ReportChartField { Column = "s.staff", Aggregate = ReportAggregates.Avg }],
                Sort = ReportChartSorts.YAscending,
                Width = ReportChartWidths.Full,
            });

            // No totals on either axis: each row is a point.
            p.Charts.Add(new ReportChartDef
            {
                Name = "points",
                TitleAr = "النقاط",
                TitleEn = "Points",
                Type = ReportChartTypes.Scatter,
                X = [new ReportChartField { Column = "s.code" }],
                Y = [new ReportChartField { Column = "s.staff" }],
                Series = new ReportChartField { Column = "s.parent_code" },
                Sort = ReportChartSorts.YDescending,
            });
        }));

        var view = await AsAdmin(r => r.ViewAsync(_report, CancellationToken.None));
        view.Charts.Select(c => c.Name).ShouldBe(["staffByParent", "sectors", "points"]);
        var run = await AsAdmin(r => r.RunAsync(_report, new RunReportRequest(null), CancellationToken.None));
        var sectors = run.Charts[1];
        (sectors.Category, sectors.Width, sectors.X[0].LabelEn, sectors.X[1].Type).ShouldBe(("Y", ReportChartWidths.Full, "Count", "NUMBER"));
        sectors.Rows.Select(r => (r["y0"]!.GetValue<string>(), r["x0"]!.GetValue<decimal>(), r["x1"]!.GetValue<decimal>()))
            .ShouldBe([("BUILD", 1m, 30m), ("ICT", 2m, 30m)], "the rows counted and the staff averaged per parent, down the Y axis");
        var points = run.Charts[2];
        points.Category.ShouldBe("ROWS");
        points.Rows.Select(r => (r["x0"]!.GetValue<string>(), r["y0"]!.GetValue<decimal>(), r["s"]!.GetValue<string>()))
            .ShouldBe([("SOFTWARE", 40m, "ICT"), ("ROADS", 30m, "BUILD"), ("NETWORKS", 20m, "ICT")], "each sector with a parent, the most staff first");

        // Totals on both axes, or several fields on the axis grouped by, fit neither way.
        var both = (await Should.ThrowAsync<ChangeValidationException>(() => PublishAsync(Payload(p => p.Charts[0].X[0].Aggregate = ReportAggregates.Count), ChangeOperation.Update))).Errors.Keys;
        both.ShouldContain(k => k.EndsWith("charts[0].y", StringComparison.Ordinal));

        // The designer's live preview of one chart of the unsaved report.
        var payload = Payload();
        payload.Charts[0].Limit = 1;
        var preview = await AsAdmin(r => r.PreviewChartAsync(new PreviewChartRequest(payload, "staffByParent", new() { ["minStaff"] = "10" }), CancellationToken.None));
        preview.Rows.ShouldHaveSingleItem()["x0"]!.GetValue<string>().ShouldBe("ICT");
        preview.HasMore.ShouldBeTrue();
        await Should.ThrowAsync<ChangeValidationException>(() => AsAdmin(r => r.PreviewChartAsync(new PreviewChartRequest(payload, "nope", null), CancellationToken.None)));

        // A package written before several charts: its chart is read as the first one.
        await PublishAsync(Payload(p =>
        {
            p.Charts.Clear();
            p.Chart = new LegacyReportChart { Type = ReportChartTypes.Pie, Category = "parent", Values = ["staff"] };
        }), ChangeOperation.Update);
        var legacy = (await AsAdmin(r => r.ViewAsync(_report, CancellationToken.None))).Charts.ShouldHaveSingleItem();
        (legacy.Type, legacy.X.Single().Column, legacy.Y.Single().Column, legacy.Y.Single().Aggregate).ShouldBe((ReportChartTypes.Pie, "s.parent_code", "s.staff", ReportAggregates.Sum));
    }

    [Fact]
    public async Task The_excel_file_holds_each_chart_as_a_native_chart_beside_its_data()
    {
        await PublishAsync(Payload(p =>
        {
            p.Charts[0].Width = ReportChartWidths.Full;
            p.Charts.Add(new ReportChartDef
            {
                Name = "across",
                TitleAr = "أفقي",
                TitleEn = "Across",
                Type = ReportChartTypes.Bar,
                Y = [new ReportChartField { Column = "s.parent_code", LabelEn = "Parent" }],
                X = [new ReportChartField { Column = "s.staff", Aggregate = ReportAggregates.Avg, LabelEn = "Average" }],
            });
            p.Charts.Add(new ReportChartDef
            {
                Name = "points",
                TitleAr = "نقاط",
                TitleEn = "Points",
                Type = ReportChartTypes.Scatter,
                X = [new ReportChartField { Column = "s.staff" }],
                Y = [new ReportChartField { Column = "s.staff" }],
                Series = new ReportChartField { Column = "s.parent_code" },
            });
            p.Charts.Add(new ReportChartDef
            {
                Name = "pie",
                TitleAr = "دائري",
                TitleEn = "Pie",
                Type = ReportChartTypes.Pie,
                X = [new ReportChartField { Column = "s.parent_code" }],
                Y = [new ReportChartField { Aggregate = ReportAggregates.Count }],
            });
        }));

        var excel = await AsAdmin(r => r.ExportAsync(_report, null, "xlsx", arabic: false, CancellationToken.None));
        using (var book = new XLWorkbook(new MemoryStream(excel.Content)))
        {
            book.Worksheets.Select(w => w.Name).ShouldBe(["Report", "Charts"]);
            var sheet = book.Worksheet("Charts");
            sheet.Cell(1, 1).GetString().ShouldBe("Staff by parent");
            sheet.Cell(2, 1).GetString().ShouldBe("Parent");
            sheet.Cell(2, 2).GetString().ShouldBe("Staff");
            sheet.Cell(3, 1).GetString().ShouldBe("ICT");
            sheet.Cell(3, 2).GetValue<double>().ShouldBe(60);
        }

        using var document = SpreadsheetDocument.Open(new MemoryStream(excel.Content), isEditable: false);
        var charts = document.WorkbookPart!.WorksheetParts.SelectMany(w => w.DrawingsPart?.ChartParts ?? []).ToList();
        charts.Count.ShouldBe(4);
        var xml = charts.Select(c => c.ChartSpace.OuterXml).ToList();
        xml[0].ShouldContain("<c:barDir val=\"col\" />");
        xml[0].ShouldContain("'Charts'!$B$3:$B$4");
        xml[1].ShouldContain("<c:barDir val=\"bar\" />", customMessage: "grouped down the Y axis: horizontal bars");
        xml[2].ShouldContain("<c:scatterChart>");
        xml[2].ShouldContain("ICT", customMessage: "a series per parent");
        xml[3].ShouldContain("<c:pieChart>");

        // The charts and their drawing follow the Open XML schema.
        var problems = new OpenXmlValidator().Validate(document, TestContext.Current.CancellationToken)
            .Where(e => e.Part is ChartPart or DrawingsPart)
            .Select(e => $"{e.Part?.Uri} {e.Path?.XPath}: {e.Description}")
            .ToList();
        problems.ShouldBeEmpty();

        var csv = await AsAdmin(r => r.ExportAsync(_report, null, "csv", arabic: false, CancellationToken.None));
        Encoding.UTF8.GetString(csv.Content).ShouldNotContain("Points", customMessage: "a CSV file holds the rows only");
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
