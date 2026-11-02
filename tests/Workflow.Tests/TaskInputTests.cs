using System.Text.Json.Nodes;
using DynamicCube.Modules.Decisions.Changes;
using DynamicCube.Modules.Workflow.Changes;
using DynamicCube.Modules.Workflow.Domain;
using DynamicCube.Modules.Workflow.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Tests.Infrastructure;
using Shouldly;
using static DynamicCube.Modules.Workflow.Tests.WorkflowHost;

namespace DynamicCube.Modules.Workflow.Tests;

/// <summary>Fields an employee fills in on a task: kept for later rules, and decisions worked out again with them.</summary>
public sealed class TaskInputTests(DatabaseFixture fixture)
{
    private const string Rules = """
        { "output": { "grade": {"if": [{">=": [{"var": "input.score"}, 80]}, 1, {">=": [{"var": "input.score"}, 50]}, 3, 0]},
                      "eligible": {">=": [{"var": "input.score"}, 50]} } }
        """;

    /// <summary>The reviewer's score when there is one, else the applicant's.</summary>
    private static readonly JsonNode Score = JsonNode.Parse("""{"if": [{"var": "inputs.REVIEW.SCORE"}, {"var": "inputs.REVIEW.SCORE"}, {"var": "data.SCORE"}]}""")!;

    private static object Review(string role) => new
    {
        code = "REVIEW",
        type = "HUMAN_TASK",
        nameAr = "مراجعة",
        nameEn = "Review",
        assignment = Role(role),
        actions = new object[] { Act("APPROVE", "ROUTE", "APPROVE"), Return() },
        fields = new object[]
        {
            new { code = "SCORE", labelAr = "الدرجة", labelEn = "Score", type = "NUMBER", required = true, min = 0, max = 100, initial = JsonNode.Parse("""{"var": "data.SCORE"}""") },
            new
            {
                code = "FINDING",
                labelAr = "الملاحظة",
                labelEn = "Finding",
                type = "SELECT",
                options = new[] { new { value = "NONE", labelAr = "لا يوجد", labelEn = "None" }, new { value = "MINOR", labelAr = "بسيطة", labelEn = "Minor" } },
            },
        },
        recalculate = new[] { "GRADE" },
    };

    [Fact]
    public async Task The_reviewers_inputs_rework_the_decision_and_route_the_request()
    {
        await using var host = new WorkflowHost(fixture);
        var ruleSet = Unique("SCORE");
        await host.Host.PublishAsync(
            DecisionChangeTypes.Area,
            new Change(DecisionChangeTypes.RuleSet, ruleSet, ChangeOperation.Create, new { nameAr = "الدرجة", nameEn = "Grade", engine = "RULES" }),
            new Change(DecisionChangeTypes.Version, ruleSet + "/1", ChangeOperation.Create, new { nameAr = "1", nameEn = "1", configuration = JsonNode.Parse(Rules) }));
        var reviewer = User("rev");
        var roles = await host.RolesAsync("REVIEWER");
        await host.UsersAsync((reviewer, roles));
        var workflow = await host.WorkflowAsync(
        [
            Start("GRADE"),
            new { code = "GRADE", type = "DECISION", nameAr = "الدرجة", nameEn = "Grade", ruleSet, input = new { score = Score }, result = "grading", next = "REVIEW" },
            Review(roles[0]),
            Gateway("ROUTE", [new { when = JsonNode.Parse("""{"var": "decisions.grading.eligible"}"""), to = "OK" }], "NO"),
            End("OK", "APPROVED"),
            End("NO", "REJECTED"),
        ]);

        var subject = await host.StartAsync(workflow, new JsonObject { ["SCORE"] = 90 });
        var task = (await host.OpenTasksAsync(subject)).ShouldHaveSingleItem();
        await host.AsAsync(reviewer, c => c.ClaimAsync(task.Id, CancellationToken.None));

        // The fields start from the applicant's figure.
        var detail = await host.AsAsync(reviewer, c => c.GetAsync(task.Id, CancellationToken.None));
        detail.Fields.Count.ShouldBe(2);
        detail.Recalculates.ShouldBeTrue();
        detail.Inputs["SCORE"]!.GetValue<decimal>().ShouldBe(90);

        // Live: a lower score would give no grade; nothing is recorded.
        var preview = (await host.AsAsync(reviewer, c => c.RecalculateAsync(task.Id, new InputsRequest(new JsonObject { ["SCORE"] = 20 }), CancellationToken.None))).ShouldHaveSingleItem();
        preview.Result.ShouldBe("grading");
        preview.Before!["grade"]!.GetValue<double>().ShouldBe(1);
        preview.After!["grade"]!.GetValue<double>().ShouldBe(0);
        JsonNode.Parse((await host.InstanceAsync(subject)).Facts)!["decisions"]!["grading"]!["grade"]!.GetValue<double>().ShouldBe(1);

        // Saved while working, then read back.
        await host.AsAsync(reviewer, c => c.SaveInputsAsync(task.Id, new InputsRequest(new JsonObject { ["SCORE"] = "30", ["IGNORED"] = 1 }), CancellationToken.None));
        (await host.AsAsync(reviewer, c => c.GetAsync(task.Id, CancellationToken.None))).Inputs.ToJsonString().ShouldBe("""{"SCORE":30}""");

        // Moving on needs the score, within its range, and a known choice.
        (await Should.ThrowAsync<WorkflowValidationException>(() => host.AsAsync(reviewer, c => c.ActAsync(task.Id, new ActRequest("APPROVE", null), CancellationToken.None)))).Field.ShouldBe("inputs.SCORE");
        (await Should.ThrowAsync<WorkflowValidationException>(() => host.AsAsync(reviewer, c => c.ActAsync(task.Id, new ActRequest("APPROVE", null, new JsonObject { ["SCORE"] = 150 }), CancellationToken.None)))).Field.ShouldBe("inputs.SCORE");
        (await Should.ThrowAsync<WorkflowValidationException>(() => host.AsAsync(reviewer, c => c.ActAsync(task.Id, new ActRequest("APPROVE", null, new JsonObject { ["SCORE"] = 20, ["FINDING"] = "MAJOR" }), CancellationToken.None)))).Field.ShouldBe("inputs.FINDING");

        await host.AsAsync(reviewer, c => c.ActAsync(task.Id, new ActRequest("APPROVE", null, new JsonObject { ["SCORE"] = 20, ["FINDING"] = "MINOR" }), CancellationToken.None));
        var instance = await host.InstanceAsync(subject);
        instance.Status.ShouldBe(InstanceStatus.Completed);
        instance.Outcome.ShouldBe("REJECTED");
        var facts = JsonNode.Parse(instance.Facts)!;
        facts["inputs"]!["REVIEW"]!.ToJsonString().ShouldBe("""{"SCORE":20,"FINDING":"MINOR"}""");
        facts["decisions"]!["grading"]!["grade"]!.GetValue<double>().ShouldBe(0);
    }

    [Fact]
    public async Task Task_fields_are_checked_when_published()
    {
        await using var host = new WorkflowHost(fixture);
        var code = await host.ServiceAsync();
        var ex = await Should.ThrowAsync<ChangeValidationException>(() => host.Host.PublishAsync(
            WorkflowChangeTypes.Area,
            new Change(WorkflowChangeTypes.Version, code + "/2", ChangeOperation.Create, new
            {
                nameAr = "1",
                nameEn = "1",
                definition = new
                {
                    nodes = new object[]
                    {
                        new { code = "START", type = "START", nameAr = "بداية", nameEn = "Start", next = "REVIEW", fields = new[] { new { code = "X", labelAr = "س", labelEn = "X", type = "TEXT" } } },
                        new
                        {
                            code = "REVIEW",
                            type = "HUMAN_TASK",
                            nameAr = "مراجعة",
                            nameEn = "Review",
                            assignment = Role("R"),
                            actions = new object[] { Act("OK", "DONE") },
                            fields = new object[]
                            {
                                new { code = "PICK", labelAr = "اختر", labelEn = "Pick", type = "SELECT" },
                                new { code = "NOTE", labelAr = "ملاحظة", labelEn = "Note", type = "TEXT", min = 1 },
                            },
                            recalculate = new[] { "DONE" },
                        },
                        End("DONE", "APPROVED"),
                    },
                },
            })));

        ex.Errors.Keys.ShouldContain("definition.nodes[0].fields");
        ex.Errors.Keys.ShouldContain("definition.nodes[1].fields[0].options");
        ex.Errors.Keys.ShouldContain("definition.nodes[1].fields[1].min");
        ex.Errors.Keys.ShouldContain("definition.nodes[1].recalculate");
    }
}
