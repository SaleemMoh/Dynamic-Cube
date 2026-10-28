using System.Text.Json.Nodes;
using DynamicCube.Platform.Expressions;
using Shouldly;

namespace DynamicCube.Platform.Tests;

public sealed class JsonLogicEvaluatorTests
{
    [Fact]
    public void Unknown_operation_throws()
    {
        var evaluator = new JsonLogicEvaluator();

        Should.Throw<JsonLogicException>(() => evaluator.Evaluate(JsonNode.Parse("""{ "nope": [1] }"""), null));
    }

    [Fact]
    public void Custom_operation_receives_evaluated_arguments()
    {
        var evaluator = new JsonLogicEvaluator()
            .AddOperation("double", (args, _) => JsonValue.Create(args[0]!.GetValue<double>() * 2));

        var result = evaluator.Evaluate(JsonNode.Parse("""{ "double": [{ "var": "x" }] }"""), JsonNode.Parse("""{ "x": 21 }"""));

        result!.GetValue<double>().ShouldBe(42);
    }

    [Fact]
    public void Built_in_operation_cannot_be_replaced()
    {
        var evaluator = new JsonLogicEvaluator();

        Should.Throw<ArgumentException>(() => evaluator.AddOperation("if", (_, _) => null));
    }

    [Fact]
    public void Lazy_operations_do_not_evaluate_skipped_branches()
    {
        var evaluator = new JsonLogicEvaluator();

        // The else branch uses an unknown operation; it must never be evaluated.
        var result = evaluator.Evaluate(JsonNode.Parse("""{ "if": [true, "ok", { "nope": [] }] }"""), null);

        result!.GetValue<string>().ShouldBe("ok");
    }

    [Theory]
    [InlineData("null", false)]
    [InlineData("0", false)]
    [InlineData("\"\"", false)]
    [InlineData("[]", false)]
    [InlineData("\"0\"", true)]
    [InlineData("{}", true)]
    [InlineData("[0]", true)]
    public void Truthiness_matches_jsonlogic(string json, bool expected) =>
        JsonLogicEvaluator.IsTruthy(JsonNode.Parse(json)).ShouldBe(expected);
}
