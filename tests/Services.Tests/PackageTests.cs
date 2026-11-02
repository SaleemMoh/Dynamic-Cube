using System.Text.Json.Nodes;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Runtime;
using DynamicCube.Platform.Configuration;
using DynamicCube.Platform.Expressions;
using Shouldly;

namespace DynamicCube.Modules.Services.Tests;

/// <summary>Service levels as packages: features, a default, a recommendation; fees by formula kept between a least and a most.</summary>
public sealed class PackageTests
{
    private static readonly JsonLogicEvaluator Evaluator = new();

    private static VersionDefinition Definition() => new()
    {
        LevelFeatures = [new() { Code = "PRIORITY", NameAr = "الأولوية", NameEn = "Priority" }, new() { Code = "ADVISER", NameAr = "مستشار", NameEn = "Adviser" }],
        Levels =
        [
            new() { Code = "BASIC", NameAr = "أساسي", NameEn = "Basic", SlaWorkingDays = 20, IsDefault = true, Features = { ["PRIORITY"] = JsonValue.Create("Standard") } },
            new()
            {
                Code = "PREMIUM",
                NameAr = "متميز",
                NameEn = "Premium",
                SlaWorkingDays = 5,
                RecommendWhen = JsonNode.Parse("""{">": [{"var": "data.ASSETS"}, 100000000]}"""),
                Features = { ["PRIORITY"] = JsonValue.Create("Highest"), ["ADVISER"] = JsonValue.Create(true) },
            },
        ],
        Steps = [new() { Code = "LEVEL", Type = StepType.Level, TitleAr = "المسار", TitleEn = "Path" }],
        Fees =
        [
            new()
            {
                Code = "REVIEW",
                NameAr = "رسوم الدراسة",
                NameEn = "Review fee",
                Formula = JsonNode.Parse("""{"*": [{"+": [{"var": "data.ASSETS"}, {"var": "data.REVENUE"}]}, 0.0005]}"""),
                Minimum = 3000,
                Maximum = 175000,
                VatIncluded = false,
                NoteEn = "0.05% of assets and revenues, 3,000 to 175,000",
            },
        ],
    };

    private static decimal Fee(VersionDefinition definition, string level, decimal? assets, decimal? revenue)
    {
        var data = new JsonObject();
        if (assets is not null)
        {
            data["ASSETS"] = assets;
        }

        if (revenue is not null)
        {
            data["REVENUE"] = revenue;
        }

        var line = new FeeCalculator(Evaluator).Compute(definition, level, new JsonObject { ["data"] = data }).Single();
        line.NoteEn.ShouldBe("0.05% of assets and revenues, 3,000 to 175,000");
        return line.Net;
    }

    [Fact]
    public void A_formula_fee_stays_between_its_least_and_its_most()
    {
        var definition = Definition();
        Fee(definition, "BASIC", 20_000_000, 10_000_000).ShouldBe(15_000m);
        Fee(definition, "BASIC", 1_000_000, 0).ShouldBe(3_000m);
        Fee(definition, "BASIC", 900_000_000, 100_000_000).ShouldBe(175_000m);
    }

    [Fact]
    public void Levels_are_checked_for_one_default_known_features_and_valid_rules()
    {
        var definition = Definition();
        var errors = new ValidationErrors();
        DefinitionValidator.Validate(definition, _ => true, Evaluator, errors);
        errors.HasErrors.ShouldBeFalse(string.Join("; ", errors.ToDictionary().Select(e => $"{e.Key}: {string.Join(",", e.Value)}")));

        definition.Levels[1].IsDefault = true;
        definition.Levels[1].Features["GIFT"] = JsonValue.Create(true);
        definition.Fees[0].Minimum = 200_000;
        errors = new ValidationErrors();
        DefinitionValidator.Validate(definition, _ => true, Evaluator, errors);
        errors.ToDictionary().Keys.ShouldBe(["definition.levels", "definition.levels[1].features", "definition.fees[0].maximum"], ignoreOrder: true);
    }
}
