using System.Text.Json.Nodes;
using DynamicCube.Modules.Decisions.Runtime.Matrix;
using DynamicCube.Platform.Expressions;
using Shouldly;

namespace DynamicCube.Modules.Decisions.Tests;

/// <summary>
/// The scoring matrix against the reviewed classification system: its exported April 2026 guide is imported and must give
/// what that system gave for the same values (two simulator runs and three applications).
/// </summary>
public sealed class MatrixEngineTests
{
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ClassificationGuide");

    private static JsonObject Load(string name) => JsonNode.Parse(File.ReadAllText(Path.Combine(Folder, name + ".json")))!.AsObject();

    private static MatrixImportResult Import() => GuideImport.Convert(Load("ruleset-export"));

    private static MatrixEngine Engine() => new(new JsonLogicEvaluator(), new FixedClock());

    [Fact]
    public void The_guide_imports_as_a_valid_matrix()
    {
        var import = Import();
        import.Warnings.ShouldBeEmpty();
        var definition = import.Definition;
        definition.Rows.Count.ShouldBe(9);
        definition.Columns.Select(c => c.Code).ShouldBe(["technical", "performance", "governance", "financial"]);
        definition.Cells.Count.ShouldBe(36);
        definition.Cells.Sum(c => c.Groups.Sum(g => g.Criteria.Count)).ShouldBe(179);
        definition.Cells.Sum(c => c.Groups.Sum(g => g.Criteria.Sum(x => x.Bands.Count))).ShouldBe(1062);
        definition.Inputs.Count.ShouldBe(88);
        definition.Inputs.Single(i => i.Code == "credit_rating").Source.Kind.ShouldBe("reviewer");
        definition.Inputs.Single(i => i.Code == "cumulative_value_sar_m").Source.ShouldSatisfyAllConditions(s => s.Kind.ShouldBe("projects"), s => s.Function.ShouldBe("sum"));
        definition.Cells.Single(c => c.Row == "ict" && c.Column == "technical").Kind.ShouldBe("formula");
        Engine().Validate(definition.ToJson()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("simulator-construction-medium")]
    [InlineData("simulator-construction-empty")]
    [InlineData("application-324-construction")]
    [InlineData("application-324-ict")]
    [InlineData("application-286-construction")]
    public async Task It_grades_as_the_reviewed_system(string name)
    {
        var expected = Load(name);
        // Each result was graded with a version of the guide: the published one, or an older one kept with it.
        var version = expected["ruleSetVersionId"]?.GetValue<int>();
        var definition = GuideImport.Convert(Load(version == 372 ? "ruleset-export-372" : "ruleset-export")).Definition;
        var input = new JsonObject
        {
            ["values"] = expected["resolvedValues"]!.DeepClone(),
            ["rows"] = new JsonArray(expected["sectorCode"]!.GetValue<string>()),
        };

        var result = await Engine().EvaluateAsync(definition.ToJson(), input, TestContext.Current.CancellationToken);
        var row = result.Output["rows"]!.AsArray().Single()!.AsObject();

        row["size"]?.GetValue<string>().ShouldBe(expected["sizeCategoryCode"]?.GetValue<string>(), name);
        row["eligible"]!.GetValue<bool>().ShouldBe(expected["isEligible"]!.GetValue<bool>(), name);
        Grade(row["provisionalGrade"]).ShouldBe(Grade(expected["nonFinancialGrade"]), $"{name}: provisional grade");
        Grade(row["baseGrade"]).ShouldBe(Grade(expected["baseGrade"]), $"{name}: base grade");
        Grade(row["finalGrade"]).ShouldBe(Grade(expected["finalGrade"]), $"{name}: final grade");
        Grade(row["expectedGrade"]).ShouldBe(Grade(expected["expectedGrade"]), $"{name}: expected grade");
        row["limiting"]!.AsArray().Select(l => l!.GetValue<string>()).ShouldBe(expected["limitingTracks"]!.AsArray().Select(l => l!.GetValue<string>()), name);

        foreach (var track in expected["tracks"]!.AsArray().OfType<JsonObject>())
        {
            var code = track["code"]!.GetValue<string>();
            var column = row["columns"]!.AsArray().OfType<JsonObject>().Single(c => c["code"]!.GetValue<string>() == code);
            Grade(column["grade"]).ShouldBe(Grade(track["grade"]), $"{name} {code}: grade");
            column["pending"]!.GetValue<bool>().ShouldBe(track["isPending"]!.GetValue<bool>(), $"{name} {code}: pending");
            if (track["aggregation"]!.GetValue<string>() == "SumToScale" && !track["isPending"]!.GetValue<bool>())
            {
                ((double)MatrixEngine.Number(column["score"])!).ShouldBe(MatrixEngine.Number(track["score"]) is { } s ? (double)s : 0, 0.001, $"{name} {code}: score");
            }

            foreach (var group in track["groups"]!.AsArray().OfType<JsonObject>())
            {
                var mine = column["groups"]?.AsArray().OfType<JsonObject>().SingleOrDefault(g => g["code"]!.GetValue<string>() == group["code"]!.GetValue<string>());
                if (mine is null)
                {
                    continue;
                }

                mine["minimumMet"]!.GetValue<bool>().ShouldBe(group["minimumMet"]!.GetValue<bool>(), $"{name} {code}/{group["code"]}: minimum");
                foreach (var criterion in group["criteria"]!.AsArray().OfType<JsonObject>())
                {
                    var c = mine["criteria"]!.AsArray().OfType<JsonObject>().Single(x => x["code"]!.GetValue<string>() == criterion["code"]!.GetValue<string>());
                    var at = $"{name} {code}/{criterion["code"]}";
                    c["applicable"]!.GetValue<bool>().ShouldBe(criterion["isApplicable"]!.GetValue<bool>(), at);
                    if (criterion["outputType"]!.GetValue<string>() == "Points")
                    {
                        ((double)(MatrixEngine.Number(c["points"]) ?? 0)).ShouldBe((double)(MatrixEngine.Number(criterion["points"]) ?? 0), 0.001, at);
                    }
                    else if (!track["isPending"]!.GetValue<bool>())
                    {
                        Grade(c["grade"]).ShouldBe(Grade(criterion["grade"]), at);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task Projects_count_by_their_rules_and_derived_inputs_are_worked_out()
    {
        var definition = Import().Definition;
        var projects = new JsonArray(
            new JsonObject { ["location"] = "domestic", ["project_type"] = "execution", ["completion_pct"] = 80, ["start_date"] = "2022-01-01", ["value_sar_m"] = 60 },
            new JsonObject { ["location"] = "domestic", ["project_type"] = "execution", ["completion_pct"] = 30, ["start_date"] = "2023-01-01", ["value_sar_m"] = 500 },
            new JsonObject { ["location"] = "foreign", ["project_type"] = "execution", ["completion_pct"] = 100, ["start_date"] = "2021-06-01", ["value_sar_m"] = 90, ["certified_value_sar_m"] = 40 },
            new JsonObject { ["location"] = "domestic", ["project_type"] = "om", ["completion_pct"] = 40, ["start_date"] = "2010-01-01", ["value_sar_m"] = 70 });
        var input = new JsonObject
        {
            ["values"] = new JsonObject { ["projects"] = projects, ["total_employees"] = 60, ["engineers"] = 9, ["technicians"] = 12 },
            ["rows"] = new JsonArray("construction"),
        };

        var result = await Engine().EvaluateAsync(definition.ToJson(), input, TestContext.Current.CancellationToken);
        var values = result.Output["values"]!.AsObject();
        // Counted: the first (60) and the foreign one at its certified value (40); the unfinished and the too-old are left out.
        MatrixEngine.Number(values["cumulative_value_sar_m"]).ShouldBe(100);
        MatrixEngine.Number(values["largest_project_sar_m"]).ShouldBe(60);
        MatrixEngine.Number(values["engineers_pct"]).ShouldBe(15);
        MatrixEngine.Number(values["value_added_pct"]).ShouldBe(35);
        result.Output["size"]!.GetValue<string>().ShouldBe("medium");
    }

    [Fact]
    public void Guide_formulas_become_JSONLogic()
    {
        GuideExpressions.Convert("IIF([total_employees] > 0, ([engineers] + [technicians]) / [total_employees] * 100, null)").ShouldBeJson("""{"if":[{">":[{"var":"total_employees"},0]},{"*":[{"/":[{"+":[{"var":"engineers"},{"var":"technicians"}]},{"var":"total_employees"}]},100]},null]}""");
        GuideExpressions.Convert("[withdrawn_gov_projects_3y] >= 2 && [withdrawn_gov_entities_3y] >= 2").ShouldBeJson("""{"and":[{">=":[{"var":"withdrawn_gov_projects_3y"},2]},{">=":[{"var":"withdrawn_gov_entities_3y"},2]}]}""");
        var fields = new HashSet<string> { "is_withdrawn", "owner_type", "withdrawn_at", "withdrawn_by_entity" };
        var source = GuideExpressions.ProjectSource("PROJECTS_DISTINCT(withdrawn_by_entity, is_withdrawn && owner_type == gov && withdrawn_at >= as_of - 3y)", fields)!;
        source.Function.ShouldBe("distinct");
        source.Column.ShouldBe("withdrawn_by_entity");
        source.Where.ShouldBeJson("""{"and":[{"var":"is_withdrawn"},{"==":[{"var":"owner_type"},"gov"]},{"<=":[{"var":"withdrawn_at_yearsAgo"},3]}]}""");
        Should.Throw<FormatException>(() => GuideExpressions.Convert("[a] >"));
    }

    [Fact]
    public async Task The_designer_imports_a_guide_and_simulates_an_unsaved_matrix()
    {
        var imported = MatrixDesign.Import(new JsonObject { ["succeeded"] = true, ["data"] = Load("ruleset-export") });
        imported["warnings"]!.AsArray().ShouldBeEmpty();
        var design = new MatrixDesign(new JsonLogicEvaluator(), new FixedClock());
        var expected = Load("simulator-construction-medium");
        var result = await design.SimulateAsync(
            new MatrixSimulation(imported["configuration"]!.AsObject(), new JsonObject { ["values"] = expected["resolvedValues"]!.DeepClone(), ["rows"] = new JsonArray("construction") }),
            TestContext.Current.CancellationToken);
        result.Errors.ShouldBeEmpty();
        Grade(result.Output!["rows"]![0]!["expectedGrade"]).ShouldBe(2);

        var broken = imported["configuration"]!.DeepClone().AsObject();
        broken["cells"]![0]!["scale"] = "NOPE";
        (await design.SimulateAsync(new MatrixSimulation(broken, null), TestContext.Current.CancellationToken)).Errors.Keys.ShouldContain("cells[0].scale");
        Should.Throw<DynamicCube.Platform.Decisions.DecisionException>(() => MatrixDesign.Import(new JsonObject { ["x"] = 1 }));
    }

    private static int? Grade(JsonNode? node) => MatrixEngine.Number(node) is { } d ? (int)d : null;

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    }
}

internal static class JsonShould
{
    public static void ShouldBeJson(this JsonNode? actual, string expected) =>
        JsonNode.DeepEquals(actual, JsonNode.Parse(expected)).ShouldBeTrue($"{actual?.ToJsonString()} should be {expected}");
}
