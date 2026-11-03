using System.Text.Json.Nodes;
using DynamicCube.Modules.Decisions.Changes;
using DynamicCube.Modules.Decisions.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Decisions.Tests;

/// <summary>A public rule set shown as a calculator on the portal: its fields, results and starting values.</summary>
public sealed class CalculatorTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task A_public_rule_set_is_shown_as_a_calculator_with_its_fields_results_and_prefill()
    {
        await using var host = new TestHost(fixture.RequireConnectionString());
        var code = TestHost.Unique("CALC");
        await host.PublishAsync(
            DecisionChangeTypes.Area,
            new Change(DecisionChangeTypes.RuleSet, code, ChangeOperation.Create, new
            {
                nameAr = "تقدير",
                nameEn = "Estimate",
                engine = "RULES",
                isPublic = true,
                calculatorTitleAr = "حاسبة الفئة",
                calculatorTitleEn = "Band calculator",
                calculatorInputs = new object[]
                {
                    new { code = "capital", labelAr = "رأس المال", labelEn = "Capital", type = "NUMBER", required = true, min = 0, prefill = JsonNode.Parse("""{"var": "party.profile.CR_CAPITAL"}""") },
                    new { code = "years", labelAr = "سنوات الخبرة", labelEn = "Years", type = "NUMBER", required = true },
                },
                calculatorResults = new object[] { new { path = "band", labelAr = "الفئة", labelEn = "Band", format = "TEXT", headline = true } },
            }),
            new Change(DecisionChangeTypes.Version, code + "/1", ChangeOperation.Create, new
            {
                nameAr = "1",
                nameEn = "1",
                configuration = JsonNode.Parse("""{ "output": { "band": {"if": [{"and": [{">=": [{"var": "input.capital"}, 1000000]}, {">=": [{"var": "input.years"}, 5]}]}, "A", "B"]}, "seen": {"var": "input.extra"} } }"""),
            }));

        static DecisionService Service(IServiceProvider sp) => sp.GetRequiredService<DecisionService>();
        (await host.AsAsync("anonymous", sp => Service(sp).CalculatorsAsync(CancellationToken.None))).ShouldContain(c => c.Code == code && c.TitleEn == "Band calculator");
        var view = await host.AsAsync("anonymous", sp => Service(sp).CalculatorAsync(code, CancellationToken.None));
        view.Inputs.Select(i => $"{i.Code}:{i.Prefilled}").ShouldBe(["capital:True", "years:False"]);

        // Only the calculator's fields reach the rule set; numbers typed as text are read as numbers.
        var output = await host.AsAsync("anonymous", sp => Service(sp).CalculateAsync(code, new JsonObject { ["capital"] = "2000000", ["years"] = 6, ["extra"] = "x" }, CancellationToken.None));
        output["band"]!.GetValue<string>().ShouldBe("A");
        output["seen"].ShouldBeNull();

        var prefill = await host.AsAsync("applicant", sp => Service(sp).PrefillAsync(code, new JsonObject { ["profile"] = new JsonObject { ["CR_CAPITAL"] = 500000 } }, CancellationToken.None));
        prefill.ToJsonString().ShouldBe("""{"capital":500000}""");

        // A calculator needs its titles, fields and results, and only a public rule set is shown as one.
        var bad = await Should.ThrowAsync<ChangeValidationException>(() => host.PublishAsync(
            DecisionChangeTypes.Area,
            new Change(DecisionChangeTypes.RuleSet, TestHost.Unique("CALC"), ChangeOperation.Create, new
            {
                nameAr = "س",
                nameEn = "X",
                engine = "RULES",
                calculatorTitleAr = "ح",
                calculatorInputs = new object[] { new { code = "1bad", labelAr = "س", labelEn = "X", type = "SELECT" } },
            })));
        bad.Errors.Keys.ShouldContain("calculatorTitleEn");
        bad.Errors.Keys.ShouldContain("calculatorResults");
        bad.Errors.Keys.ShouldContain("calculatorInputs[0].code");
        bad.Errors.Keys.ShouldContain("calculatorInputs[0].options");
        bad.Errors.Keys.ShouldContain("isPublic");
    }
}
