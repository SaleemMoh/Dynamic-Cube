using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Lovs;
using DynamicCube.Modules.AppsControl.Tests.Infrastructure;
using DynamicCube.Platform.ChangeSets;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.AppsControl.Tests;

public sealed class LovTests(AppsControlFixture fixture) : IAsyncLifetime
{
    private Harness _harness = null!;
    private string _class = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = new Harness(fixture, new Dictionary<string, string?> { ["AppsControl:LovSqlConnectionString"] = fixture.ConnectionString });
        if (fixture.ConnectionString is null)
        {
            return;
        }

        _class = Harness.Unique("SECTOR");
        await _harness.PublishAsync(
            AppsControlAreas.Lookups,
            LookupTests.ClassChange(_class, [new { code = "ACTIVE_SINCE", nameAr = "منذ", nameEn = "Since", dataType = "NUMBER" }]),
            LookupTests.DomainChange(_class, "CONSTRUCTION", "مقاولات", "Construction", new { ACTIVE_SINCE = 1990 }),
            LookupTests.DomainChange(_class, "ICT", "تقنية المعلومات", "ICT", new { ACTIVE_SINCE = 2010 }),
            LookupTests.DomainChange(_class, "CONSULTING", "استشارات", "Consulting", new { ACTIVE_SINCE = 2000 }));
    }

    public ValueTask DisposeAsync() => _harness.DisposeAsync();

    [Fact]
    public async Task Class_lov_returns_values_in_both_languages_with_search_and_static_filter()
    {
        var all = Harness.Unique("LOV");
        var recent = Harness.Unique("LOV");
        await _harness.PublishAsync(
            AppsControlAreas.Lovs,
            Lov(all, new { sourceType = "CLASS", classCode = _class }),
            Lov(recent, new { sourceType = "CLASS", classCode = _class, staticFilter = new Dictionary<string, object> { [">="] = new object[] { new { var = "attributes.ACTIVE_SINCE" }, 2000 } } }));

        var page = await ValuesAsync("admin", all, new LovQuery());
        page.Items.Select(i => i.Value).ShouldBe(["CONSTRUCTION", "ICT", "CONSULTING"], ignoreOrder: true);
        page.Items.Single(i => i.Value == "ICT").LabelAr.ShouldBe("تقنية المعلومات");

        (await ValuesAsync("admin", all, new LovQuery(Search: "consult"))).Items.ShouldHaveSingleItem().Value.ShouldBe("CONSULTING");
        (await ValuesAsync("admin", recent, new LovQuery())).Items.Select(i => i.Value).ShouldBe(["ICT", "CONSULTING"], ignoreOrder: true);
    }

    [Fact]
    public async Task Grants_filter_values_per_role_with_screen_overrides_and_rules()
    {
        var lov = Harness.Unique("LOV");
        var fixedRole = Harness.Unique("ROLE");
        var ruleRole = Harness.Unique("ROLE");
        var screen = Harness.Unique("screen").ToLowerInvariant();
        var unit = Harness.Unique("UNIT");
        var fixedUser = Harness.Unique("u").ToLowerInvariant();
        var ruleUser = Harness.Unique("u").ToLowerInvariant();
        var plainUser = Harness.Unique("u").ToLowerInvariant();
        var personalUser = Harness.Unique("u").ToLowerInvariant();

        await _harness.PublishAsync(AppsControlAreas.Lovs, Lov(lov, new { sourceType = "CLASS", classCode = _class, requiresGrant = true }));
        var unitType = Harness.Unique("TYPE");
        await _harness.PublishAsync(
            AppsControlAreas.Organization,
            new Change(AppsControlChangeTypes.OrgUnitType, unitType, ChangeOperation.Create, new { nameAr = "نوع", nameEn = "Type" }),
            new Change(AppsControlChangeTypes.OrgUnit, unit, ChangeOperation.Create, new { nameAr = "وحدة", nameEn = "Unit", typeCode = unitType, attributes = new { SECTORS = new[] { "ICT", "CONSULTING" } } }));
        await _harness.PublishAsync(
            AppsControlAreas.Security,
            new Change(AppsControlChangeTypes.Resource, screen, ChangeOperation.Create, new { nameAr = "شاشة", nameEn = "Screen", appCode = "INTERNAL" }),
            new Change(AppsControlChangeTypes.Role, fixedRole, ChangeOperation.Create, new
            {
                nameAr = "ثابت",
                nameEn = "Fixed",
                grants = new object[]
                {
                    new { lovCode = lov, grantType = "VALUES", grantedValues = new[] { "CONSTRUCTION" }, defaultValue = "CONSTRUCTION" },
                    new { lovCode = lov, resourceCode = screen, grantType = "ALL" },
                },
            }),
            new Change(AppsControlChangeTypes.Role, ruleRole, ChangeOperation.Create, new
            {
                nameAr = "قاعدة",
                nameEn = "Rule",
                grants = new[] { new { lovCode = lov, grantType = "RULE", rule = new { var = "user.orgUnitAttributes.SECTORS" } } },
            }),
            new Change(AppsControlChangeTypes.User, fixedUser, ChangeOperation.Create, new { nameAr = "أ", nameEn = "A", roles = new[] { new { roleCode = fixedRole, companyCode = "*", branches = new[] { "*" } } } }),
            new Change(AppsControlChangeTypes.User, ruleUser, ChangeOperation.Create, new
            {
                nameAr = "ب",
                nameEn = "B",
                roles = new[] { new { roleCode = ruleRole, companyCode = "*", branches = new[] { "*" } } },
                assignments = new[] { new { orgUnitCode = unit } },
            }),
            // Values given to one person on one screen, besides their roles.
            new Change(AppsControlChangeTypes.User, personalUser, ChangeOperation.Create, new
            {
                nameAr = "ج",
                nameEn = "C",
                privileges = new[]
                {
                    new { companyCode = "*", resourceCode = screen, actions = Array.Empty<string>(), grants = new[] { new { lovCode = lov, grantType = "VALUES", grantedValues = new[] { "ICT" } } } },
                },
            }));

        (await ValuesAsync(plainUser, lov, new LovQuery())).Items.ShouldBeEmpty();

        var fixedPage = await ValuesAsync(fixedUser, lov, new LovQuery());
        fixedPage.Items.ShouldHaveSingleItem().Value.ShouldBe("CONSTRUCTION");
        fixedPage.DefaultValue.ShouldBe("CONSTRUCTION");
        (await ValuesAsync(fixedUser, lov, new LovQuery(Resource: screen))).Items.Count.ShouldBe(3);

        (await ValuesAsync(ruleUser, lov, new LovQuery())).Items.Select(i => i.Value).ShouldBe(["ICT", "CONSULTING"], ignoreOrder: true);

        (await ValuesAsync(personalUser, lov, new LovQuery(Resource: screen))).Items.ShouldHaveSingleItem().Value.ShouldBe("ICT");
        (await ValuesAsync(personalUser, lov, new LovQuery())).Items.ShouldBeEmpty("personal values hold on their screen only");
    }

    [Fact]
    public async Task Data_object_lov_reads_a_registered_view_and_data_scopes_use_the_same_grants()
    {
        var dataObject = Harness.Unique("OBJ");
        var lov = Harness.Unique("LOV");
        var role = Harness.Unique("ROLE");
        var user = Harness.Unique("u").ToLowerInvariant();
        var view = "v_class_" + _class.ToLowerInvariant();

        await _harness.PublishAsync(
            AppsControlAreas.Lovs,
            new Change(AppsControlChangeTypes.DataObject, dataObject, ChangeOperation.Create, new
            {
                nameAr = "قطاعات",
                nameEn = "Sectors",
                dbSchema = "apps",
                dbObject = view,
                columns = new[] { new { name = "code" }, new { name = "name_ar" }, new { name = "name_en" }, new { name = "active_since" } },
            }),
            Lov(lov, new { sourceType = "DATA_OBJECT", dataObjectCode = dataObject, valueColumn = "code", displayArColumn = "name_ar", displayEnColumn = "name_en" }),
            new Change(AppsControlChangeTypes.DataObject, dataObject, ChangeOperation.Update, new
            {
                nameAr = "قطاعات",
                nameEn = "Sectors",
                dbSchema = "apps",
                dbObject = view,
                columns = new[] { new { name = "code" }, new { name = "name_ar" }, new { name = "name_en" }, new { name = "active_since" } },
                scopeBindings = new[] { new { columnName = "code", lovCode = lov } },
            }));
        await _harness.PublishAsync(
            AppsControlAreas.Security,
            new Change(AppsControlChangeTypes.Role, role, ChangeOperation.Create, new
            {
                nameAr = "دور",
                nameEn = "Role",
                grants = new[] { new { lovCode = lov, grantType = "VALUES", grantedValues = new[] { "ICT" } } },
            }),
            new Change(AppsControlChangeTypes.User, user, ChangeOperation.Create, new { nameAr = "م", nameEn = "U", roles = new[] { new { roleCode = role, companyCode = "*", branches = new[] { "*" } } } }));

        var adminPage = await ValuesAsync("admin", lov, new LovQuery(Search: "con"));
        adminPage.Items.Select(i => i.Value).ShouldBe(["CONSTRUCTION", "CONSULTING"], ignoreOrder: true);
        (await ValuesAsync(user, lov, new LovQuery())).Items.ShouldHaveSingleItem().Value.ShouldBe("ICT");

        var filters = await _harness.AsAsync(user, async sp =>
            await sp.GetRequiredService<DataScopeService>().GetFiltersAsync(
                dataObject,
                await sp.GetRequiredService<Access.AccessResolver>().GetAsync(CancellationToken.None),
                null,
                CancellationToken.None));
        var filter = filters.ShouldHaveSingleItem();
        filter.Column.ShouldBe("code");
        filter.Grant.Values.ShouldBe(["ICT"]);
    }

    [Fact]
    public async Task Data_objects_must_exist_in_an_open_schema()
    {
        var ex = await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(
            AppsControlAreas.Lovs,
            new Change(AppsControlChangeTypes.DataObject, Harness.Unique("OBJ"), ChangeOperation.Create, new
            {
                nameAr = "سجل",
                nameEn = "Audit",
                dbSchema = "audit",
                dbObject = "audit_log",
                columns = new[] { new { name = "id" } },
            })));

        ex.Errors.Keys.ShouldContain("dbSchema");
    }

    [Fact]
    public async Task Restricted_sql_lov_runs_read_only_with_bound_user_parameters()
    {
        var lov = Harness.Unique("LOV");
        var sql = $"SELECT code AS value, name_ar AS display_ar, name_en AS display_en FROM apps.v_class_{_class.ToLowerInvariant()} WHERE @username IS NOT NULL";
        await _harness.PublishAsync(AppsControlAreas.Lovs, Lov(lov, new { sourceType = "SQL", sqlText = sql }));

        (await ValuesAsync("admin", lov, new LovQuery(Search: "ict"))).Items.ShouldHaveSingleItem().LabelEn.ShouldBe("ICT");

        // An existing query used as it is: the LOV names its value and label columns.
        var mapped = Harness.Unique("LOV");
        var legacySql = $"SELECT code, name_ar, name_en FROM apps.v_class_{_class.ToLowerInvariant()}";
        await _harness.PublishAsync(AppsControlAreas.Lovs, Lov(mapped, new { sourceType = "SQL", sqlText = legacySql, valueColumn = "code", displayArColumn = "name_ar", displayEnColumn = "name_en" }));
        var mappedItem = (await ValuesAsync("admin", mapped, new LovQuery(Search: "تقنية"))).Items.ShouldHaveSingleItem();
        mappedItem.Value.ShouldBe("ICT");
        mappedItem.LabelEn.ShouldBe("ICT");

        var wrongColumn = await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(
            AppsControlAreas.Lovs,
            Lov(Harness.Unique("LOV"), new { sourceType = "SQL", sqlText = legacySql, valueColumn = "id" })));
        wrongColumn.Errors["sqlText"].ShouldContain(m => m.Contains("id", StringComparison.Ordinal));

        var missingColumns = await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(
            AppsControlAreas.Lovs,
            Lov(Harness.Unique("LOV"), new { sourceType = "SQL", sqlText = "SELECT 1 AS value" })));
        missingColumns.Errors["sqlText"].ShouldContain(m => m.Contains("display_ar", StringComparison.Ordinal));

        var twoStatements = await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(
            AppsControlAreas.Lovs,
            Lov(Harness.Unique("LOV"), new { sourceType = "SQL", sqlText = sql + "; DELETE FROM apps.domain" })));
        twoStatements.Errors.Keys.ShouldContain("sqlText");
    }

    private static Change Lov(string code, object source)
    {
        var payload = new Dictionary<string, object?> { ["nameAr"] = "قائمة " + code, ["nameEn"] = "List " + code };
        foreach (var property in source.GetType().GetProperties())
        {
            payload[property.Name] = property.GetValue(source);
        }

        return new Change(AppsControlChangeTypes.Lov, code, ChangeOperation.Create, payload);
    }

    private Task<LovPage> ValuesAsync(string userName, string lov, LovQuery query) =>
        _harness.AsAsync(userName, async sp =>
            await sp.GetRequiredService<LovService>().GetValuesAsync(
                lov,
                await sp.GetRequiredService<Access.AccessResolver>().GetAsync(CancellationToken.None),
                query,
                publicOnly: false,
                CancellationToken.None));
}
