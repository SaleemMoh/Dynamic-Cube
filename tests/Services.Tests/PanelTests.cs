using System.Text.Json.Nodes;
using DynamicCube.Modules.Decisions.Changes;
using DynamicCube.Modules.Decisions.Persistence;
using DynamicCube.Modules.Services.Changes;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Runtime;
using DynamicCube.Modules.Workflow.Changes;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Services.Tests;

/// <summary>
/// The panel beside the applicant's form (ADR-0040): its definition is checked with the version, and the live result is the
/// decision step of the service's workflow evaluated on the answers on screen, without recording anything.
/// </summary>
public sealed class PanelTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly ServiceSamples _sample = new();
    private readonly string _ruleSet = TestHost.Unique("GRADE");
    private Applicant _applicant = null!;

    public async ValueTask InitializeAsync() => _applicant = await Applicant.CreateAsync(fixture);

    public async ValueTask DisposeAsync() => await _applicant.DisposeAsync();

    private PanelDefinition Panel(bool result = true) => new()
    {
        Assistant = new AssistantPanelDefinition
        {
            Enabled = true,
            TitleAr = "وكيل التصنيف",
            TitleEn = "Classification assistant",
            Lines =
            [
                new() { Step = "SECTORS", Kind = AssistantLineKind.Message, TextAr = "اختر القطاعات", TextEn = "Choose the sectors" },
                new() { Kind = AssistantLineKind.Question, QuestionAr = "ما الدرجة؟", QuestionEn = "What is the grade?", TextAr = "تحسب من البيانات", TextEn = "Worked out from the answers" },
            ],
        },
        Readiness = new ReadinessPanelDefinition { Enabled = true },
        Summary = new SummaryPanelDefinition { Enabled = true, Questions = [_sample.Employees] },
        Result = new ResultPanelDefinition
        {
            Enabled = result,
            Step = "GRADE",
            Headline = new ResultValueDefinition { Output = "grade", LabelAr = "الدرجة المتوقعة", LabelEn = "Expected grade", OnScale = true },
            Scale = [new() { Value = "1", LabelAr = "الأولى", LabelEn = "First" }, new() { Value = "3", LabelAr = "الثالثة", LabelEn = "Third" }],
            UnknownAr = "غير مصنف",
            UnknownEn = "Unclassified",
            Items = new ResultItemsDefinition { Output = "items", TitleOutput = "sector", ResultOutput = "grade" },
        },
    };

    private async Task ConfigureAsync(PanelDefinition? panel)
    {
        var host = _applicant.Host;
        await host.PublishAsync(
            DecisionChangeTypes.Area,
            new Change(DecisionChangeTypes.RuleSet, _ruleSet, ChangeOperation.Create, new { nameAr = "الدرجة", nameEn = "Grade", engine = "RULES" }),
            new Change(DecisionChangeTypes.Version, _ruleSet + "/1", ChangeOperation.Create, new
            {
                nameAr = "1",
                nameEn = "1",
                configuration = JsonNode.Parse("""
                    {
                      "variables": [{ "code": "grade", "rule": {"if": [{">=": [{"var": "input.employees"}, 20]}, 1, 3]} }],
                      "items": { "source": {"var": "input.sectors"}, "as": "sector", "output": { "sector": {"var": "sector"}, "grade": {"var": "vars.grade"} } },
                      "output": { "grade": {"var": "vars.grade"}, "level": {"var": "input.level"} }
                    }
                    """),
            }));
        var definition = _sample.Definition();
        definition.Panel = panel;
        await host.PublishAsync(ServiceChangeTypes.Area, [.. _sample.Questions(), _sample.ServiceChange(), _sample.Version(1, definition)]);
        await host.PublishAsync(
            WorkflowChangeTypes.Area,
            new Change(WorkflowChangeTypes.Version, _sample.Service + "/2", ChangeOperation.Create, new
            {
                nameAr = "2",
                nameEn = "2",
                definition = new
                {
                    nodes = new object[]
                    {
                        new { code = "START", type = "START", nameAr = "بداية", nameEn = "Start", next = "GRADE" },
                        new
                        {
                            code = "GRADE",
                            type = "DECISION",
                            nameAr = "الدرجة",
                            nameEn = "Grade",
                            ruleSet = _ruleSet,
                            input = new { employees = new { var = "data." + _sample.Employees }, sectors = new { var = "data." + _sample.Sectors }, level = new { var = "level" } },
                            result = "grading",
                            next = "DONE",
                        },
                        new { code = "DONE", type = "END", nameAr = "انتهى", nameEn = "Done", outcome = "APPROVED" },
                    },
                },
            }));
    }

    private Task<T> Requests<T>(Func<RequestService, Task<T>> action) =>
        _applicant.AsAsync(sp => action(sp.GetRequiredService<RequestService>()));

    private Task<int> EvaluationsAsync() =>
        _applicant.Host.AsAsync("admin", sp => sp.GetRequiredService<DecisionsDbContext>().Evaluations.CountAsync(e => e.RuleSetCode == _ruleSet));

    [Fact]
    public async Task The_live_result_is_the_workflows_decision_on_the_answers_on_screen_and_is_not_recorded()
    {
        await ConfigureAsync(Panel());
        var draft = await Requests(s => s.CreateAsync(new CreateRequest(_sample.Service), CancellationToken.None));
        draft.Version.Definition.Panel.ShouldNotBeNull().Result.Step.ShouldBe("GRADE");

        var onScreen = new JsonObject { [_sample.Employees] = 25, [_sample.Sectors] = new JsonArray("ICT", "BUILDING") };
        var live = await Requests(s => s.LiveResultAsync(draft.Request.Id, new LiveResultRequest(onScreen, "PREMIUM"), CancellationToken.None));
        live.RuleSetCode.ShouldBe(_ruleSet);
        live.Version.ShouldBe(1);
        live.Output["grade"]!.GetValue<double>().ShouldBe(1, "the step's input mapping read the answers on screen");
        live.Output["level"]!.GetValue<string>().ShouldBe("PREMIUM");
        live.Output["items"]!.AsArray().Select(i => i!["sector"]!.GetValue<string>()).ShouldBe(["ICT", "BUILDING"]);

        // Without answers it uses the saved ones (none yet): the lower grade.
        var saved = await Requests(s => s.LiveResultAsync(draft.Request.Id, new LiveResultRequest(null, null), CancellationToken.None));
        saved.Output["grade"]!.GetValue<double>().ShouldBe(3);
        (await EvaluationsAsync()).ShouldBe(0, "a live result is not an evaluation of the request");

        // Once submitted, the workflow decides (recorded) and the live result is no longer offered.
        await Requests(s => s.SaveAsync(draft.Request.Id, new SaveRequest(new JsonObject
        {
            [_sample.Sectors] = new JsonArray("ICT"),
            [_sample.IctCategory] = "GOLD",
            [_sample.Employees] = 25,
            [_sample.Revenue] = 20_000_000,
        }, "PREMIUM", null), CancellationToken.None));
        await Requests(s => s.SubmitAsync(draft.Request.Id, new SubmitRequest(true), CancellationToken.None));
        (await EvaluationsAsync()).ShouldBe(1);
        await Should.ThrowAsync<RequestConflictException>(() => Requests(s => s.LiveResultAsync(draft.Request.Id, new LiveResultRequest(onScreen, null), CancellationToken.None)));
    }

    [Fact]
    public async Task A_service_without_a_live_result_refuses_it_and_the_panel_is_checked_with_the_version()
    {
        await ConfigureAsync(Panel(result: false));
        var draft = await Requests(s => s.CreateAsync(new CreateRequest(_sample.Service), CancellationToken.None));
        await Should.ThrowAsync<RequestConflictException>(() => Requests(s => s.LiveResultAsync(draft.Request.Id, new LiveResultRequest(null, null), CancellationToken.None)));

        var bad = Panel();
        bad.Assistant.Lines.Add(new AssistantLineDefinition { Step = "NO_SUCH_STEP", Kind = AssistantLineKind.Question, TextAr = "ج", TextEn = "A" });
        bad.Summary.Questions.Add("NOT_ASKED");
        bad.Result.Step = null;
        bad.Result.Scale = [];
        var definition = _sample.Definition();
        definition.Panel = bad;
        var errors = (await Should.ThrowAsync<ChangeValidationException>(() => _applicant.Host.PublishAsync(ServiceChangeTypes.Area, _sample.Version(2, definition)))).Errors.Keys;
        errors.ShouldContain("definition.panel.assistant.lines[2].step");
        errors.ShouldContain("definition.panel.assistant.lines[2].questionAr", "a question line needs its question");
        errors.ShouldContain("definition.panel.summary.questions[1]");
        errors.ShouldContain("definition.panel.result.step");
        errors.ShouldContain("definition.panel.result.scale", "values on the scale need the scale");
    }
}
