using System.Text.Json.Nodes;
using DynamicCube.Modules.Services.Changes;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Integration;
using DynamicCube.Platform.Parties;
using DynamicCube.Platform.Workflow;
using DynamicCube.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Services.Tests;

/// <summary>Stands in for the Integration Manager: answers by operation, and records the calls.</summary>
internal sealed class FakeGateway : IIntegrationGateway
{
    public List<(string Operation, IReadOnlyDictionary<string, string?> Inputs)> Calls { get; } = [];

    public bool VerifyFails { get; set; }

    public decimal Capital { get; set; } = 500000;

    public string Status { get; set; } = "ACTIVE";

    public Task<IntegrationResult> InvokeAsync(string operationCode, IReadOnlyDictionary<string, string?> inputs, CancellationToken cancellationToken)
    {
        Calls.Add((operationCode, inputs));
        return Task.FromResult(operationCode switch
        {
            "CR_INFO" => Result(IntegrationOutcome.Success, new Dictionary<string, JsonNode?> { ["capital"] = Capital }, new JsonObject { ["name"] = "مؤسسة من السجل" }),
            "VERIFY" when VerifyFails => Result(IntegrationOutcome.BusinessError, new Dictionary<string, JsonNode?>(), null, "السجل غير نشط", "The registration is not active"),
            "VERIFY" => Result(IntegrationOutcome.Success, new Dictionary<string, JsonNode?> { ["status"] = Status }, new JsonObject { ["expiry"] = "2030-01-01" }),
            _ => throw new IntegrationOperationNotFoundException(operationCode),
        });
    }

    private static IntegrationResult Result(IntegrationOutcome outcome, Dictionary<string, JsonNode?> outputs, JsonNode? data, string? ar = null, string? en = null) =>
        new(outcome, outcome == IntegrationOutcome.Success ? "000" : "101", ar, en, outputs, data, Guid.NewGuid(), false);
}

/// <summary>
/// Questions answered by an integration (locked or editable), integration steps of the wizard, questions that belong to
/// one service, and service archetypes with their default workflow.
/// </summary>
public sealed class IntegrationQuestionTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly FakeGateway _gateway = new();
    private readonly string _service = TestHost.Unique("INTEG");
    private readonly string _capital = TestHost.Unique("CAPITAL");
    private readonly string _name = TestHost.Unique("REG_NAME");
    private readonly string _status = TestHost.Unique("REG_STATUS");
    private readonly string _own = TestHost.Unique("OWN_Q");
    private Applicant _applicant = null!;

    public async ValueTask InitializeAsync()
    {
        _applicant = await Applicant.CreateAsync(fixture, configure: s => s.AddSingleton<IIntegrationGateway>(_gateway));
        var crInput = new IntegrationInput { Parameter = "crNumber", From = "party.identifiers.CR_NUMBER" };
        var definition = new VersionDefinition
        {
            Steps =
            [
                new()
                {
                    Code = "VERIFY",
                    Type = StepType.Integration,
                    TitleAr = "التحقق من السجل",
                    TitleEn = "Verify the registration",
                    Integration = new IntegrationStepDefinition
                    {
                        Operation = "VERIFY",
                        Inputs = [crInput],
                        Outputs = [new() { Output = "status", Question = _status }],
                        Display = [new() { Output = "status", LabelAr = "الحالة", LabelEn = "Status" }, new() { Output = "expiry", LabelAr = "الانتهاء", LabelEn = "Expiry" }],
                    },
                },
                new()
                {
                    Code = "DETAILS",
                    Type = StepType.Form,
                    TitleAr = "البيانات",
                    TitleEn = "Details",
                    Sections = [new() { Code = "MAIN", TitleAr = "البيانات", TitleEn = "Details", Questions = [new() { Code = _capital }, new() { Code = _name }, new() { Code = _status }, new() { Code = _own }] }],
                },
            ],
        };
        await _applicant.Host.PublishAsync(
            ServiceChangeTypes.Area,
            new Change(ServiceChangeTypes.Service, _service, ChangeOperation.Create, new ServicePayload
            {
                NameAr = "خدمة بتكامل",
                NameEn = "Service with integrations",
                Archetype = "ISSUANCE",
                PartyKinds = [PartyKind.Establishment],
                NumberPrefix = "INT",
            }),
            new Change(ServiceChangeTypes.Question, _capital, ChangeOperation.Create, new QuestionPayload
            {
                NameAr = "رأس المال",
                NameEn = "Capital",
                Type = QuestionType.Number,
                Fill = QuestionFill.Integration,
                Integration = new IntegrationFill { Operation = "CR_INFO", Inputs = [crInput], Output = "capital" },
                AllowManualEntry = false,
            }),
            new Change(ServiceChangeTypes.Question, _name, ChangeOperation.Create, new QuestionPayload
            {
                NameAr = "الاسم في السجل",
                NameEn = "Registered name",
                Type = QuestionType.Text,
                Fill = QuestionFill.Integration,
                Integration = new IntegrationFill { Operation = "CR_INFO", Inputs = [crInput], Output = "name" },
            }),
            new Change(ServiceChangeTypes.Question, _status, ChangeOperation.Create, new QuestionPayload { NameAr = "حالة السجل", NameEn = "Status", Type = QuestionType.Text }),
            new Change(ServiceChangeTypes.Question, _own, ChangeOperation.Create, new QuestionPayload { NameAr = "سؤال الخدمة", NameEn = "Service question", Type = QuestionType.Text, ServiceCode = _service }),
            new Change(ServiceChangeTypes.Version, $"{_service}/1", ChangeOperation.Create, new ServiceVersionPayload { NameAr = "الإصدار 1", NameEn = "Version 1", Definition = definition }));
    }

    public async ValueTask DisposeAsync() => await _applicant.DisposeAsync();

    private Task<T> Requests<T>(Func<RequestService, Task<T>> action) => _applicant.AsAsync(sp => action(sp.GetRequiredService<RequestService>()));

    [Fact]
    public async Task Answers_come_from_integrations_locked_ones_cannot_change_and_a_required_integration_step_must_succeed()
    {
        var draft = await Requests(s => s.CreateAsync(new CreateRequest(_service), CancellationToken.None));
        var id = draft.Request.Id;
        draft.Data[_capital]!.GetValue<decimal>().ShouldBe(500000m, "filled from the commercial register");
        draft.Data[_name]!.GetValue<string>().ShouldBe("مؤسسة من السجل", "a path in the provider's result");
        _gateway.Calls.ShouldContain(c => c.Operation == "CR_INFO" && c.Inputs["crNumber"] == _applicant.CrNumber);
        draft.Version.Questions[_capital].Locked.ShouldBeTrue();
        draft.Version.Questions[_name].Locked.ShouldBeFalse();

        // The applicant may change the editable answer but not the locked one.
        var tampered = new JsonObject { [_capital] = 1, [_name] = "اسم آخر" };
        _gateway.VerifyFails = true;
        var failed = await Requests(s => s.SaveAsync(id, new SaveRequest(tampered, null, "DETAILS", "VERIFY"), CancellationToken.None));
        failed.Data[_capital]!.GetValue<decimal>().ShouldBe(500000m);
        failed.Data[_name]!.GetValue<string>().ShouldBe("اسم آخر");
        failed.StepErrors.ShouldHaveSingleItem().Code.ShouldBe("INTEGRATION");
        failed.StepErrors[0].MessageEn.ShouldBe("The registration is not active");
        failed.CurrentStep.ShouldBe("VERIFY", "a failed required step keeps the applicant on it");
        (await Should.ThrowAsync<RequestValidationException>(() => Requests(s => s.SubmitAsync(id, new SubmitRequest(true), CancellationToken.None))))
            .Errors.ShouldContain(e => e.Code == "INTEGRATION" && e.Step == "VERIFY");

        _gateway.VerifyFails = false;
        var passed = await Requests(s => s.SaveAsync(id, new SaveRequest(failed.Data, null, "DETAILS", "VERIFY"), CancellationToken.None));
        passed.StepErrors.ShouldBeEmpty();
        passed.Data[_status]!.GetValue<string>().ShouldBe("ACTIVE", "the step's output became an answer");
        var submitted = await Requests(s => s.SubmitAsync(id, new SubmitRequest(true), CancellationToken.None));
        submitted.Request.Status.ShouldNotBe(StatusCategory.Draft);
    }

    [Fact]
    public async Task An_integration_step_runs_on_arrival_shows_the_chosen_values_and_a_reopened_draft_reads_its_integrations_again()
    {
        var draft = await Requests(s => s.CreateAsync(new CreateRequest(_service), CancellationToken.None));
        var id = draft.Request.Id;

        // Arriving on the step (here: back from the details) calls it, so the result card shows the outcome at once.
        var arrived = await Requests(s => s.SaveAsync(id, new SaveRequest(draft.Data, null, "VERIFY", null), CancellationToken.None));
        var run = arrived.Integrations["VERIFY"];
        run.Success.ShouldBeTrue();
        run.Values.ShouldNotBeNull();
        run.Values["status"]!.GetValue<string>().ShouldBe("ACTIVE");
        run.Values["expiry"]!.GetValue<string>().ShouldBe("2030-01-01", "a path in the provider's result");
        arrived.Data[_status]!.GetValue<string>().ShouldBe("ACTIVE");
        var calls = _gateway.Calls.Count(c => c.Operation == "VERIFY");

        // Moving on after a success does not call it again; "try again" (leaving and arriving on the same step) does.
        await Requests(s => s.SaveAsync(id, new SaveRequest(arrived.Data, null, "DETAILS", "VERIFY"), CancellationToken.None));
        _gateway.Calls.Count(c => c.Operation == "VERIFY").ShouldBe(calls);
        var retried = await Requests(s => s.SaveAsync(id, new SaveRequest(arrived.Data, null, "VERIFY", "VERIFY"), CancellationToken.None));
        _gateway.Calls.Count(c => c.Operation == "VERIFY").ShouldBe(calls + 1);
        retried.CurrentStep.ShouldBe("VERIFY");
        retried.Integrations["VERIFY"].At.ShouldBeGreaterThanOrEqualTo(run.At);

        // An autosave keeps the answers and the step, and calls nothing.
        var autosaved = await Requests(s => s.SaveAsync(id, new SaveRequest(new JsonObject(retried.Data.Select(p => KeyValuePair.Create(p.Key, p.Value?.DeepClone()))) { [_name] = "اسم مكتوب" }, null, null, null), CancellationToken.None));
        autosaved.CurrentStep.ShouldBe("VERIFY");
        autosaved.Data[_name]!.GetValue<string>().ShouldBe("اسم مكتوب");
        _gateway.Calls.Count(c => c.Operation == "VERIFY").ShouldBe(calls + 1);

        // Opened again after the providers' data changed: the locked answer and the step's output are read again, and
        // the applicant is told what changed.
        _gateway.Capital = 750000;
        _gateway.Status = "SUSPENDED";
        var reopened = await Requests(s => s.GetAsync(id, CancellationToken.None));
        reopened.Data[_capital]!.GetValue<decimal>().ShouldBe(750000m);
        reopened.Data[_status]!.GetValue<string>().ShouldBe("SUSPENDED");
        reopened.Data[_name]!.GetValue<string>().ShouldBe("اسم مكتوب", "an answer the applicant may change is not overwritten");
        reopened.Changes.Select(c => c.Question).ShouldBe([_capital, _status], ignoreOrder: true);
        var capital = reopened.Changes.Single(c => c.Question == _capital);
        capital.From!.GetValue<decimal>().ShouldBe(500000m);
        capital.To!.GetValue<decimal>().ShouldBe(750000m);
        capital.LabelEn.ShouldBe("Capital");
        reopened.Integrations["VERIFY"].Values!["status"]!.GetValue<string>().ShouldBe("SUSPENDED");

        // Nothing changed since: no notice.
        (await Requests(s => s.GetAsync(id, CancellationToken.None))).Changes.ShouldBeEmpty();
    }

    [Fact]
    public async Task Values_shown_on_a_result_card_need_a_valid_output_and_labels()
    {
        var bad = new VersionDefinition
        {
            Steps =
            [
                new()
                {
                    Code = "CHECK",
                    Type = StepType.Integration,
                    TitleAr = "تحقق",
                    TitleEn = "Check",
                    Integration = new IntegrationStepDefinition { Operation = "VERIFY", Display = [new() { Output = "not a path!", LabelAr = "", LabelEn = "" }] },
                },
            ],
        };
        var error = await Should.ThrowAsync<ChangeValidationException>(() => _applicant.Host.PublishAsync(
            ServiceChangeTypes.Area,
            new Change(ServiceChangeTypes.Version, $"{_service}/2", ChangeOperation.Create, new ServiceVersionPayload { NameAr = "2", NameEn = "2", Definition = bad })));
        error.Errors.Keys.ShouldContain(k => k.EndsWith("display[0].output", StringComparison.Ordinal));
        error.Errors.Keys.ShouldContain(k => k.EndsWith("display[0].label", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_question_of_one_service_is_not_used_by_another_and_fills_are_checked()
    {
        var other = TestHost.Unique("OTHER");
        var version = new VersionDefinition
        {
            Steps = [new() { Code = "S", Type = StepType.Form, TitleAr = "س", TitleEn = "S", Sections = [new() { Code = "M", TitleAr = "م", TitleEn = "M", Questions = [new() { Code = _own }] }] }],
        };
        var error = await Should.ThrowAsync<ChangeValidationException>(() => _applicant.Host.PublishAsync(
            ServiceChangeTypes.Area,
            new Change(ServiceChangeTypes.Service, other, ChangeOperation.Create, new ServicePayload { NameAr = "أخرى", NameEn = "Other", Archetype = "ISSUANCE", PartyKinds = [PartyKind.Establishment] }),
            new Change(ServiceChangeTypes.Version, $"{other}/1", ChangeOperation.Create, new ServiceVersionPayload { NameAr = "1", NameEn = "1", Definition = version })));
        error.Errors.Keys.ShouldContain(k => k.Contains("questions[0].code") || k == "definition.steps");

        var bad = await Should.ThrowAsync<ChangeValidationException>(() => _applicant.Host.PublishAsync(
            ServiceChangeTypes.Area,
            new Change(ServiceChangeTypes.Question, TestHost.Unique("BAD"), ChangeOperation.Create, new QuestionPayload
            {
                NameAr = "س",
                NameEn = "Q",
                Type = QuestionType.Text,
                Fill = QuestionFill.Integration,
                Integration = new IntegrationFill { Operation = "", Inputs = [new() { Parameter = "x", From = "not a path!" }], Output = "" },
            })));
        bad.Errors.Keys.ShouldBe(["integration.operation", "integration.output", "integration.inputs[0].from"], ignoreOrder: true);
        (await Should.ThrowAsync<ChangeValidationException>(() => _applicant.Host.PublishAsync(
            ServiceChangeTypes.Area,
            new Change(ServiceChangeTypes.Question, TestHost.Unique("NOPATH"), ChangeOperation.Create, new QuestionPayload { NameAr = "س", NameEn = "Q", Type = QuestionType.Text, Fill = QuestionFill.Party }))))
            .Errors.Keys.ShouldContain("prefill");
    }

    [Fact]
    public async Task A_new_service_takes_its_archetype_workflow_and_an_archetype_copies_a_service_and_a_workflow()
    {
        var workflow = await _applicant.Host.AsAsync("admin", sp => sp.GetRequiredService<IServiceWorkflows>().WorkflowOfAsync(_service, CancellationToken.None));
        var definition = await _applicant.Host.AsAsync("admin", sp => sp.GetRequiredService<IServiceWorkflows>().LatestDefinitionAsync(workflow!, CancellationToken.None));
        definition.ShouldNotBeNull().ShouldContain("SERVICE_REVIEWER", Case.Sensitive, "the issuance archetype reviews requests");

        var code = TestHost.Unique("ARCH");
        (await Should.ThrowAsync<ChangeValidationException>(() => _applicant.Host.PublishAsync(ServiceChangeTypes.Area,
            new Change(ServiceChangeTypes.Archetype, code, ChangeOperation.Create, new ArchetypePayload { NameAr = "نمط", NameEn = "Archetype", TemplateFrom = _service }))))
            .Errors.Keys.ShouldContain("template", "the service uses a question of its own, which a template cannot");
        var template = new VersionDefinition { Steps = [new() { Code = "DETAILS", Type = StepType.Form, TitleAr = "البيانات", TitleEn = "Details", Sections = [new() { Code = "MAIN", TitleAr = "أ", TitleEn = "A", Questions = [new() { Code = _status }] }] }, new() { Code = "REVIEW", Type = StepType.Review, TitleAr = "مراجعة", TitleEn = "Review" }] };
        await _applicant.Host.PublishAsync(ServiceChangeTypes.Area, new Change(ServiceChangeTypes.Archetype, code, ChangeOperation.Create, new ArchetypePayload
        {
            NameAr = "نمط",
            NameEn = "Archetype",
            Template = template,
            WorkflowFrom = workflow,
        }));
        var row = await _applicant.Host.AsAsync("admin", sp => sp.GetServices<DynamicCube.Platform.Configuration.IConfigApplier>()
            .Single(a => a.EntityType == ServiceChangeTypes.Archetype).GetAsync(code, CancellationToken.None));
        var payload = System.Text.Json.JsonSerializer.SerializeToElement(row);
        payload.GetProperty("workflow").GetProperty("nodes").GetArrayLength().ShouldBeGreaterThan(1);
        payload.GetProperty("template").GetProperty("steps").GetArrayLength().ShouldBe(2);
        (await Should.ThrowAsync<ChangeValidationException>(() => _applicant.Host.PublishAsync(ServiceChangeTypes.Area,
            new Change(ServiceChangeTypes.Service, TestHost.Unique("NOARCH"), ChangeOperation.Create, new ServicePayload { NameAr = "س", NameEn = "S", Archetype = "NOPE", PartyKinds = [PartyKind.Establishment] }))))
            .Errors.Keys.ShouldContain("archetype");
    }
}
