using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Tests.Infrastructure;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Lookups;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.AppsControl.Tests;

/// <summary>Fields of class values taking several values, values of a list of values or long text; settings of a class.</summary>
public sealed class ClassFieldTests(AppsControlFixture fixture) : IAsyncLifetime
{
    private Harness _harness = null!;

    public ValueTask InitializeAsync()
    {
        _harness = new Harness(fixture);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _harness.DisposeAsync();

    private static Change Class(string code, object[] attributes, object[]? settings = null) =>
        new(AppsControlChangeTypes.Class, code, ChangeOperation.Create, new { nameAr = "تصنيف", nameEn = "Class " + code, attributes, settings = settings ?? [] });

    private static Change Value(string classCode, string code, object attributes) =>
        new(AppsControlChangeTypes.Domain, $"{classCode}/{code}", ChangeOperation.Create, new { nameAr = "قيمة", nameEn = "Value " + code, attributes });

    [Fact]
    public async Task Values_take_several_codes_of_a_class_or_a_list_and_long_text()
    {
        var (menus, lov, lists) = (Harness.Unique("MENU_OPTIONS"), Harness.Unique("LOV_MENUS"), Harness.Unique("EMAIL_LIST"));
        await _harness.PublishAsync(AppsControlAreas.Lookups,
            Class(menus, []),
            Value(menus, "PAYMENT", new { }),
            Value(menus, "REIMBURSEMENT", new { }),
            Value(menus, "JV", new { }));
        await _harness.PublishAsync(AppsControlAreas.Lovs, new Change(AppsControlChangeTypes.Lov, lov, ChangeOperation.Create, new { nameAr = "قائمة", nameEn = "List", sourceType = "CLASS", classCode = menus }));

        await _harness.PublishAsync(AppsControlAreas.Lookups, Class(lists,
        [
            new { code = "MENU_OPTIONS", nameAr = "خيارات", nameEn = "Menu options", dataType = "DOMAIN_REF", refClassCode = menus, isMultiple = true },
            new { code = "MENU_FROM_LIST", nameAr = "من القائمة", nameEn = "From the list", dataType = "LOV_REF", refLovCode = lov, isMultiple = true },
            new { code = "MAIN_MENU", nameAr = "الرئيسية", nameEn = "Main", dataType = "LOV_REF", refLovCode = lov },
            new { code = "EMAILS", nameAr = "البريد", nameEn = "Emails", dataType = "TEXTAREA" },
        ],
        [new { code = "ACTIVE_FLAG", nameAr = "مؤشر التفعيل", nameEn = "Active flag", value = "1 is active, 0 or empty is not" }]));

        await _harness.PublishAsync(AppsControlAreas.Lookups, Value(lists, "CEO_CFO", new
        {
            MENU_OPTIONS = new[] { "PAYMENT", "REIMBURSEMENT" },
            MENU_FROM_LIST = new[] { "JV" },
            MAIN_MENU = "PAYMENT",
            EMAILS = "first@example.com,\nsecond@example.com",
        }));

        var stored = await _harness.AsAsync("admin", sp => sp.GetRequiredService<ILookupDirectory>().FindAsync(lists, "CEO_CFO", CancellationToken.None));
        stored!.Attributes["MENU_OPTIONS"]!.AsArray().Select(c => c!.GetValue<string>()).ShouldBe(["PAYMENT", "REIMBURSEMENT"]);
        (await _harness.AsAsync("admin", sp => sp.GetRequiredService<ILookupDirectory>().SettingsAsync(lists, CancellationToken.None)))["ACTIVE_FLAG"].ShouldBe("1 is active, 0 or empty is not");

        async Task<IEnumerable<string>> Problems(object attributes) =>
            (await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(AppsControlAreas.Lookups, Value(lists, Harness.Unique("V"), attributes)))).Errors.Keys;
        (await Problems(new { MENU_OPTIONS = new[] { "PAYMENT", "NOPE" } })).ShouldContain("attributes.MENU_OPTIONS");
        (await Problems(new { MENU_OPTIONS = "PAYMENT" })).ShouldContain("attributes.MENU_OPTIONS", "a field taking several values takes a list");
        (await Problems(new { MAIN_MENU = "NOPE" })).ShouldContain("attributes.MAIN_MENU", "the code is not in the list of values");
        (await Problems(new { MENU_FROM_LIST = new[] { "JV", "JV" } })).ShouldContain("attributes.MENU_FROM_LIST", "each value once");
    }

    [Fact]
    public async Task Only_class_and_list_fields_take_several_values_and_a_list_field_names_its_list()
    {
        async Task<IEnumerable<string>> Problems(object attribute) =>
            (await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(AppsControlAreas.Lookups, Class(Harness.Unique("C"), [attribute])))).Errors.Keys;

        (await Problems(new { code = "N", nameAr = "ر", nameEn = "N", dataType = "NUMBER", isMultiple = true })).ShouldContain("attributes[0].isMultiple");
        (await Problems(new { code = "L", nameAr = "ق", nameEn = "L", dataType = "LOV_REF" })).ShouldContain("attributes[0].refLovCode");
        (await Problems(new { code = "L", nameAr = "ق", nameEn = "L", dataType = "LOV_REF", refLovCode = Harness.Unique("NONE") })).ShouldContain("attributes[0].refLovCode");
    }

    [Fact]
    public async Task A_class_with_values_per_company_limits_values_to_companies()
    {
        var (main, sub, cls) = (Harness.Unique("CO"), Harness.Unique("CO"), Harness.Unique("BANKS"));
        await _harness.PublishAsync(AppsControlAreas.System,
            new Change(AppsControlChangeTypes.Company, main, ChangeOperation.Create, new { nameAr = "شركة", nameEn = "Main" }),
            new Change(AppsControlChangeTypes.Company, sub, ChangeOperation.Create, new { nameAr = "شركة", nameEn = "Subsidiary" }));
        await _harness.PublishAsync(AppsControlAreas.Lookups,
            new Change(AppsControlChangeTypes.Class, cls, ChangeOperation.Create, new { nameAr = "بنوك", nameEn = "Banks", valuesPerCompany = true, attributes = Array.Empty<object>() }));
        Change Bank(string code, string[] companies, bool isDefault = false) =>
            new(AppsControlChangeTypes.Domain, $"{cls}/{code}", ChangeOperation.Create, new { nameAr = "بنك", nameEn = code, companies, isDefault });
        await _harness.PublishAsync(AppsControlAreas.Lookups,
            Bank("SHARED", ["*"], isDefault: true), Bank("MAIN_ONLY", [main], isDefault: true), Bank("SUB_ONLY", [sub], isDefault: true));

        var values = await _harness.AsAsync("admin", sp => sp.GetRequiredService<ILookupDirectory>().ValuesAsync(cls, CancellationToken.None));
        values.Where(v => v.Serves(main)).Select(v => v.Code).ShouldBe(["MAIN_ONLY", "SHARED"], ignoreOrder: true);
        values.Where(v => v.Serves(sub)).Select(v => v.Code).ShouldBe(["SHARED", "SUB_ONLY"], ignoreOrder: true);
        values.Where(v => v.IsDefault).Select(v => v.Code).ShouldBe(["MAIN_ONLY", "SUB_ONLY"], ignoreOrder: true, "one default per company");

        (await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(AppsControlAreas.Lookups, Bank("NOWHERE", [Harness.Unique("NONE")]))))
            .Errors.Keys.ShouldContain("companies");
        (await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(AppsControlAreas.Lookups, Bank("UNCHOSEN", []))))
            .Errors.Keys.ShouldContain("companies", "all companies is chosen explicitly (*), not left empty");
        (await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(AppsControlAreas.Lookups,
            new Change(AppsControlChangeTypes.Class, cls, ChangeOperation.Update, new { nameAr = "بنوك", nameEn = "Banks", valuesPerCompany = false, attributes = Array.Empty<object>() }))))
            .Errors.Keys.ShouldContain("valuesPerCompany", "values limited to a company must be opened first");
        var plain = Harness.Unique("PLAIN");
        await _harness.PublishAsync(AppsControlAreas.Lookups, Class(plain, []));
        (await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(AppsControlAreas.Lookups,
            new Change(AppsControlChangeTypes.Domain, $"{plain}/X", ChangeOperation.Create, new { nameAr = "س", nameEn = "X", companies = new[] { main } }))))
            .Errors.Keys.ShouldContain("companies", "the class does not have values per company");
    }
}
