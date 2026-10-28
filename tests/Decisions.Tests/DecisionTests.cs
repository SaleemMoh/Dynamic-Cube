using System.Text.Json.Nodes;
using DynamicCube.Modules.Decisions.Changes;
using DynamicCube.Modules.Decisions.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Decisions;
using DynamicCube.Platform.Expressions;
using DynamicCube.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Decisions.Tests;

public sealed class DecisionTests(DatabaseFixture fixture)
{
    /// <summary>A small grading: performance grade per sector from project values, the worst grade overall.</summary>
    internal static JsonObject Grading() => (JsonObject)JsonNode.Parse("""
        {
          "variables": [
            { "code": "governance", "rule": {"+": [{"if": [{"var": "input.board"}, 40, 0]}, {"if": [{"var": "input.erp"}, 60, 0]}]} },
            { "code": "governanceGrade", "rule": {"if": [{">=": [{"var": "vars.governance"}, 85]}, 1, {">=": [{"var": "vars.governance"}, 50]}, 3, 5]} }
          ],
          "items": {
            "source": {"var": "input.sectors"},
            "as": "sector",
            "variables": [
              { "code": "performance", "rule": {"if": [{">=": [{"var": "input.projects"}, 160]}, 1, {">=": [{"var": "input.projects"}, 50]}, 3, 5]} },
              { "code": "grade", "rule": {"max": [{"var": "vars.performance"}, {"var": "vars.governanceGrade"}]} }
            ],
            "output": { "sector": {"var": "sector"}, "grade": {"var": "vars.grade"} }
          },
          "output": { "worstGrade": {"reduce": [{"var": "items"}, {"max": [{"var": "accumulator"}, {"var": "current.grade"}]}, 0]} }
        }
        """)!;

    [Fact]
    public async Task The_rules_engine_computes_per_item_results_with_a_trace()
    {
        var engine = new RulesEngine(new JsonLogicEvaluator());
        engine.Validate(Grading()).ShouldBeEmpty();

        var result = await engine.EvaluateAsync(Grading(), new JsonObject { ["board"] = true, ["erp"] = true, ["projects"] = 95, ["sectors"] = new JsonArray("CONSTRUCTION", "ICT") }, CancellationToken.None);

        result.Output["worstGrade"]!.GetValue<double>().ShouldBe(3);
        var items = result.Output["items"]!.AsArray();
        items.Select(i => i!["sector"]!.GetValue<string>()).ShouldBe(["CONSTRUCTION", "ICT"]);
        items[0]!["grade"]!.GetValue<double>().ShouldBe(3);
        result.Trace.Select(t => t!["step"]!.GetValue<string>()).ShouldContain("vars.governanceGrade");
        result.Trace.Select(t => t!["step"]!.GetValue<string>()).ShouldContain("items[1].vars.performance");
        result.Trace.Select(t => t!["step"]!.GetValue<string>()).ShouldContain("output.worstGrade");
    }

    [Fact]
    public void The_rules_engine_reports_configuration_problems()
    {
        var engine = new RulesEngine(new JsonLogicEvaluator());
        var errors = engine.Validate(JsonNode.Parse("""
            { "variables": [{ "code": "a b", "rule": 1 }, { "code": "x" }], "items": { "as": "vars", "output": {} }, "extra": 1 }
            """));
        errors.Keys.ShouldBe(["extra", "variables[0].code", "variables[1].rule", "items.source", "items.as", "items.output"], ignoreOrder: true);
        engine.Validate(JsonValue.Create(3)).ShouldContainKey(string.Empty);
    }

    [Fact]
    public async Task Versions_are_checked_by_their_engine_and_every_evaluation_is_recorded()
    {
        await using var host = new TestHost(fixture.RequireConnectionString());
        var code = TestHost.Unique("GRADING");

        var bad = await Should.ThrowAsync<ChangeValidationException>(() => host.PublishAsync(
            DecisionChangeTypes.Area,
            new Change(DecisionChangeTypes.RuleSet, code, ChangeOperation.Create, new { nameAr = "التصنيف", nameEn = "Grading", engine = "RULES" }),
            new Change(DecisionChangeTypes.Version, code + "/1", ChangeOperation.Create, new { nameAr = "1", nameEn = "1", configuration = new { output = new { } } })));
        bad.Errors.ShouldContainKey("configuration.output");
        var noEngine = await Should.ThrowAsync<ChangeValidationException>(() => host.PublishAsync(
            DecisionChangeTypes.Area,
            new Change(DecisionChangeTypes.RuleSet, TestHost.Unique("X"), ChangeOperation.Create, new { nameAr = "س", nameEn = "X", engine = "NOPE" })));
        noEngine.Errors.ShouldContainKey("engine");

        await host.PublishAsync(
            DecisionChangeTypes.Area,
            new Change(DecisionChangeTypes.RuleSet, code, ChangeOperation.Create, new { nameAr = "التصنيف", nameEn = "Grading", engine = "RULES", isPublic = true }),
            new Change(DecisionChangeTypes.Version, code + "/1", ChangeOperation.Create, new { nameAr = "1", nameEn = "1", configuration = Grading() }));

        var subject = Guid.CreateVersion7();
        var input = new JsonObject { ["board"] = true, ["erp"] = true, ["projects"] = 200, ["sectors"] = new JsonArray("OM") };
        var result = await host.AsAsync("analyst", sp => sp.GetRequiredService<IDecisionService>().EvaluateAsync(new DecisionRequest(code, input, "SERVICE_REQUEST", subject, "CLS-1"), CancellationToken.None));
        result.Version.ShouldBe(1);
        result.Output["worstGrade"]!.GetValue<double>().ShouldBe(1);

        var log = await host.AsAsync("admin", sp => sp.GetRequiredService<DecisionService>().ListAsync("SERVICE_REQUEST", subject, null, 10, CancellationToken.None));
        log.Single().By.ShouldBe("analyst");
        log.Single().Input["projects"]!.GetValue<int>().ShouldBe(200);
        log.Single().Trace.Count.ShouldBeGreaterThan(3);

        // Published versions never change; a new version is used from then on, an older one can still be tried.
        var change = await Should.ThrowAsync<ChangeValidationException>(() => host.PublishAsync(
            DecisionChangeTypes.Area,
            new Change(DecisionChangeTypes.Version, code + "/1", ChangeOperation.Update, new { nameAr = "1", nameEn = "1", configuration = new { output = new { x = 1 } } })));
        change.Errors.ShouldContainKey("configuration");
        await host.PublishAsync(DecisionChangeTypes.Area, new Change(DecisionChangeTypes.Version, code + "/2", ChangeOperation.Create, new { nameAr = "2", nameEn = "2", configuration = new { output = new { worstGrade = 9 } } }));
        (await host.AsAsync("analyst", sp => sp.GetRequiredService<IDecisionService>().EvaluateAsync(new DecisionRequest(code, input, "SERVICE_REQUEST", subject, null), CancellationToken.None))).Version.ShouldBe(2);
        var tried = await host.AsAsync("maker", sp => sp.GetRequiredService<DecisionService>().TryAsync(code, new TryRequest(input, 1), CancellationToken.None));
        tried.Output["worstGrade"]!.GetValue<double>().ShouldBe(1);

        // A public calculator answers without recording anything.
        var calculated = await host.AsAsync("anonymous", sp => sp.GetRequiredService<DecisionService>().CalculateAsync(code, input, CancellationToken.None));
        calculated["worstGrade"]!.GetValue<int>().ShouldBe(9);
        (await host.AsAsync("admin", sp => sp.GetRequiredService<DecisionService>().ListAsync(null, null, code, 50, CancellationToken.None))).Count.ShouldBe(3);
    }

    [Fact]
    public async Task Unknown_or_failing_rule_sets_raise_decision_errors()
    {
        await using var host = new TestHost(fixture.RequireConnectionString());
        await Should.ThrowAsync<DecisionNotFoundException>(() => host.AsAsync("analyst", sp => sp.GetRequiredService<IDecisionService>().EvaluateAsync(new DecisionRequest("NOPE", [], "T", Guid.Empty, null), CancellationToken.None)));

        var code = TestHost.Unique("PRIVATE");
        await host.PublishAsync(
            DecisionChangeTypes.Area,
            new Change(DecisionChangeTypes.RuleSet, code, ChangeOperation.Create, new { nameAr = "خاص", nameEn = "Private", engine = "RULES" }),
            new Change(DecisionChangeTypes.Version, code + "/1", ChangeOperation.Create, new { nameAr = "1", nameEn = "1", configuration = new { output = new { bad = new Dictionary<string, object> { ["no_such_op"] = 1 } } } }));
        await Should.ThrowAsync<DecisionException>(() => host.AsAsync("analyst", sp => sp.GetRequiredService<IDecisionService>().EvaluateAsync(new DecisionRequest(code, [], "T", Guid.Empty, null), CancellationToken.None)));
        await Should.ThrowAsync<DecisionNotFoundException>(() => host.AsAsync("anonymous", sp => sp.GetRequiredService<DecisionService>().CalculateAsync(code, [], CancellationToken.None)));
    }
}
