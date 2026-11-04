using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Lovs;
using DynamicCube.Modules.AppsControl.Tests.Infrastructure;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.PublicApi;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.AppsControl.Tests;

/// <summary>Public endpoints' read-data queries and the class values function (ADR-0042).</summary>
public sealed class DataQueryTests(AppsControlFixture fixture) : IAsyncLifetime
{
    private Harness _harness = null!;
    private string _sector = null!;
    private string _main = null!;
    private string _sub = null!;
    private string _banks = null!;

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
                attributes = new[] { new { code = "ACTIVE_SINCE", nameAr = "منذ", nameEn = "Since", dataType = "NUMBER" } },
            }),
            DataDictionaryTests.DomainChange(_sector, "CONSTRUCTION", "مقاولات", "Construction", new { ACTIVE_SINCE = 1990 }),
            DataDictionaryTests.DomainChange(_sector, "ICT", "تقنية المعلومات", "ICT", new { ACTIVE_SINCE = 2010 }),
            DataDictionaryTests.DomainChange(_sector, "CONSULTING", "استشارات", "Consulting", new { ACTIVE_SINCE = 2000 }),
            DataDictionaryTests.DomainChange(_sector, "SOFTWARE", "برمجيات", "Software", new { ACTIVE_SINCE = 2015 }, parent: "ICT"));

        (_main, _sub, _banks) = (Harness.Unique("CO"), Harness.Unique("CO"), Harness.Unique("BANKS"));
        await _harness.PublishAsync(AppsControlAreas.System,
            new Change(AppsControlChangeTypes.Company, _main, ChangeOperation.Create, new { nameAr = "شركة", nameEn = "Main" }),
            new Change(AppsControlChangeTypes.Company, _sub, ChangeOperation.Create, new { nameAr = "شركة", nameEn = "Subsidiary" }));
        await _harness.PublishAsync(AppsControlAreas.DataDictionary,
            new Change(AppsControlChangeTypes.Class, _banks, ChangeOperation.Create, new { nameAr = "بنوك", nameEn = "Banks", valuesPerCompany = true, attributes = Array.Empty<object>() }));
        Change Bank(string code, string[] companies) =>
            new(AppsControlChangeTypes.Domain, $"{_banks}/{code}", ChangeOperation.Create, new { nameAr = "بنك", nameEn = code, companies });
        await _harness.PublishAsync(AppsControlAreas.DataDictionary, Bank("SHARED", ["*"]), Bank("MAIN_ONLY", [_main]), Bank("SUB_ONLY", [_sub]));
    }

    public ValueTask DisposeAsync() => _harness.DisposeAsync();

    private JsonNode Query(object? where = null, object? from = null) => JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(new
    {
        from = from ?? new { kind = "CLASS", code = _sector, alias = "s" },
        where,
        sort = new[] { new { column = "s.active_since", direction = "ASC" } },
    }))!;

    private static readonly DataField[] SectorFields = [new("code", "s.code"), new("name", "s.name_en"), new("since", "s.active_since")];

    [Fact]
    public async Task Rows_return_the_chosen_columns_typed_page_by_page_and_inputs_filter_them()
    {
        var query = Query(new { match = "ALL", conditions = new[] { new { column = "s.active_since", op = "GE", param = "since" } } });
        (await CheckAsync(query, SectorFields, ["since"])).ShouldBeEmpty();

        var first = await RunAsync(query, SectorFields, new Dictionary<string, string?> { ["since"] = "2000" }, ApiCaller.Unlimited, 2, 0);
        first.Rows.Select(r => r["code"]!.GetValue<string>()).ShouldBe(["CONSULTING", "ICT"]);
        first.Rows[0]["since"]!.GetValue<decimal>().ShouldBe(2000m, "numbers come back as numbers");
        first.Rows[0].Select(p => p.Key).ShouldBe(["code", "name", "since"]);
        first.HasMore.ShouldBeTrue();
        var second = await RunAsync(query, SectorFields, new Dictionary<string, string?> { ["since"] = "2000" }, ApiCaller.Unlimited, 2, 2);
        second.Rows.ShouldHaveSingleItem()["code"]!.GetValue<string>().ShouldBe("SOFTWARE");
        second.HasMore.ShouldBeFalse();

        (await RunAsync(query, SectorFields, new Dictionary<string, string?> { ["since"] = null }, ApiCaller.Unlimited, 10, 0))
            .Rows.Count.ShouldBe(4, "an input left out drops its condition");

        var bad = await Should.ThrowAsync<ApiInputException>(() =>
            RunAsync(query, SectorFields, new Dictionary<string, string?> { ["since"] = "soon" }, ApiCaller.Unlimited, 10, 0));
        bad.Errors.Keys.ShouldBe(["since"], "an input that does not fit its column is the caller's mistake");

        var injected = await RunAsync(
            Query(new { match = "ALL", conditions = new[] { new { column = "s.code", op = "EQ", param = "code" } } }),
            SectorFields,
            new Dictionary<string, string?> { ["code"] = "x' OR '1'='1" },
            ApiCaller.Unlimited,
            10,
            0);
        injected.Rows.ShouldBeEmpty("an input is only ever compared with, as a value");
    }

    [Fact]
    public async Task A_definition_is_checked_for_its_columns_and_inputs()
    {
        var problems = await CheckAsync(
            Query(new
            {
                match = "ALL",
                conditions = new object[]
            {
                new { column = "s.code", op = "EQ", param = "unknown" },
                new { column = "s.code", op = "IS_EMPTY", param = "code" },
            }
            }),
            [new("1st", "s.code"), new("name", "s.name_en"), new("name", "s.code"), new("x", "s.nope")],
            ["code"]);
        problems.Keys.ShouldBe(
            ["fields[0].name", "fields[2].name", "fields[3].column", "query.where.conditions[0].param", "query.where.conditions[1].op"],
            ignoreOrder: true);
        (await CheckAsync(Query(), [], [])).Keys.ShouldBe(["fields"]);
        (await CheckAsync(Query(from: new { kind = "TABLE", code = "apps.class", alias = "s" }), SectorFields, [])).Keys.ShouldContain("query.from.code", "only registered tables and classes");
    }

    [Fact]
    public async Task A_limited_caller_sees_the_class_values_of_its_company()
    {
        var fields = new DataField[] { new("code", "b.code") };
        var query = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(new { from = new { kind = "CLASS", code = _banks, alias = "b" }, sort = new[] { new { column = "b.code", direction = "ASC" } } }))!;
        (await RunAsync(query, fields, new Dictionary<string, string?>(), new ApiCaller("P", null, _main, null), 10, 0))
            .Rows.Select(r => r["code"]!.GetValue<string>()).ShouldBe(["MAIN_ONLY", "SHARED"]);
        (await RunAsync(query, fields, new Dictionary<string, string?>(), new ApiCaller("P", null, _sub, null), 10, 0))
            .Rows.Select(r => r["code"]!.GetValue<string>()).ShouldBe(["SHARED", "SUB_ONLY"]);
        (await RunAsync(query, fields, new Dictionary<string, string?>(), ApiCaller.Unlimited, 10, 0)).Rows.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Class_values_are_listed_by_class_and_parent_for_the_callers_companies()
    {
        var all = await InvokeAsync(new Dictionary<string, string?> { ["class"] = _sector }, ApiCaller.Unlimited);
        var values = all.Data.ShouldBeOfType<JsonArray>();
        values.Select(v => v!["code"]!.GetValue<string>()).ShouldBe(["CONSTRUCTION", "CONSULTING", "ICT", "SOFTWARE"], ignoreOrder: true);
        values.Single(v => v!["code"]!.GetValue<string>() == "SOFTWARE")!["parentCode"]!.GetValue<string>().ShouldBe("ICT");

        var children = await InvokeAsync(new Dictionary<string, string?> { ["class"] = _sector, ["parent"] = "ICT" }, ApiCaller.Unlimited);
        children.Data.ShouldBeOfType<JsonArray>().ShouldHaveSingleItem()!["code"]!.GetValue<string>().ShouldBe("SOFTWARE");

        var banks = await InvokeAsync(new Dictionary<string, string?> { ["class"] = _banks }, new ApiCaller("P", null, _sub, null));
        banks.Data.ShouldBeOfType<JsonArray>().Select(v => v!["code"]!.GetValue<string>()).ShouldBe(["SHARED", "SUB_ONLY"], ignoreOrder: true);

        (await InvokeAsync(new Dictionary<string, string?> { ["class"] = Harness.Unique("NONE") }, ApiCaller.Unlimited)).Found.ShouldBeFalse();
        (await Should.ThrowAsync<ApiInputException>(() => InvokeAsync(new Dictionary<string, string?>(), ApiCaller.Unlimited))).Errors.Keys.ShouldBe(["class"]);
    }

    [Fact]
    public async Task Several_classes_and_public_lists_come_in_one_call_for_the_callers_companies()
    {
        var (open, closed) = (Harness.Unique("LOV"), Harness.Unique("LOV"));
        Change Lov(string code, string classCode, bool isPublic) => new(AppsControlChangeTypes.Lov, code, ChangeOperation.Create, new
        {
            nameAr = "قائمة " + code,
            nameEn = "List " + code,
            sourceType = "CLASS",
            classCode,
            isPublic,
            maxRows = 2,
        });
        await _harness.PublishAsync(AppsControlAreas.Lovs, Lov(open, _banks, true), Lov(closed, _sector, false));

        var both = await LookupsAsync(new Dictionary<string, string?> { ["classes"] = $"{_sector}, {_banks}", ["lovs"] = open }, new ApiCaller("P", null, _sub, null));
        var data = both.Data.ShouldBeOfType<JsonObject>();
        data["classes"]!.AsObject().Select(c => c.Key).ShouldBe([_sector, _banks]);
        data["classes"]![_sector]!.AsArray().Count.ShouldBe(4);
        data["classes"]![_banks]!.AsArray().Select(v => v!["code"]!.GetValue<string>()).ShouldBe(["SHARED", "SUB_ONLY"], ignoreOrder: true);
        data["lovs"]![open]!.AsArray().Select(v => v!["value"]!.GetValue<string>()).ShouldBe(["SHARED", "SUB_ONLY"], ignoreOrder: true, "every page of the list, for the client's company");

        var all = await LookupsAsync(new Dictionary<string, string?> { ["lovs"] = open }, ApiCaller.Unlimited);
        all.Data!["lovs"]![open]!.AsArray().Count.ShouldBe(3, "as the portal serves them to anyone, every company's");
        all.Data!.AsObject().ContainsKey("classes").ShouldBeFalse();

        var refused = await Should.ThrowAsync<ApiInputException>(() => LookupsAsync(
            new Dictionary<string, string?> { ["classes"] = Harness.Unique("NONE"), ["lovs"] = $"{open},{closed}" }, ApiCaller.Unlimited));
        refused.Errors.Keys.ShouldBe(["classes", "lovs"], ignoreOrder: true);
        refused.Errors["lovs"].ShouldHaveSingleItem().ShouldContain(closed, Case.Sensitive, "a list that is not public is not served");
        (await Should.ThrowAsync<ApiInputException>(() => LookupsAsync(new Dictionary<string, string?>(), ApiCaller.Unlimited))).Errors.Keys.ShouldBe(["classes"]);
    }

    [Fact]
    public void The_rows_compiler_reads_inputs_as_values_and_pages()
    {
        var info = new LovQuerySourceInfo("CLASS", "C", "ت", "C", "apps", "v_class_c",
            [new LovSourceColumn("code", "TEXT", LovColumnCategory.Text, null, null), new LovSourceColumn("n", "NUMERIC", LovColumnCategory.Number, null, null)]);
        var definition = new LovQueryDefinition
        {
            From = new LovQuerySource { Kind = "CLASS", Code = "C", Alias = "c" },
            Where = new LovQueryGroup { Conditions = [new LovQueryCondition { Column = "c.n", Op = "LT", Param = "max" }] },
        };
        var facts = new LovUserFacts(null, null, null, [], [], "api:P", "API", [], ["A", "B"]);
        var (compiled, problems) = LovQueryCompiler.CompileRows(definition, (_, _) => info, [new("code", "c.code"), new("n", "c.n")],
            new LovQueryRun(facts, Limit: 11, Offset: 20, Inputs: new Dictionary<string, string?> { ["max"] = "5" }));
        problems.ShouldBeEmpty();
        var sql = compiled.ShouldNotBeNull().Sql;
        sql.ShouldStartWith("SELECT q.f0, q.f1 FROM (SELECT \"c\".\"code\" AS f0, \"c\".\"n\" AS f1 FROM");
        sql.ShouldContain("\"c\".\"n\"::numeric < @q");
        sql.ShouldContain("\"c\".companies && @q");
        sql.ShouldEndWith("ORDER BY q.f0 LIMIT 11 OFFSET 20");
        compiled.Parameters.Values.ShouldContain("5");
        compiled.Fields.ShouldNotBeNull().Select(f => f.Category).ShouldBe([LovColumnCategory.Text, LovColumnCategory.Number]);

        LovQueryCompiler.Check(definition, (_, _) => info).ShouldContain(p => p.Field == "query.where.conditions[0].param", "only an endpoint's query has inputs");
    }

    private Task<IReadOnlyDictionary<string, string[]>> CheckAsync(JsonNode query, IReadOnlyList<DataField> fields, string[] inputs) =>
        _harness.AsAsync("admin", sp => sp.GetRequiredService<IDataQueries>().CheckAsync(query, fields, inputs, CancellationToken.None));

    private Task<DataRows> RunAsync(JsonNode query, IReadOnlyList<DataField> fields, IReadOnlyDictionary<string, string?> inputs, ApiCaller caller, int limit, int offset) =>
        _harness.AsAsync("admin", sp => sp.GetRequiredService<IDataQueries>().RunAsync(query, fields, inputs, caller, limit, offset, CancellationToken.None));

    private Task<ApiFunctionResult> LookupsAsync(IReadOnlyDictionary<string, string?> inputs, ApiCaller caller) =>
        _harness.AsAsync("admin", sp => sp.GetServices<IApiFunction>().Single(f => f.Info.Code == "LOOKUPS").InvokeAsync(inputs, caller, CancellationToken.None));

    private Task<ApiFunctionResult> InvokeAsync(IReadOnlyDictionary<string, string?> inputs, ApiCaller caller) =>
        _harness.AsAsync("admin", sp => sp.GetServices<IApiFunction>().Single(f => f.Info.Code == "CLASS_VALUES").InvokeAsync(inputs, caller, CancellationToken.None));
}
