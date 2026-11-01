using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.Services.Changes;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Runtime;
using DynamicCube.Modules.Workflow.Changes;
using DynamicCube.Modules.Workflow.Persistence;
using DynamicCube.Modules.Workflow.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Parties;
using DynamicCube.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Services.Tests;

/// <summary>
/// Sub-requests (3f): prerequisites a service asks for before it can be started, services bundled with a request when it
/// is submitted, and requests a workflow step makes — waiting for them or carrying on.
/// </summary>
public sealed class SubRequestTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly ServiceSamples _main = new();
    private readonly string _reviewed = TestHost.Unique("REVIEWED");
    private readonly string _instant = TestHost.Unique("INSTANT");
    private readonly string _gated = TestHost.Unique("GATED");
    private readonly string _role = TestHost.Unique("REVIEWER");
    private readonly string _employee = TestHost.Unique("reviewer").ToLowerInvariant();
    private Applicant _applicant = null!;

    public async ValueTask InitializeAsync()
    {
        _applicant = await Applicant.CreateAsync(fixture);
        var host = _applicant.Host;

        // The main service, and three small ones whose only question (the employees) the main form shares: one reviewed by
        // an employee, one completed without review, and one gated by a prerequisite (an approved request of the reviewed one).
        await host.PublishAsync(ServiceChangeTypes.Area, [.. _main.Questions(), _main.ServiceChange(), _main.Version(1)]);
        await host.PublishAsync(ServiceChangeTypes.Area, Small(_reviewed), Small(_instant), Small(_gated));
        await host.PublishAsync(ServiceChangeTypes.Area, SmallVersion(_reviewed), SmallVersion(_instant), SmallVersion(_gated, new PrerequisiteDefinition
        {
            Code = "HAS_REVIEWED",
            Kind = PrerequisiteKind.Request,
            Service = _reviewed,
            MessageAr = "يلزم طلب معتمد.",
            MessageEn = "An approved request is needed.",
        }));
        await host.PublishAsync(
            AppsControlAreas.Security,
            new Change(AppsControlChangeTypes.Role, _role, ChangeOperation.Create, new { nameAr = "مراجع", nameEn = "Reviewer" }),
            new Change(AppsControlChangeTypes.User, _employee, ChangeOperation.Create, new { nameAr = "مراجع", nameEn = "Reviewer", roles = new[] { new { roleCode = _role, companyCode = "*", branches = new[] { "*" } } } }));
        await host.PublishAsync(WorkflowChangeTypes.Area, Workflow(_reviewed, 2, Reviewed()));
    }

    public async ValueTask DisposeAsync() => await _applicant.DisposeAsync();

    [Fact]
    public async Task A_prerequisite_blocks_the_service_until_it_is_met_and_points_to_the_service_that_meets_it()
    {
        var gated = await Requests(s => s.GetServiceAsync(_gated, CancellationToken.None));
        gated.Service.IsEligible.ShouldBeFalse();
        var reason = gated.Service.Reasons.ShouldHaveSingleItem();
        reason.Code.ShouldBe("PREREQUISITE_HAS_REVIEWED");
        reason.MessageEn.ShouldBe("An approved request is needed.");
        reason.StartService.ShouldBe(_reviewed, "the applicant is offered the service that meets it");
        await Should.ThrowAsync<NotEligibleException>(() => Requests(s => s.CreateAsync(new CreateRequest(_gated), CancellationToken.None)));

        // A submitted request is not enough; an approved one is.
        var reviewed = await SubmitSmallAsync(_reviewed);
        (await Requests(s => s.GetServiceAsync(_gated, CancellationToken.None))).Service.IsEligible.ShouldBeFalse();
        await ApproveAsync(reviewed);
        (await Requests(s => s.GetServiceAsync(_gated, CancellationToken.None))).Service.IsEligible.ShouldBeTrue();

        // A prerequisite must name an existing service, not the service itself, and say why in both languages.
        var bad = SmallDefinition();
        bad.Prerequisites = [new() { Code = "X", Kind = PrerequisiteKind.Request, Service = TestHost.Unique("NO_SUCH"), MessageAr = "", MessageEn = "x" }, new() { Code = "Y", Kind = PrerequisiteKind.Request, Service = _gated, MessageAr = "ع", MessageEn = "e", Statuses = ["SOMEDAY"] }];
        var refused = await Should.ThrowAsync<ChangeValidationException>(() => _applicant.Host.PublishAsync(ServiceChangeTypes.Area, Version(_gated, 2, bad)));
        refused.Errors.Keys.ShouldBe(["definition.prerequisites[0].messageAr", "definition.prerequisites[0].service", "definition.prerequisites[1].statuses", "definition.prerequisites[1].service"], ignoreOrder: true);
    }

    [Fact]
    public async Task Bundled_services_are_requested_with_the_main_request_and_tracked_with_it()
    {
        var definition = _main.Definition();
        definition.Bundle = [new() { Service = _instant, Required = true }, new() { Service = _reviewed, NoteEn = "Also ask for a review" }];
        await _applicant.Host.PublishAsync(ServiceChangeTypes.Area, _main.Version(2, definition));

        // The draft shows what comes with it; only the ticked optional ones are requested.
        var draft = await DraftMainAsync();
        draft.Bundle.Select(b => (b.Service, b.Required, b.IsEligible)).ShouldBe([(_instant, true, true), (_reviewed, false, true)]);
        var submitted = await Requests(s => s.SubmitAsync(draft.Request.Id, new SubmitRequest(true), CancellationToken.None));
        var bundled = submitted.Links.ShouldHaveSingleItem();
        (bundled.Relation, bundled.ServiceCode, bundled.Status).ShouldBe(("BUNDLED", _instant, StatusCategory.Approved), "complete answers: submitted, and its workflow approved it");
        var child = await Requests(s => s.GetAsync(bundled.Id, CancellationToken.None));
        child.Data[_main.Employees]!.GetValue<int>().ShouldBe(25, "the answers both forms share are carried over");
        child.Links.ShouldHaveSingleItem().Relation.ShouldBe("PARENT");

        var withOptional = await DraftMainAsync();
        var both = await Requests(s => s.SubmitAsync(withOptional.Request.Id, new SubmitRequest(true, null, [_reviewed]), CancellationToken.None));
        both.Links.Select(l => (l.ServiceCode, l.Status)).ShouldBe([(_instant, StatusCategory.Approved), (_reviewed, StatusCategory.InProgress)], ignoreOrder: true);

        // A bundled service the applicant may not have stops the submission, saying which.
        definition.Bundle = [new() { Service = _gated, Required = true }];
        await _applicant.Host.PublishAsync(ServiceChangeTypes.Area, _main.Version(3, definition));
        var blocked = await DraftMainAsync();
        var refused = await Should.ThrowAsync<NotEligibleException>(() => Requests(s => s.SubmitAsync(blocked.Request.Id, new SubmitRequest(true), CancellationToken.None)));
        refused.Reasons.ShouldHaveSingleItem().Code.ShouldBe($"BUNDLE_{_gated}_PREREQUISITE_HAS_REVIEWED");
    }

    [Fact]
    public async Task A_step_makes_a_request_and_waits_for_it_or_carries_on()
    {
        // Waiting for a request an employee reviews: approved takes next, the parent waits meanwhile.
        await _applicant.Host.PublishAsync(WorkflowChangeTypes.Area, Workflow(_main.Service, 2, Spawning(_reviewed, wait: true)));
        var waiting = await SubmitMainAsync();
        var parent = await Requests(s => s.GetAsync(waiting, CancellationToken.None));
        parent.Request.Status.ShouldBe(StatusCategory.PendingExternal);
        parent.Request.DisplayStatus.ShouldBe("AWAITING_SUB_REQUEST");
        var spawned = parent.Links.ShouldHaveSingleItem();
        (spawned.Relation, spawned.ServiceCode, spawned.Status, spawned.Step).ShouldBe(("SPAWNED", _reviewed, StatusCategory.InProgress, "SPAWN"));
        await ApproveAsync(spawned.Id);
        (await Requests(s => s.GetAsync(waiting, CancellationToken.None))).Request.Status.ShouldBe(StatusCategory.Approved);

        // A rejected request takes the failed way.
        var rejecting = await SubmitMainAsync();
        var child = (await Requests(s => s.GetAsync(rejecting, CancellationToken.None))).Links.Single();
        await ActAsync(child.Id, "REJECT", "Not enough employees.");
        var rejected = await Requests(s => s.GetAsync(rejecting, CancellationToken.None));
        rejected.Request.Status.ShouldBe(StatusCategory.Rejected);

        // A request completed at once (no review) moves the waiting step on straight away.
        await _applicant.Host.PublishAsync(WorkflowChangeTypes.Area, Workflow(_main.Service, 3, Spawning(_instant, wait: true)));
        var instantId = await SubmitMainAsync();
        var instant = await Requests(s => s.GetAsync(instantId, CancellationToken.None));
        instant.Request.Status.ShouldBe(StatusCategory.Approved);
        instant.Links.ShouldHaveSingleItem().Status.ShouldBe(StatusCategory.Approved);

        // Carrying on: the parent finishes while the request is still reviewed.
        await _applicant.Host.PublishAsync(WorkflowChangeTypes.Area, Workflow(_main.Service, 4, Spawning(_reviewed, wait: false)));
        var carriedId = await SubmitMainAsync();
        var carried = await Requests(s => s.GetAsync(carriedId, CancellationToken.None));
        carried.Request.Status.ShouldBe(StatusCategory.Approved);
        carried.Links.ShouldHaveSingleItem().Status.ShouldBe(StatusCategory.InProgress);

        // A step needs a service that exists, and a waiting one a way for a request that is not approved.
        var refused = await Should.ThrowAsync<ChangeValidationException>(() => _applicant.Host.PublishAsync(WorkflowChangeTypes.Area,
            Workflow(_main.Service, 5, Spawning(TestHost.Unique("NO_SUCH"), wait: true, failed: null))));
        refused.Errors.Keys.ShouldBe(["definition.nodes[1].failed", "definition.nodes[1].childService"], ignoreOrder: true);
    }

    private static Change Small(string code) =>
        new(ServiceChangeTypes.Service, code, ChangeOperation.Create, new ServicePayload
        {
            NameAr = "خدمة " + code,
            NameEn = "Service " + code,
            Archetype = "ATTESTATION",
            PartyKinds = [PartyKind.Establishment],
            NumberPrefix = "SUB",
        });

    private VersionDefinition SmallDefinition() => new()
    {
        Steps =
        [
            new()
            {
                Code = "DATA",
                Type = StepType.Form,
                TitleAr = "البيانات",
                TitleEn = "Data",
                Sections = [new() { Code = "MAIN", TitleAr = "البيانات", TitleEn = "Data", Questions = [new() { Code = _main.Employees, Required = true }] }],
            },
        ],
    };

    private Change SmallVersion(string code, PrerequisiteDefinition? prerequisite = null)
    {
        var definition = SmallDefinition();
        if (prerequisite is not null)
        {
            definition.Prerequisites = [prerequisite];
        }

        return Version(code, 1, definition);
    }

    private static Change Version(string code, int number, VersionDefinition definition) =>
        new(ServiceChangeTypes.Version, $"{code}/{number}", ChangeOperation.Create, new ServiceVersionPayload { NameAr = $"الإصدار {number}", NameEn = $"Version {number}", Definition = definition });

    private static Change Workflow(string service, int number, object[] nodes) =>
        new(WorkflowChangeTypes.Version, $"{service}/{number}", ChangeOperation.Create, new { nameAr = number.ToString(System.Globalization.CultureInfo.InvariantCulture), nameEn = "v", definition = new { nodes } });

    private object[] Reviewed() =>
    [
        new { code = "START", type = "START", nameAr = "بداية", nameEn = "Start", next = "REVIEW" },
        new
        {
            code = "REVIEW",
            type = "HUMAN_TASK",
            nameAr = "المراجعة",
            nameEn = "Review",
            assignment = new { target = "ROLE", role = _role, mode = "QUEUE" },
            actions = new object[]
            {
                new { code = "APPROVE", kind = "APPROVE", nameAr = "اعتماد", nameEn = "Approve", to = "APPROVED" },
                new { code = "REJECT", kind = "REJECT", nameAr = "رفض", nameEn = "Reject", to = "REJECTED" },
            },
        },
        new { code = "APPROVED", type = "END", nameAr = "معتمد", nameEn = "Approved", outcome = "APPROVED" },
        new { code = "REJECTED", type = "END", nameAr = "مرفوض", nameEn = "Rejected", outcome = "REJECTED" },
    ];

    private static object[] Spawning(string child, bool wait, string? failed = "REJECTED") =>
    [
        new { code = "START", type = "START", nameAr = "بداية", nameEn = "Start", next = "SPAWN" },
        new { code = "SPAWN", type = "SUB_REQUEST", nameAr = "طلب فرعي", nameEn = "Sub-request", childService = child, wait, submitChild = true, next = "APPROVED", failed },
        new { code = "APPROVED", type = "END", nameAr = "معتمد", nameEn = "Approved", outcome = "APPROVED" },
        new { code = "REJECTED", type = "END", nameAr = "مرفوض", nameEn = "Rejected", outcome = "REJECTED" },
    ];

    private async Task<RequestView> DraftMainAsync()
    {
        var draft = await Requests(s => s.CreateAsync(new CreateRequest(_main.Service), CancellationToken.None));
        var answers = new JsonObject
        {
            [_main.Sectors] = new JsonArray("ICT"),
            [_main.IctCategory] = "GOLD",
            [_main.Employees] = 25,
            [_main.Revenue] = 20_000_000,
        };
        return await Requests(s => s.SaveAsync(draft.Request.Id, new SaveRequest(answers, "PREMIUM", null), CancellationToken.None));
    }

    private async Task<Guid> SubmitMainAsync()
    {
        var draft = await DraftMainAsync();
        await Requests(s => s.SubmitAsync(draft.Request.Id, new SubmitRequest(true), CancellationToken.None));
        return draft.Request.Id;
    }

    private async Task<Guid> SubmitSmallAsync(string service)
    {
        var draft = await Requests(s => s.CreateAsync(new CreateRequest(service), CancellationToken.None));
        await Requests(s => s.SaveAsync(draft.Request.Id, new SaveRequest(new JsonObject { [_main.Employees] = 5 }, null, null), CancellationToken.None));
        await Requests(s => s.SubmitAsync(draft.Request.Id, new SubmitRequest(true), CancellationToken.None));
        return draft.Request.Id;
    }

    private Task ApproveAsync(Guid request) => ActAsync(request, "APPROVE", null);

    private async Task ActAsync(Guid request, string action, string? comment)
    {
        var task = await _applicant.Host.AsAsync("admin", sp => sp.GetRequiredService<WorkflowDbContext>().Tasks
            .Where(t => t.SubjectId == request && t.Status == Modules.Workflow.Domain.WorkTaskStatus.Open).Select(t => t.Id).SingleAsync());
        await _applicant.Host.AsAsync(_employee, sp => sp.GetRequiredService<ActionCenter>().ClaimAsync(task, CancellationToken.None));
        await _applicant.Host.AsAsync(_employee, sp => sp.GetRequiredService<ActionCenter>().ActAsync(task, new ActRequest(action, comment), CancellationToken.None));
    }

    private Task<T> Requests<T>(Func<RequestService, Task<T>> action) =>
        _applicant.AsAsync(sp => action(sp.GetRequiredService<RequestService>()));
}
