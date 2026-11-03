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

    [Theory]
    [InlineData("application-324-construction")]
    [InlineData("simulator-construction-medium")]
    public async Task It_reads_a_request_and_grades_as_with_the_values(string name)
    {
        var expected = Load(name);
        var definition = Import().Definition;
        var values = expected["resolvedValues"]!.AsObject();
        var input = new JsonObject { ["rows"] = new JsonArray(expected["sectorCode"]!.GetValue<string>()) };
        var withValues = await Engine().EvaluateAsync(definition.ToJson(), new JsonObject { ["values"] = values.DeepClone(), ["rows"] = input["rows"]!.DeepClone() }, TestContext.Current.CancellationToken);

        // The same values spread over a request: answers, a review task, workflow integration calls and party facts.
        var facts = new JsonObject { ["data"] = new JsonObject(), ["inputs"] = new JsonObject { ["REVIEW"] = new JsonObject() }, ["integrations"] = new JsonObject(), ["party"] = new JsonObject() };
        var call = 0;
        foreach (var i in definition.Inputs)
        {
            var value = values[i.Code]?.DeepClone();
            switch (i.Source.Kind)
            {
                case "applicant":
                    facts["data"]![i.Source.Question ?? i.Code] = value;
                    break;
                case "reviewer":
                    facts["inputs"]!["REVIEW"]![i.Source.Field ?? i.Code] = value;
                    break;
                case "integration":
                    facts["integrations"]![$"CALL{++call}"] = new JsonObject { ["operation"] = i.Source.Operation, ["success"] = true, ["outputs"] = new JsonObject { [i.Source.Output ?? i.Code] = value } };
                    break;
                case "derived" or "projects":
                    // Worked out again by the matrix; a value the reviewed system kept wins as before.
                    if (value is not null)
                    {
                        facts["inputs"]!["REVIEW"]![i.Code] = value;
                        i.Source.AllowOverride = true;
                    }

                    break;
                default:
                    facts[i.Code] = value;
                    break;
            }
        }

        facts["rows"] = input["rows"]!.DeepClone();
        var fromRequest = await Engine().EvaluateAsync(definition.ToJson(), facts, TestContext.Current.CancellationToken);
        var row = fromRequest.Output["rows"]![0]!;
        foreach (var key in new[] { "provisionalGrade", "baseGrade", "finalGrade", "expectedGrade", "eligible" })
        {
            row[key].ShouldBeJson(withValues.Output["rows"]![0]![key], $"{name}: {key}");
            fromRequest.Output[key].ShouldBeJson(row[key], $"{name}: top-level {key}");
        }

        fromRequest.Trace.First()!["step"]!.GetValue<string>().ShouldBe("sources");
        var item = fromRequest.Output["items"]![0]!;
        item["code"]!.GetValue<string>().ShouldBe(expected["sectorCode"]!.GetValue<string>());
        item["technical"].ShouldBeJson(row["columns"]![0]!["grade"]);
    }

    [Fact]
    public void Facts_are_read_from_each_source()
    {
        var definition = new MatrixDefinition
        {
            RowsInput = "sectors",
            Rows = [new() { Code = "construction", Answer = "BUILD" }, new() { Code = "ict" }, new() { Code = "om" }],
            Inputs =
            [
                new() { Code = "sectors", Type = "list", Source = new() { Kind = "applicant", Question = "CLS_SECTORS" } },
                new() { Code = "rating", Source = new() { Kind = "reviewer", Step = "REVIEW", Field = "credit" } },
                new() { Code = "anyTask", Source = new() { Kind = "reviewer", Field = "score" } },
                new() { Code = "employees", Source = new() { Kind = "integration", Operation = "GOSI", Output = "count", AllowOverride = true } },
                new() { Code = "byStep", Source = new() { Kind = "integration", Step = "ZATCA_CALL", Output = "revenue" } },
                new() { Code = "fromForm", Source = new() { Kind = "integration", Operation = "MISSING", Question = "REVENUE" } },
                new() { Code = "cr", Source = new() { Kind = "party", Path = "party.identifiers.CR_NUMBER" } },
                new() { Code = "blank", Source = new() { Kind = "applicant" } },
            ],
        };
        var facts = JsonNode.Parse("""
            {
              "data": { "CLS_SECTORS": ["BUILD", "ict"], "REVENUE": 7, "blank": " " },
              "inputs": { "REVIEW": { "credit": "A" }, "AUDIT": { "score": 3, "employees": 12 } },
              "integrations": {
                "OLD": { "operation": "gosi", "success": true, "outputs": { "count": 5 } },
                "FAILED": { "operation": "GOSI", "success": false, "outputs": { "count": 9 } },
                "ZATCA_CALL": { "operation": "ZATCA", "success": true, "outputs": { "revenue": 40 } }
              },
              "party": { "identifiers": { "CR_NUMBER": "101" } }
            }
            """)!.AsObject();

        MatrixFacts.IsFacts(facts).ShouldBeTrue();
        MatrixFacts.IsFacts(new JsonObject { ["values"] = new JsonObject(), ["data"] = new JsonObject() }).ShouldBeFalse();
        var (values, sources) = MatrixFacts.Resolve(definition, facts);
        values["rating"]!.GetValue<string>().ShouldBe("A");
        values["anyTask"]!.GetValue<int>().ShouldBe(3);
        values["employees"]!.GetValue<int>().ShouldBe(12, "an employee's value replaces the integration's when allowed");
        values["byStep"]!.GetValue<int>().ShouldBe(40);
        values["fromForm"]!.GetValue<int>().ShouldBe(7);
        values["cr"]!.GetValue<string>().ShouldBe("101");
        values["blank"].ShouldBeNull();
        sources.Single(s => s!["input"]!.GetValue<string>() == "blank")!["found"]!.GetValue<bool>().ShouldBeFalse();
        MatrixFacts.Rows(definition, values["sectors"]).OrderBy(r => r).ShouldBe(["construction", "ict"]);
        MatrixFacts.Rows(definition, JsonValue.Create("om, BUILD")).OrderBy(r => r).ShouldBe(["construction", "om"]);
        MatrixFacts.Rows(definition, null).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_request_grades_only_the_rows_applied_for()
    {
        var definition = Import().Definition;
        definition.RowsInput = definition.Inputs[0].Code;
        definition.Rows.Single(r => r.Code == "ict").Answer = "ICT_SECTOR";
        definition.Inputs[0].Source = new MatrixSource { Kind = "applicant", Question = "SECTORS" };
        var facts = new JsonObject { ["data"] = new JsonObject { ["SECTORS"] = new JsonArray("ICT_SECTOR", "catering") } };
        var output = (await Engine().EvaluateAsync(definition.ToJson(), facts, TestContext.Current.CancellationToken)).Output;
        output["rows"]!.AsArray().Select(r => r!["code"]!.GetValue<string>()).ShouldBe(["ict", "catering"]);
        output["items"]!.AsArray().Count.ShouldBe(2);
        output["finalGrade"].ShouldBeNull();

        facts["data"]!["SECTORS"] = null;
        (await Engine().EvaluateAsync(definition.ToJson(), facts, TestContext.Current.CancellationToken)).Output["rows"]!.AsArray().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_shared_calculation_grades_as_the_cell_it_came_from()
    {
        var expected = Load("application-324-construction");
        var definition = Import().Definition;
        var input = new JsonObject { ["values"] = expected["resolvedValues"]!.DeepClone(), ["rows"] = new JsonArray("construction") };
        var before = (await Engine().EvaluateAsync(definition.ToJson(), input.DeepClone().AsObject(), TestContext.Current.CancellationToken)).Output["rows"]![0]!;

        // Every column of the row becomes a shared calculation the row's cells link to.
        foreach (var cell in definition.Cells.Where(c => c.Row == "construction").ToList())
        {
            var code = $"shared-{cell.Column}";
            definition.Shared.Add(new MatrixShared { Code = code, NameAr = code, NameEn = code, Cell = MatrixDefinition.Read(JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(new { cells = new[] { cell } }, MatrixDefinition.Json))!).Cells[0] });
            cell.Kind = "shared";
            cell.Shared = code;
            cell.Groups = [];
        }

        Engine().Validate(definition.ToJson()).ShouldBeEmpty();
        var after = (await Engine().EvaluateAsync(definition.ToJson(), input, TestContext.Current.CancellationToken)).Output["rows"]![0]!;
        foreach (var key in new[] { "provisionalGrade", "baseGrade", "finalGrade", "expectedGrade" })
        {
            after[key].ShouldBeJson(before[key], key);
        }

        after["columns"]![0]!["shared"]!.GetValue<string>().ShouldBe("shared-technical");
        after["columns"]![0]!["score"].ShouldBeJson(before["columns"]![0]!["score"], "score");

        definition.Cells.First(c => c.Kind == "shared").Shared = "nope";
        definition.Shared.Add(new MatrixShared { Code = "bad", Cell = new MatrixCell { Kind = "same" } });
        var errors = Engine().Validate(definition.ToJson());
        errors.Keys.ShouldContain(k => k.EndsWith(".shared", StringComparison.Ordinal));
        errors.Keys.ShouldContain(k => k.StartsWith("shared[", StringComparison.Ordinal) && k.EndsWith(".kind", StringComparison.Ordinal));
    }

    private static int? Grade(JsonNode? node) => MatrixEngine.Number(node) is { } d ? (int)d : null;

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    }
}

internal static class JsonShould
{
    public static void ShouldBeJson(this JsonNode? actual, JsonNode? expected, string? because = null) =>
        JsonNode.DeepEquals(actual, expected).ShouldBeTrue($"{because}: {actual?.ToJsonString() ?? "null"} should be {expected?.ToJsonString() ?? "null"}");

    public static void ShouldBeJson(this JsonNode? actual, string expected) =>
        JsonNode.DeepEquals(actual, JsonNode.Parse(expected)).ShouldBeTrue($"{actual?.ToJsonString()} should be {expected}");
}
