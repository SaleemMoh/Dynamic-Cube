using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Lovs;
using DynamicCube.Modules.AppsControl.Tests.Infrastructure;
using DynamicCube.Platform.ChangeSets;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.AppsControl.Tests;

/// <summary>Lists built in the visual query builder: classes and data objects, joins, filters, the user's data, grants.</summary>
public sealed class LovQueryBuilderTests(AppsControlFixture fixture) : IAsyncLifetime
{
    private Harness _harness = null!;
    private string _sector = null!;
    private string _group = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = new Harness(fixture);
        if (fixture.ConnectionString is null)
        {
            return;
        }

        _sector = Harness.Unique("SECTOR");
        _group = Harness.Unique("GROUP");
        await _harness.PublishAsync(
            AppsControlAreas.Lookups,
            LookupTests.ClassChange(_group, []),
            LookupTests.DomainChange(_group, "BUILD", "البناء", "Building", null),
            LookupTests.DomainChange(_group, "TECH", "التقنية", "Technology", null),
            LookupTests.ClassChange(_sector,
            [
                new { code = "ACTIVE_SINCE", nameAr = "منذ", nameEn = "Since", dataType = "NUMBER" },
                new { code = "GROUP_CODE", nameAr = "المجموعة", nameEn = "Group", dataType = "TEXT" },
                new { code = "OWNER", nameAr = "المسؤول", nameEn = "Owner", dataType = "TEXT" },
            ]),
            LookupTests.DomainChange(_sector, "CONSTRUCTION", "مقاولات", "Construction", new { ACTIVE_SINCE = 1990, GROUP_CODE = "BUILD", OWNER = "admin" }),
            LookupTests.DomainChange(_sector, "ICT", "تقنية المعلومات", "ICT", new { ACTIVE_SINCE = 2010, GROUP_CODE = "TECH", OWNER = "someone" }),
            LookupTests.DomainChange(_sector, "CONSULTING", "استشارات", "Consulting", new { ACTIVE_SINCE = 2000, GROUP_CODE = "BUILD", OWNER = "admin" }));
    }

    public ValueTask DisposeAsync() => _harness.DisposeAsync();

    private object Query(object? where = null, object[]? sort = null, object? map = null, bool distinct = false) => new
    {
        from = new { kind = "CLASS", code = _sector, alias = "s" },
        joins = new[] { new { kind = "CLASS", code = _group, alias = "g", type = "LEFT", on = new[] { new { left = "s.group_code", right = "g.code" } } } },
        map = map ?? new { value = "s.code", labelAr = "s.name_ar", labelEn = "g.name_en" },
        where,
        sort = sort ?? [],
        distinct,
    };

    [Fact]
    public async Task A_query_joins_classes_filters_sorts_and_maps_and_grants_still_apply()
    {
        var lov = Harness.Unique("LOV");
        var role = Harness.Unique("ROLE");
        var user = Harness.Unique("u").ToLowerInvariant();
        await _harness.PublishAsync(AppsControlAreas.Lovs, Lov(lov, Query(
            where: new { match = "ALL", conditions = new[] { new { column = "s.active_since", op = "GE", value = (object)2000 } } },
            sort: [new { column = "s.active_since", direction = "DESC" }])));

        var page = await ValuesAsync("admin", lov, new LovQuery());
        page.Items.Select(i => i.Value).ShouldBe(["ICT", "CONSULTING"], "filtered on the attribute and sorted newest first");
        page.Items[0].LabelEn.ShouldBe("Technology", "the English label comes from the joined class");
        (await ValuesAsync("admin", lov, new LovQuery(Search: "consult"))).Items.ShouldHaveSingleItem().Value.ShouldBe("CONSULTING");

        // A role limited to one value sees only it.
        await _harness.PublishAsync(
            AppsControlAreas.Security,
            new Change(AppsControlChangeTypes.Role, role, ChangeOperation.Create, new
            {
                nameAr = "دور",
                nameEn = "Role",
                grants = new[] { new { lovCode = lov, grantType = "VALUES", grantedValues = new[] { "ICT" } } },
            }),
            new Change(AppsControlChangeTypes.User, user, ChangeOperation.Create, new { nameAr = "أ", nameEn = "A", roles = new[] { new { roleCode = role, companyCode = "*", branches = new[] { "*" } } } }));
        (await ValuesAsync(user, lov, new LovQuery())).Items.ShouldHaveSingleItem().Value.ShouldBe("ICT");
    }

    [Fact]
    public async Task A_condition_on_the_users_data_shows_each_user_their_rows()
    {
        var lov = Harness.Unique("LOV");
        await _harness.PublishAsync(AppsControlAreas.Lovs, Lov(lov, Query(
            where: new { match = "ALL", conditions = new[] { new { column = "s.owner", op = "EQ", user = "USERNAME" } } })));

        (await ValuesAsync("admin", lov, new LovQuery())).Items.Select(i => i.Value).ShouldBe(["CONSTRUCTION", "CONSULTING"], ignoreOrder: true);
        (await ValuesAsync("nobody-" + Harness.Unique("x").ToLowerInvariant(), lov, new LovQuery())).Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Distinct_parent_and_any_groups_work_together()
    {
        var lov = Harness.Unique("LOV");
        await _harness.PublishAsync(AppsControlAreas.Lovs, Lov(lov, Query(
            map: new { value = "g.code", labelAr = "g.name_ar", labelEn = "g.name_en", parent = "g.code" },
            where: new
            {
                match = "ANY",
                conditions = new object[] { new { column = "s.code", op = "IN", value = new[] { "CONSTRUCTION", "CONSULTING" } } },
                groups = new[] { new { match = "ALL", conditions = new[] { new { column = "s.name_en", op = "STARTS_WITH", value = "IC" } } } },
            },
            distinct: true)));

        (await ValuesAsync("admin", lov, new LovQuery())).Items.Select(i => i.Value).ShouldBe(["BUILD", "TECH"], ignoreOrder: true, "each group once");
        (await ValuesAsync("admin", lov, new LovQuery(Parent: "TECH"))).Items.ShouldHaveSingleItem().Value.ShouldBe("TECH");
    }

    [Fact]
    public async Task Problems_are_reported_by_path_and_values_are_never_sql()
    {
        var error = await Should.ThrowAsync<ChangeValidationException>(() => _harness.PublishAsync(AppsControlAreas.Lovs, Lov(Harness.Unique("LOV"), new
        {
            from = new { kind = "CLASS", code = _sector, alias = "S!" },
            joins = new[] { new { kind = "DATA_OBJECT", code = "NOT_REGISTERED", alias = "x", type = "SIDEWAYS", on = Array.Empty<object>() } },
            map = new { value = "s.code\"; DROP TABLE apps.lov; --", labelAr = "s.name_ar" },
            where = new { match = "ALL", conditions = new object[] { new { column = "s.active_since", op = "GT", value = "soon" } } },
        })));
        error.Errors.Keys.ShouldContain("query.from.alias");
        error.Errors.Keys.ShouldContain("query.joins[0].code");
        error.Errors.Keys.ShouldContain("query.map.labelEn");

        var typed = await Should.ThrowAsync<ChangeValidationException>(() => _harness.PublishAsync(AppsControlAreas.Lovs, Lov(Harness.Unique("LOV"), Query(
            where: new { match = "ALL", conditions = new object[] { new { column = "s.active_since", op = "GT", value = "soon" }, new { column = "s.nope", op = "EQ", value = "1" } } }))));
        typed.Errors.Keys.ShouldBe(["query.where.conditions[0].value", "query.where.conditions[1].column"], ignoreOrder: true);

        // A value that looks like SQL is only compared with, as text.
        var lov = Harness.Unique("LOV");
        await _harness.PublishAsync(AppsControlAreas.Lovs, Lov(lov, Query(
            where: new { match = "ALL", conditions = new[] { new { column = "s.code", op = "EQ", value = (object)"x' OR '1'='1" } } })));
        (await ValuesAsync("admin", lov, new LovQuery())).Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_table_registered_in_the_same_change_can_be_queried_and_a_missing_source_reports_once()
    {
        var dataObject = Harness.Unique("OBJ");
        var lov = Harness.Unique("LOV");
        await _harness.PublishAsync(
            AppsControlAreas.Lovs,
            new Change(AppsControlChangeTypes.DataObject, dataObject, ChangeOperation.Create, new
            {
                nameAr = "قطاعات",
                nameEn = "Sectors",
                dbSchema = "apps",
                dbObject = "v_class_" + _sector.ToLowerInvariant(),
                columns = new[] { new { name = "code" }, new { name = "name_ar" }, new { name = "name_en" }, new { name = "active_since" } },
            }),
            Lov(lov, new
            {
                from = new { kind = "DATA_OBJECT", code = dataObject, alias = "t" },
                map = new { value = "t.code", labelAr = "t.name_ar", labelEn = "t.name_en" },
                where = new { match = "ALL", conditions = new[] { new { column = "t.active_since", op = "LT", value = (object)2005 } } },
            }));
        (await ValuesAsync("admin", lov, new LovQuery())).Items.Select(i => i.Value).ShouldBe(["CONSTRUCTION", "CONSULTING"], ignoreOrder: true);

        var missing = await Should.ThrowAsync<ChangeValidationException>(() => _harness.PublishAsync(AppsControlAreas.Lovs, Lov(Harness.Unique("LOV"), new
        {
            from = new { kind = "DATA_OBJECT", code = "NOPE", alias = "t" },
            map = new { value = "t.code", labelAr = "t.name_ar", labelEn = "t.name_en" },
        })));
        missing.Errors.Keys.ShouldBe(["query.from.code"], "the columns of a missing source are not reported again");
    }

    [Fact]
    public async Task Other_tables_are_offered_and_previewed_and_a_saved_list_registers_them_first()
    {
        var table = Harness.Unique("qb_parcel").ToLowerInvariant();
        await _harness.AsAsync("admin", async sp => await Sql.QueryAsync(
            sp.GetRequiredService<Persistence.AppsControlDbContext>(),
            $"CREATE TABLE apps.{table} (code text, name_ar text, name_en text, area numeric); "
            + $"INSERT INTO apps.{table} VALUES ('P1', 'قطعة 1', 'Parcel 1', 500), ('P2', 'قطعة 2', 'Parcel 2', 900)"));

        async Task<IReadOnlyList<LovQuerySourceInfo>> Sources() =>
            await _harness.AsAsync("admin", sp => sp.GetRequiredService<LovQueryService>().SourcesAsync(CancellationToken.None));
        var sources = await Sources();
        var other = sources.Single(s => s.Kind == LovQueryKinds.Table && s.Code == $"apps.{table}");
        other.ObjectType.ShouldBe("TABLE");
        other.Columns.Select(c => c.Name).ShouldBe(["code", "name_ar", "name_en", "area"]);
        other.Columns.Single(c => c.Name == "area").Category.ShouldBe(LovColumnCategory.Number);
        sources.ShouldNotContain(s => s.Kind == LovQueryKinds.Table
            && (s.Code.StartsWith("cfg.", StringComparison.Ordinal) || s.Code.StartsWith("platform.", StringComparison.Ordinal)
                || s.Code.StartsWith("apps.v_class_", StringComparison.Ordinal) || s.Code.Contains("__", StringComparison.Ordinal)),
            "the platform's infrastructure, migration history and class views (offered as classes) are not offered");

        // While building, the table is previewed as it is.
        var definition = new LovQueryDefinition
        {
            From = new LovQuerySource { Kind = LovQueryKinds.Table, Code = $"apps.{table}", Alias = "p" },
            Map = new LovQueryMap { Value = "p.code", LabelAr = "p.name_ar", LabelEn = "p.name_en" },
            Where = new LovQueryGroup { Match = "ALL", Conditions = [new LovQueryCondition { Column = "p.area", Op = "GT", Value = 600 }] },
        };
        var preview = await _harness.AsAsync("admin", async sp => await sp.GetRequiredService<LovQueryService>().PreviewAsync(
            definition, await sp.GetRequiredService<Access.AccessResolver>().GetAsync(CancellationToken.None), 10, null, CancellationToken.None));
        preview.Problems.ShouldBeEmpty();
        preview.Items.ShouldHaveSingleItem().Value.ShouldBe("P2");

        // A saved list never reads an unregistered table.
        object Saved(string kind, string code) => new
        {
            from = new { kind, code, alias = "p" },
            map = new { value = "p.code", labelAr = "p.name_ar", labelEn = "p.name_en" },
        };
        var refused = await Should.ThrowAsync<ChangeValidationException>(() => _harness.PublishAsync(AppsControlAreas.Lovs, Lov(Harness.Unique("LOV"), Saved(LovQueryKinds.Table, $"apps.{table}"))));
        refused.Errors.Keys.ShouldBe(["query.from.code"]);

        // As the builder saves it: the table registered as a data object, with the list, in one change.
        var (dataObject, lov) = ("APPS_" + table.ToUpperInvariant(), Harness.Unique("LOV"));
        await _harness.PublishAsync(
            AppsControlAreas.Lovs,
            new Change(AppsControlChangeTypes.DataObject, dataObject, ChangeOperation.Create, new
            {
                nameAr = "جدول apps." + table,
                nameEn = "apps." + table,
                businessApp = "",
                dbSchema = "apps",
                dbObject = table,
                objectType = "TABLE",
                columns = other.Columns.Select(c => new { name = c.Name, dataType = c.DataType }).ToArray(),
            }),
            Lov(lov, Saved(LovQueryKinds.DataObject, dataObject)));
        (await ValuesAsync("admin", lov, new LovQuery())).Items.Select(i => i.Value).ShouldBe(["P1", "P2"], ignoreOrder: true);
        var after = await Sources();
        after.ShouldNotContain(s => s.Kind == LovQueryKinds.Table && s.Code == $"apps.{table}", "a registered table is offered as registered");
        after.ShouldContain(s => s.Kind == LovQueryKinds.DataObject && s.Code == dataObject);
    }

    [Fact]
    public async Task The_builder_lists_its_sources_and_previews_a_definition()
    {
        var sources = await _harness.AsAsync("admin", sp => sp.GetRequiredService<LovQueryService>().SourcesAsync(CancellationToken.None));
        var sector = sources.Single(s => s.Kind == "CLASS" && s.Code == _sector);
        sector.Columns.Select(c => c.Name).ShouldContain("active_since");
        sector.Columns.Single(c => c.Name == "active_since").Category.ShouldBe(LovColumnCategory.Number);

        var definition = new LovQueryDefinition
        {
            From = new LovQuerySource { Kind = "CLASS", Code = _sector, Alias = "s" },
            Map = new LovQueryMap { Value = "s.code", LabelAr = "s.name_ar", LabelEn = "s.name_en" },
            Sort = [new LovQuerySort { Column = "s.code", Direction = "ASC" }],
        };
        var preview = await _harness.AsAsync("admin", async sp => await sp.GetRequiredService<LovQueryService>().PreviewAsync(
            definition, await sp.GetRequiredService<Access.AccessResolver>().GetAsync(CancellationToken.None), 2, null, CancellationToken.None));
        preview.Problems.ShouldBeEmpty();
        preview.Items.Select(i => i.Value).ShouldBe(["CONSTRUCTION", "CONSULTING"]);
        preview.HasMore.ShouldBeTrue();
        preview.Sql.ShouldNotBeNull().ShouldContain($"FROM \"apps\".\"v_class_{_sector.ToLowerInvariant()}\" AS \"s\"");

        definition.Map.LabelEn = null;
        var broken = await _harness.AsAsync("admin", async sp => await sp.GetRequiredService<LovQueryService>().PreviewAsync(
            definition, await sp.GetRequiredService<Access.AccessResolver>().GetAsync(CancellationToken.None), 5, null, CancellationToken.None));
        broken.Problems.ShouldHaveSingleItem().Field.ShouldBe("query.map.labelEn");
    }

    [Fact]
    public void The_compiler_quotes_names_binds_values_and_keeps_class_rules()
    {
        var info = new LovQuerySourceInfo("CLASS", "C", "ت", "C", "apps", "v_class_c",
            [new LovSourceColumn("code", "TEXT", LovColumnCategory.Text, null, null), new LovSourceColumn("n", "NUMERIC", LovColumnCategory.Number, null, null)]);
        var definition = new LovQueryDefinition
        {
            From = new LovQuerySource { Kind = "CLASS", Code = "C", Alias = "c" },
            Map = new LovQueryMap { Value = "c.code", LabelAr = "c.code", LabelEn = "c.code" },
            Where = new LovQueryGroup { Conditions = [new LovQueryCondition { Column = "c.n", Op = "LT", Value = System.Text.Json.Nodes.JsonValue.Create(5) }] },
        };
        var (compiled, problems) = LovQueryCompiler.Compile(definition, (_, _) => info, new LovQueryRun(new LovUserFacts("CO", null, null, [], [], "u", "INTERNAL", []), Search: "x"));
        problems.ShouldBeEmpty();
        var sql = compiled.ShouldNotBeNull().Sql;
        sql.ShouldContain("\"c\".\"n\"::numeric < @q");
        sql.ShouldContain("\"c\".is_active");
        sql.ShouldContain("= ANY(\"c\".companies)");
        sql.ShouldNotContain("'5'");
        compiled.Parameters.Values.ShouldContain("5");
        compiled.Parameters.Values.ShouldContain("%x%");
    }

    private static Change Lov(string code, object query) =>
        new(AppsControlChangeTypes.Lov, code, ChangeOperation.Create, new { nameAr = "قائمة " + code, nameEn = "List " + code, sourceType = "QUERY", query });

    private Task<LovPage> ValuesAsync(string userName, string lov, LovQuery query) =>
        _harness.AsAsync(userName, async sp =>
            await sp.GetRequiredService<LovService>().GetValuesAsync(
                lov,
                await sp.GetRequiredService<Access.AccessResolver>().GetAsync(CancellationToken.None),
                query,
                publicOnly: false,
                CancellationToken.None));
}
