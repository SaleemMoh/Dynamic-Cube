using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Access;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Tests.Infrastructure;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Reports;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.AppsControl.Tests;

/// <summary>Report queries (ADR-0045): columns grouped and totalled, totals row, tiles, sort, filters, viewers.</summary>
public sealed class ReportQueryTests(AppsControlFixture fixture) : IAsyncLifetime
{
    private Harness _harness = null!;
    private string _sector = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = new Harness(fixture);
        if (fixture.ConnectionString is null)
        {
            return;
        }

        _sector = Harness.Unique("SECTOR");
        await _harness.PublishAsync(
            AppsControlAreas.DataDictionary,
            new Change(AppsControlChangeTypes.Class, _sector, ChangeOperation.Create, new
            {
                nameAr = "قطاعات",
                nameEn = "Sectors",
                isHierarchical = true,
                attributes = new[] { new { code = "STAFF", nameAr = "الموظفون", nameEn = "Staff", dataType = "NUMBER" } },
            }),
            DataDictionaryTests.DomainChange(_sector, "ICT", "تقنية المعلومات", "ICT", new { STAFF = 100 }),
            DataDictionaryTests.DomainChange(_sector, "BUILD", "البناء", "Building", new { STAFF = 50 }),
            DataDictionaryTests.DomainChange(_sector, "SOFTWARE", "برمجيات", "Software", new { STAFF = 40 }, parent: "ICT"),
            DataDictionaryTests.DomainChange(_sector, "NETWORKS", "شبكات", "Networks", new { STAFF = 20 }, parent: "ICT"),
            DataDictionaryTests.DomainChange(_sector, "ROADS", "طرق", "Roads", new { STAFF = 30 }, parent: "BUILD"));
    }

    public ValueTask DisposeAsync() => _harness.DisposeAsync();

    private JsonNode Query(object? where = null) => JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(new
    {
        from = new { kind = "CLASS", code = _sector, alias = "s" },
        where,
    }))!;

    private static readonly ReportShape ByParent = new(
        [new("parent", "s.parent_code"), new("sectors", "s.code", ReportAggregates.Count), new("staff", "s.staff", ReportAggregates.Sum), new("average", "s.staff", ReportAggregates.Avg)],
        [new("rows", null, ReportAggregates.Count), new("totalStaff", "s.staff", ReportAggregates.Sum), new("largest", "s.staff", ReportAggregates.Max)],
        [new("staff", Descending: true)],
        Totals: true);

    private Task<ReportResult> RunAsync(JsonNode query, ReportShape shape, Dictionary<string, string?>? inputs = null, ReportViewer? viewer = null, int limit = 50, int offset = 0) =>
        _harness.AsAsync("admin", sp => sp.GetRequiredService<IReportQueries>().RunAsync(query, shape, inputs ?? [], viewer ?? ReportViewer.Current, null, limit, offset, withSummary: true, CancellationToken.None));

    private Task<IReadOnlyDictionary<string, string[]>> CheckAsync(JsonNode query, ReportShape shape, IReadOnlyCollection<string>? inputs = null) =>
        _harness.AsAsync("admin", sp => sp.GetRequiredService<IReportQueries>().CheckAsync(query, shape, inputs ?? [], CancellationToken.None));

    [Fact]
    public async Task Groups_by_the_columns_not_totalled_with_a_totals_row_tiles_and_the_sort()
    {
        var query = Query(new { match = "ALL", conditions = new[] { new { column = "s.parent_code", op = "IS_NOT_EMPTY" } } });
        (await CheckAsync(query, ByParent)).ShouldBeEmpty();

        var result = await RunAsync(query, ByParent);

        result.Columns.Select(c => (c.Name, c.Type, c.Aggregate)).ShouldBe(
        [
            ("parent", "TEXT", null),
            ("sectors", "NUMBER", ReportAggregates.Count),
            ("staff", "NUMBER", ReportAggregates.Sum),
            ("average", "NUMBER", ReportAggregates.Avg),
        ]);
        result.Rows.Select(r => (r["parent"]!.GetValue<string>(), r["sectors"]!.GetValue<decimal>(), r["staff"]!.GetValue<decimal>(), r["average"]!.GetValue<decimal>()))
            .ShouldBe([("ICT", 2m, 60m, 30m), ("BUILD", 1m, 30m, 30m)], "grouped by parent, the most staff first");
        result.Total.ShouldBe(2);
        result.HasMore.ShouldBeFalse();
        result.Totals!["parent"].ShouldBeNull();
        result.Totals["sectors"]!.GetValue<decimal>().ShouldBe(3m);
        result.Totals["staff"]!.GetValue<decimal>().ShouldBe(90m);
        result.Totals["average"]!.GetValue<decimal>().ShouldBe(30m);
        result.Tiles!["rows"]!.GetValue<decimal>().ShouldBe(3m);
        result.Tiles["totalStaff"]!.GetValue<decimal>().ShouldBe(90m);
        result.Tiles["largest"]!.GetValue<decimal>().ShouldBe(40m);
    }

    [Fact]
    public async Task Plain_rows_page_with_their_count_and_filters_narrow_them()
    {
        var shape = new ReportShape([new("code", "s.code"), new("staff", "s.staff")], [], [new("staff")], Totals: true);
        var query = Query(new { match = "ALL", conditions = new[] { new { column = "s.staff", op = "GE", param = "minStaff" } } });
        (await CheckAsync(query, shape, ["minStaff"])).ShouldBeEmpty();

        var first = await RunAsync(query, shape, new() { ["minStaff"] = "30" }, limit: 2);
        first.Rows.Select(r => r["code"]!.GetValue<string>()).ShouldBe(["ROADS", "SOFTWARE"]);
        first.HasMore.ShouldBeTrue();
        first.Total.ShouldBe(4);
        first.Totals.ShouldBeNull("nothing is totalled");
        first.Tiles.ShouldBeNull();
        var rest = await RunAsync(query, shape, new() { ["minStaff"] = "30" }, limit: 2, offset: 2);
        rest.Rows.Select(r => r["code"]!.GetValue<string>()).ShouldBe(["BUILD", "ICT"]);
        (await RunAsync(query, shape, new() { ["minStaff"] = null })).Total.ShouldBe(5, "a filter left empty is not applied");
        (await Should.ThrowAsync<ReportInputException>(() => RunAsync(query, shape, new() { ["minStaff"] = "many" }))).Errors.Keys.ShouldBe(["minStaff"]);
    }

    [Fact]
    public async Task Refuses_aggregates_that_do_not_fit_and_unknown_viewers()
    {
        var problems = await CheckAsync(Query(), new ReportShape(
            [new("name", "s.name_en", ReportAggregates.Sum), new("flag", "s.is_default", ReportAggregates.Max), new("name", "s.code"), new("x", "s.code", "MEDIAN")],
            [new("t", null, ReportAggregates.Sum)],
            [new("missing")],
            Totals: false));
        problems.Keys.ShouldBe(
            ["columns[0].aggregate", "columns[1].aggregate", "columns[2].name", "columns[3].aggregate", "tiles[0].column", "sort[0].column"],
            ignoreOrder: true);
        (await CheckAsync(Query(), new ReportShape([], [], [], false))).Keys.ShouldBe(["columns"]);
        (await CheckAsync(Query(new { match = "ALL", conditions = new[] { new { column = "s.staff", op = "GE", param = "nope" } } }), ByParent)).Keys.ShouldBe(["query.where.conditions[0].param"]);

        await Should.ThrowAsync<ReportAccessException>(() => RunAsync(Query(), ByParent, viewer: new ReportViewer("nobody-" + Guid.NewGuid().ToString("N"))));
        await _harness.AccessAsync("admin");
        (await RunAsync(Query(), ByParent, viewer: new ReportViewer("admin"))).Total.ShouldBe(3, "a stored user's own access: groups ICT, BUILD and the top level");
        (await _harness.AsAsync("admin", sp => sp.GetRequiredService<IReportQueries>().CanAsync(new ReportViewer("admin"), "apps-control", "VIEW", CancellationToken.None))).ShouldBeTrue();
    }

    [Fact]
    public async Task Rows_of_a_table_keep_the_viewers_company_and_branches()
    {
        var (company, other, riyadh, jeddah) = (Harness.Unique("CO"), Harness.Unique("CO2"), Harness.Unique("RUH"), Harness.Unique("JED"));
        var (role, user) = (Harness.Unique("ROLE"), Harness.Unique("staff").ToLowerInvariant());
        await _harness.PublishAsync(AppsControlAreas.System,
            new Change(AppsControlChangeTypes.Company, company, ChangeOperation.Create, new { nameAr = "شركة", nameEn = "Company" }),
            new Change(AppsControlChangeTypes.Company, other, ChangeOperation.Create, new { nameAr = "شركة", nameEn = "Other" }),
            new Change(AppsControlChangeTypes.Branch, riyadh, ChangeOperation.Create, new { nameAr = "فرع", nameEn = "Riyadh", companyCode = company }),
            new Change(AppsControlChangeTypes.Branch, jeddah, ChangeOperation.Create, new { nameAr = "فرع", nameEn = "Jeddah", companyCode = company }));
        await _harness.PublishAsync(AppsControlAreas.Security, new Change(AppsControlChangeTypes.Role, role, ChangeOperation.Create, new
        {
            nameAr = "دور",
            nameEn = "Role",
            privileges = new[] { new { resourceCode = AppsControlResources.Classes, actions = new[] { "VIEW" } } },
        }));
        await _harness.PublishAsync(AppsControlAreas.Security, new Change(AppsControlChangeTypes.User, user, ChangeOperation.Create, new
        {
            nameAr = "موظف",
            nameEn = "Staff",
            roles = new[] { new { roleCode = role, companyCode = company, branches = new[] { riyadh } } },
        }));

        var table = Harness.Unique("rpt_req").ToLowerInvariant();
        await _harness.AsAsync("admin", async sp => await Sql.QueryAsync(
            sp.GetRequiredService<Persistence.AppsControlDbContext>(),
            $"CREATE TABLE apps.{table} (code text, company_code text, branch_code text); "
            + $"INSERT INTO apps.{table} VALUES ('A', '{company}', '{riyadh}'), ('B', '{company}', '{jeddah}'), ('C', '{company}', NULL), ('D', '{other}', NULL)"));
        var dataObject = "APPS_" + table.ToUpperInvariant();
        await _harness.PublishAsync(AppsControlAreas.Lovs, new Change(AppsControlChangeTypes.DataObject, dataObject, ChangeOperation.Create, new
        {
            nameAr = "طلبات",
            nameEn = "Requests",
            businessApp = "",
            dbSchema = "apps",
            dbObject = table,
            objectType = "TABLE",
            columns = new[] { new { name = "code", dataType = "TEXT" }, new { name = "company_code", dataType = "TEXT" }, new { name = "branch_code", dataType = "TEXT" } },
        }));

        var query = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(new { from = new { kind = "DATA_OBJECT", code = dataObject, alias = "r" } }))!;
        var shape = new ReportShape([new("code", "r.code")], [], [new("code")], Totals: false);
        async Task<IEnumerable<string>> Codes(ReportViewer viewer) =>
            (await RunAsync(query, shape, viewer: viewer)).Rows.Select(r => r["code"]!.GetValue<string>());

        (await Codes(new ReportViewer(user, company))).ShouldBe(["A", "C"], "their company, their branch and the company-wide row");
        await _harness.AccessAsync("admin");
        (await Codes(new ReportViewer("admin", company))).ShouldBe(["A", "B", "C"], "the company they work in, all its branches");
        (await Codes(new ReportViewer("admin", other))).ShouldBe(["D"]);
    }

    [Fact]
    public async Task A_date_groups_by_a_period_and_rows_count_without_a_column()
    {
        var table = Harness.Unique("rpt_day").ToLowerInvariant();
        await _harness.AsAsync("admin", async sp => await Sql.QueryAsync(
            sp.GetRequiredService<Persistence.AppsControlDbContext>(),
            $"CREATE TABLE apps.{table} (code text, submitted_at timestamptz, fee numeric); "
            + $"INSERT INTO apps.{table} VALUES ('A', '2026-01-05T10:00:00Z', 100), ('B', '2026-01-20T10:00:00Z', 300), ('C', '2026-03-02T10:00:00Z', 50), ('D', NULL, 10)"));
        var dataObject = "APPS_" + table.ToUpperInvariant();
        await _harness.PublishAsync(AppsControlAreas.Lovs, new Change(AppsControlChangeTypes.DataObject, dataObject, ChangeOperation.Create, new
        {
            nameAr = "أيام",
            nameEn = "Days",
            businessApp = "",
            dbSchema = "apps",
            dbObject = table,
            objectType = "TABLE",
            columns = new[] { new { name = "code", dataType = "TEXT" }, new { name = "submitted_at", dataType = "TIMESTAMPTZ" }, new { name = "fee", dataType = "NUMERIC" } },
        }));

        var query = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(new { from = new { kind = "DATA_OBJECT", code = dataObject, alias = "d" } }))!;
        var shape = new ReportShape(
            [new("month", "d.submitted_at", null, ReportBuckets.Month), new("requests", "", ReportAggregates.Count), new("average", "d.fee", ReportAggregates.Avg)],
            [], [new("month")], Totals: false);
        (await CheckAsync(query, shape)).ShouldBeEmpty();
        var result = await RunAsync(query, shape);
        result.Columns[0].Type.ShouldBe("DATE");
        result.Rows.Select(r => (r["month"]?.ToString(), r["requests"]!.GetValue<decimal>(), r["average"]!.GetValue<decimal>()))
            .ShouldBe([("2026-01-01", 2m, 200m), ("2026-03-01", 1m, 50m), (null, 1m, 10m)], "by month, every row counted");

        var bad = await CheckAsync(query, new ReportShape(
            [new("code", "d.code", null, ReportBuckets.Month), new("fee", "d.fee", ReportAggregates.Sum, ReportBuckets.Year), new("x", "d.submitted_at", null, "DECADE")], [], [], false));
        bad.Keys.ShouldBe(["columns[0].bucket", "columns[1].bucket", "columns[2].bucket"], ignoreOrder: true);
    }
}
