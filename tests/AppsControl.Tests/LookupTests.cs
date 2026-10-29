using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Modules.AppsControl.Tests.Infrastructure;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.AppsControl.Tests;

public sealed class LookupTests(AppsControlFixture fixture) : IAsyncLifetime
{
    private Harness _harness = null!;

    public ValueTask InitializeAsync()
    {
        _harness = new Harness(fixture);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _harness.DisposeAsync();

    [Fact]
    public async Task Class_and_values_published_through_maker_checker_appear_in_a_typed_view()
    {
        var region = Harness.Unique("REGION");
        var sector = Harness.Unique("SECTOR");
        await _harness.PublishAsync(
            AppsControlAreas.Lookups,
            ClassChange(region, []),
            DomainChange(region, "R1", "الرياض", "Riyadh", null),
            ClassChange(sector,
            [
                new { code = "MIN_CAPITAL", nameAr = "رأس المال", nameEn = "Minimum capital", dataType = "NUMBER", isRequired = true, validation = new { min = 0 } },
                new { code = "LICENSED", nameAr = "مرخص", nameEn = "Licensed", dataType = "BOOL" },
                new { code = "STARTS", nameAr = "البداية", nameEn = "Starts", dataType = "DATE" },
                new { code = "REGION", nameAr = "المنطقة", nameEn = "Region", dataType = "DOMAIN_REF", refClassCode = region },
            ]),
            DomainChange(sector, "CONSTRUCTION", "مقاولات", "Construction", new { MIN_CAPITAL = 50000, LICENSED = true, STARTS = "2026-01-01", REGION = "R1" }));

        var row = await _harness.AsAsync("admin", async sp =>
        {
            var rows = await Sql.QueryAsync(
                sp.GetRequiredService<AppsControlDbContext>(),
                $"SELECT code, name_en, min_capital, licensed, starts, region FROM apps.v_class_{sector.ToLowerInvariant()}");
            return rows.ShouldHaveSingleItem();
        });

        row["code"].ShouldBe("CONSTRUCTION");
        row["min_capital"].ShouldBe(50000m);
        row["licensed"].ShouldBe(true);
        row["starts"].ShouldBe(new DateOnly(2026, 1, 1));
        row["region"].ShouldBe("R1");
    }

    [Fact]
    public async Task Codes_with_hyphens_publish_and_read_as_underscores_in_the_typed_view()
    {
        var region = Harness.Unique("SAUDI-REGION");
        await _harness.PublishAsync(
            AppsControlAreas.Lookups,
            ClassChange(region, [new { code = "GOV-CODE", nameAr = "رمز الإمارة", nameEn = "Emirate code", dataType = "TEXT" }]),
            DomainChange(region, "EASTERN-1", "الشرقية", "Eastern", new Dictionary<string, object> { ["GOV-CODE"] = "5" }));

        var row = await _harness.AsAsync("admin", async sp =>
        {
            var rows = await Sql.QueryAsync(
                sp.GetRequiredService<AppsControlDbContext>(),
                $"SELECT code, gov_code FROM apps.v_class_{region.ToLowerInvariant().Replace('-', '_')}");
            return rows.ShouldHaveSingleItem();
        });
        row["code"].ShouldBe("EASTERN-1");
        row["gov_code"].ShouldBe("5");

        var reserved = await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(
            AppsControlAreas.Lookups,
            ClassChange(Harness.Unique("CLS"), [new { code = "NAME-AR", nameAr = "س", nameEn = "S", dataType = "TEXT" }])));
        reserved.Errors.Keys.ShouldContain("attributes[0].code");

        var sameColumn = await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(
            AppsControlAreas.Lookups,
            ClassChange(Harness.Unique("CLS"),
            [
                new { code = "A-B", nameAr = "س", nameEn = "S", dataType = "TEXT" },
                new { code = "A_B", nameAr = "ص", nameEn = "T", dataType = "TEXT" },
            ])));
        sameColumn.Errors.Keys.ShouldContain("attributes[1].code");

        var sameView = await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(
            AppsControlAreas.Lookups,
            ClassChange(region.Replace('-', '_'), [])));
        sameView.Errors.Keys.ShouldContain("code");
    }

    [Fact]
    public async Task Values_are_validated_against_attribute_definitions()
    {
        var sector = Harness.Unique("SECTOR");
        await _harness.PublishAsync(AppsControlAreas.Lookups, ClassChange(sector,
        [
            new { code = "MIN_CAPITAL", nameAr = "رأس المال", nameEn = "Minimum capital", dataType = "NUMBER", isRequired = true, validation = new { min = 0 } },
        ]));

        var ex = await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(
            AppsControlAreas.Lookups,
            DomainChange(sector, "BAD", "س", "Bad", new { MIN_CAPITAL = -5, COLOR = "red" })));

        ex.Errors.Keys.ShouldContain("attributes.MIN_CAPITAL");
        ex.Errors.Keys.ShouldContain("attributes.COLOR");

        var missing = await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(
            AppsControlAreas.Lookups,
            DomainChange(sector, "EMPTY", "س", "Empty", new { })));
        missing.Errors["attributes.MIN_CAPITAL"].ShouldContain("Required.");
    }

    [Fact]
    public async Task Changing_an_attribute_type_is_refused_while_values_do_not_fit()
    {
        var cls = Harness.Unique("COLOR");
        await _harness.PublishAsync(
            AppsControlAreas.Lookups,
            ClassChange(cls, [new { code = "SHADE", nameAr = "درجة", nameEn = "Shade", dataType = "TEXT" }]),
            DomainChange(cls, "RED", "أحمر", "Red", new { SHADE = "dark" }));

        var ex = await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(
            AppsControlAreas.Lookups,
            ClassChange(cls, [new { code = "SHADE", nameAr = "درجة", nameEn = "Shade", dataType = "NUMBER" }], ChangeOperation.Update)));

        ex.Errors["attributes"].ShouldContain(m => m.Contains("RED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task System_rows_cannot_be_changed_through_change_sets()
    {
        var ex = await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(
            AppsControlAreas.Lookups,
            ClassChange("ENTITY_KIND", [], ChangeOperation.Update)));

        ex.Errors["code"].ShouldContain(m => m.Contains("maintained by the product", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Hierarchical_values_cannot_form_a_cycle()
    {
        var cls = Harness.Unique("ACTIVITY");
        await _harness.PublishAsync(
            AppsControlAreas.Lookups,
            new Change(AppsControlChangeTypes.Class, cls, ChangeOperation.Create, new { nameAr = "نشاط", nameEn = "Activity", isHierarchical = true }),
            DomainChange(cls, "A", "أ", "A", null),
            DomainChange(cls, "B", "ب", "B", null, parent: "A"));

        var ex = await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(
            AppsControlAreas.Lookups,
            DomainChange(cls, "A", "أ", "A", null, parent: "B", ChangeOperation.Update)));

        ex.Errors.Keys.ShouldContain("parentCode");
    }

    [Fact]
    public async Task Export_lists_values_of_every_class_with_parents_first()
    {
        var cls = Harness.Unique("TREE");
        await _harness.PublishAsync(
            AppsControlAreas.Lookups,
            new Change(AppsControlChangeTypes.Class, cls, ChangeOperation.Create, new { nameAr = "شجرة", nameEn = "Tree", isHierarchical = true }),
            DomainChange(cls, "Z", "ي", "Z", null),
            DomainChange(cls, "M", "م", "M", null, parent: "Z"),
            DomainChange(cls, "A", "أ", "A", null, parent: "M"));

        var codes = await _harness.AsAsync("admin", async sp =>
        {
            var applier = sp.GetServices<IConfigApplier>().Single(a => a.EntityType == AppsControlChangeTypes.Domain);
            var rows = await applier.ExportAsync(CancellationToken.None);
            return rows.Select(r => r.GetProperty("code").GetString()!).Where(c => c.StartsWith(cls + "/", StringComparison.Ordinal)).ToList();
        });

        codes.ShouldBe([$"{cls}/Z", $"{cls}/M", $"{cls}/A"]);
    }

    [Fact]
    public async Task Removing_an_attribute_removes_its_values()
    {
        var cls = Harness.Unique("SIZE");
        await _harness.PublishAsync(
            AppsControlAreas.Lookups,
            ClassChange(cls, [new { code = "WIDTH", nameAr = "عرض", nameEn = "Width", dataType = "NUMBER" }]),
            DomainChange(cls, "L", "كبير", "Large", new { WIDTH = 3 }));

        await _harness.PublishAsync(AppsControlAreas.Lookups, ClassChange(cls, [], ChangeOperation.Update));

        var attributes = await _harness.AsAsync("admin", sp => sp.GetRequiredService<AppsControlDbContext>().Domains
            .Where(d => d.Class!.Code == cls && d.Code == "L").Select(d => d.Attributes).SingleAsync());
        attributes.ShouldBe("{}");
    }

    internal static Change ClassChange(string code, object[] attributes, ChangeOperation operation = ChangeOperation.Create) =>
        new(AppsControlChangeTypes.Class, code, operation, new { nameAr = "تصنيف " + code, nameEn = "Class " + code, attributes });

    internal static Change DomainChange(string classCode, string code, string nameAr, string nameEn, object? attributes, string? parent = null, ChangeOperation operation = ChangeOperation.Create) =>
        new(AppsControlChangeTypes.Domain, $"{classCode}/{code}", operation, new { nameAr, nameEn, attributes, parentCode = parent });
}
