using System.Text.Json.Nodes;
using DynamicCube.Platform.Expressions;
using Shouldly;

namespace DynamicCube.Platform.Tests;

/// <summary>
/// Runs the cases shared with the Angular evaluator (tests/shared/jsonlogic-cases.json) so both sides behave identically.
/// </summary>
public sealed class JsonLogicConformanceTests
{
    private static readonly Lazy<Dictionary<string, JsonObject>> Cases = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "jsonlogic-cases.json");
        var array = JsonNode.Parse(File.ReadAllText(path))!.AsArray();
        return array.Select(c => c!.AsObject()).ToDictionary(c => c["name"]!.GetValue<string>());
    });

    public static TheoryData<string> CaseNames => [.. Cases.Value.Keys];

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Case_produces_expected_result(string name)
    {
        var testCase = Cases.Value[name];
        var evaluator = new JsonLogicEvaluator();

        var result = evaluator.Evaluate(testCase["rule"], testCase["data"]);

        JsonAssert.DeepEquals(testCase["result"], result).ShouldBeTrue(
            $"expected {testCase["result"]?.ToJsonString() ?? "null"} but got {result?.ToJsonString() ?? "null"}");
    }
}
