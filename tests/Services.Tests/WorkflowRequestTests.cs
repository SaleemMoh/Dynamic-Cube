using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.Services.Changes;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Runtime;
using DynamicCube.Modules.Workflow.Changes;
using DynamicCube.Modules.Workflow.Persistence;
using DynamicCube.Modules.Workflow.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Services.Tests;

/// <summary>Submission starts the version's workflow; employee actions set the request's status; returns are corrected and resubmitted.</summary>
public sealed class WorkflowRequestTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly ServiceSamples _sample = new();
    private readonly string _role = TestHost.Unique("REVIEWER");
    private readonly string _employee = TestHost.Unique("reviewer").ToLowerInvariant();
    private Applicant _applicant = null!;

    public async ValueTask InitializeAsync()
    {
        _applicant = await Applicant.CreateAsync(fixture);
        var host = _applicant.Host;

        // The service comes with its workflow (no review); version 2 of it sends requests to a reviewer.
        await host.PublishAsync(ServiceChangeTypes.Area, [.. _sample.Questions(), _sample.ServiceChange(), _sample.Version(1)]);
        await host.PublishAsync(
            AppsControlAreas.Security,
            new Change(AppsControlChangeTypes.Role, _role, ChangeOperation.Create, new { nameAr = "مراجع", nameEn = "Reviewer" }),
            new Change(AppsControlChangeTypes.User, _employee, ChangeOperation.Create, new { nameAr = "مراجع", nameEn = "Reviewer", roles = new[] { new { roleCode = _role } } }));
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
                        new { code = "START", type = "START", nameAr = "بداية", nameEn = "Start", next = "REVIEW" },
                        new
                        {
                            code = "REVIEW",
                            type = "HUMAN_TASK",
                            nameAr = "المراجعة",
                            nameEn = "Review",
                            displayStatus = "UNDER_REVIEW",
                            assignment = new { target = "ROLE", role = _role, mode = "QUEUE" },
                            actions = new object[]
                            {
                                new { code = "APPROVE", kind = "APPROVE", nameAr = "اعتماد", nameEn = "Approve", to = "APPROVED" },
                                new { code = "RETURN", kind = "RETURN", nameAr = "إعادة للتعديل", nameEn = "Return for correction" },
                            },
                        },
                        new { code = "APPROVED", type = "END", nameAr = "معتمد", nameEn = "Approved", outcome = "APPROVED" },
                    },
                },
            }));
    }

    public async ValueTask DisposeAsync() => await _applicant.DisposeAsync();

    [Fact]
    public async Task Submission_starts_the_workflow_and_a_return_comes_back_to_the_same_reviewer()
    {
        var id = await SubmitAsync();
        var request = await Requests(s => s.GetAsync(id, CancellationToken.None));
        request.Request.Status.ShouldBe(StatusCategory.InProgress);
        request.Request.DisplayStatus.ShouldBe("UNDER_REVIEW");

        var task = await OpenTaskAsync(id);
        await AsReviewer(c => c.ClaimAsync(task, CancellationToken.None));
        await AsReviewer(c => c.ActAsync(task, new ActRequest("RETURN", "Correct the number of employees."), CancellationToken.None));

        var returned = await Requests(s => s.GetAsync(id, CancellationToken.None));
        returned.Request.Status.ShouldBe(StatusCategory.PendingCustomer);
        returned.Request.DisplayStatus.ShouldBe("RETURNED");
        returned.Timeline[^1].Comment.ShouldBe("Correct the number of employees.");
        returned.Timeline[^1].UserId.ShouldBeEmpty("the applicant does not see which employee returned the request");

        // The applicant corrects an answer (the level stays) and sends the request back.
        var answers = returned.Data.DeepClone().AsObject();
        answers[_sample.Employees] = 30;
        await Should.ThrowAsync<RequestConflictException>(() => Requests(s => s.SaveAsync(id, new SaveRequest(answers, "REGULAR", null), CancellationToken.None)));
        await Requests(s => s.SaveAsync(id, new SaveRequest(answers, null, null), CancellationToken.None));
        var resubmitted = await Requests(s => s.SubmitAsync(id, new SubmitRequest(true), CancellationToken.None));

        resubmitted.Request.Status.ShouldBe(StatusCategory.InProgress);
        resubmitted.Data[_sample.Employees]!.GetValue<int>().ShouldBe(30);
        var again = await OpenTaskAsync(id);
        again.ShouldNotBe(task);
        var detail = await AsReviewer(c => c.GetAsync(again, CancellationToken.None));
        detail.Task.Assignee.ShouldBe(_employee);

        await AsReviewer(c => c.ActAsync(again, new ActRequest("APPROVE", null), CancellationToken.None));
        var approved = await Requests(s => s.GetAsync(id, CancellationToken.None));
        approved.Request.Status.ShouldBe(StatusCategory.Approved);
        approved.Timeline.Select(t => t.Type).ShouldBe(["CREATED", "SUBMITTED", "STATUS_CHANGED", "RETURNED", "RESUBMITTED", "STATUS_CHANGED", "APPROVED"]);

        // Each step the applicant hears about raised its notification event, with the reason and the beneficiary.
        var raised = await _applicant.Host.RaisedAsync(id);
        raised.Select(e => e.Code).Where(c => c.StartsWith("REQUEST_", StringComparison.Ordinal))
            .ShouldBe(["REQUEST_SUBMITTED", "REQUEST_RETURNED", "REQUEST_RESUBMITTED", "REQUEST_APPROVED"]);
        var returnedEvent = raised.Single(e => e.Code == "REQUEST_RETURNED");
        returnedEvent.Facts["request"]!["comment"]!.GetValue<string>().ShouldBe("Correct the number of employees.");
        returnedEvent.Facts["request"]!["status"]!.GetValue<string>().ShouldBe("PENDING_CUSTOMER");
        returnedEvent.PartyId.ShouldNotBeNull();
        returnedEvent.Facts["request"]!["serviceNameEn"]!.GetValue<string>().ShouldBe(approved.Request.ServiceNameEn);
        returnedEvent.SubjectNumber.ShouldBe(approved.Request.Number);
        raised.Select(e => e.Code).ShouldContain("TASK_CREATED");
    }

    [Fact]
    public async Task Without_a_published_workflow_version_a_service_takes_no_requests_and_no_new_versions()
    {
        var host = _applicant.Host;
        await host.PublishAsync(
            WorkflowChangeTypes.Area,
            new Change(WorkflowChangeTypes.Version, _sample.Service + "/1", ChangeOperation.Update, new { nameAr = "بدون مراجعة", nameEn = "No review", isRetired = true, definition = DefaultDefinition() }),
            new Change(WorkflowChangeTypes.Version, _sample.Service + "/2", ChangeOperation.Update, new { nameAr = "2", nameEn = "2", isRetired = true, definition = await DefinitionAsync(2) }));

        await Should.ThrowAsync<RequestConflictException>(SubmitAsync);
        var refused = await Should.ThrowAsync<ChangeValidationException>(() => host.PublishAsync(ServiceChangeTypes.Area, _sample.Version(2)));
        refused.Errors["workflow"].ShouldContain(m => m.Contains("no published version"));
    }

    [Fact]
    public async Task A_version_that_names_a_workflow_adds_it_to_the_service_workflow()
    {
        var host = _applicant.Host;
        // Another service's workflow, with a definition of its own (a package from before services owned their workflow
        // named such workflows in its versions).
        var otherService = new ServiceSamples();
        await host.PublishAsync(ServiceChangeTypes.Area, [.. otherService.Questions(), otherService.ServiceChange(), otherService.Version(1)]);
        var other = otherService.Service;
        await host.PublishAsync(
            WorkflowChangeTypes.Area,
            new Change(WorkflowChangeTypes.Version, other + "/2", ChangeOperation.Create, new
            {
                nameAr = "قديم",
                nameEn = "Legacy",
                definition = new
                {
                    nodes = new object[]
                    {
                        new { code = "START", type = "START", nameAr = "بداية", nameEn = "Start", next = "DONE" },
                        new { code = "DONE", type = "END", nameAr = "انتهى", nameEn = "Done", outcome = "APPROVED" },
                    },
                },
            }));

        var definition = _sample.Definition();
        definition.Workflow = other;
        await host.PublishAsync(ServiceChangeTypes.Area, _sample.Version(2, definition));

        var versions = await host.AsAsync("admin", sp => sp.GetRequiredService<WorkflowDbContext>().Versions
            .Where(v => v.Workflow!.ServiceCode == _sample.Service).OrderBy(v => v.Number).ToListAsync());
        var added = versions[^1];
        added.Number.ShouldBe(3, "versions 1 and 2 were there; the named workflow's definition is the next one");
        added.NameEn.ShouldBe("Legacy");
        added.PublishedAt.ShouldNotBeNull();
        JsonNode.Parse(added.Definition)!["nodes"]![1]!["code"]!.GetValue<string>().ShouldBe("DONE");
        var stored = await host.AsAsync("admin", sp => sp.GetRequiredService<Persistence.ServicesDbContext>().Versions.SingleAsync(v => v.Code == _sample.Service + "/2"));
        JsonNode.Parse(stored.Definition)!["workflow"].ShouldBeNull("the version keeps no workflow name");

        // Naming it again changes nothing: the service already runs that definition.
        var again = _sample.Definition();
        again.Workflow = other;
        await host.PublishAsync(ServiceChangeTypes.Area, _sample.Version(3, again));
        (await host.AsAsync("admin", sp => sp.GetRequiredService<WorkflowDbContext>().Versions.CountAsync(v => v.Workflow!.ServiceCode == _sample.Service))).ShouldBe(3);
    }

    [Fact]
    public async Task A_named_workflow_that_does_not_exist_stops_the_publish()
    {
        var definition = _sample.Definition();
        definition.Workflow = TestHost.Unique("MISSING_WF");
        var refused = await Should.ThrowAsync<ChangeSetException>(() => _applicant.Host.PublishAsync(ServiceChangeTypes.Area, _sample.Version(2, definition)));
        refused.Message.ShouldContain("does not exist");
    }

    private static object DefaultDefinition() => new
    {
        nodes = new object[]
        {
            new { code = "START", type = "START", nameAr = "البداية", nameEn = "Start", next = "COMPLETED" },
            new { code = "COMPLETED", type = "END", nameAr = "اكتمال الطلب", nameEn = "Request completed", outcome = "APPROVED" },
        },
    };

    private Task<JsonNode> DefinitionAsync(int number) =>
        _applicant.Host.AsAsync("admin", async sp => JsonNode.Parse((await sp.GetRequiredService<WorkflowDbContext>().Versions.SingleAsync(v => v.Code == $"{_sample.Service}/{number}")).Definition)!);

    private async Task<Guid> SubmitAsync()
    {
        var draft = await Requests(s => s.CreateAsync(new CreateRequest(_sample.Service), CancellationToken.None));
        var answers = new JsonObject
        {
            [_sample.Sectors] = new JsonArray("ICT"),
            [_sample.IctCategory] = "GOLD",
            [_sample.Employees] = 25,
            [_sample.Revenue] = 20_000_000,
        };
        await Requests(s => s.SaveAsync(draft.Request.Id, new SaveRequest(answers, "PREMIUM", null), CancellationToken.None));
        await Requests(s => s.SubmitAsync(draft.Request.Id, new SubmitRequest(true), CancellationToken.None));
        return draft.Request.Id;
    }

    private Task<T> Requests<T>(Func<RequestService, Task<T>> action) =>
        _applicant.AsAsync(sp => action(sp.GetRequiredService<RequestService>()));

    private Task<T> AsReviewer<T>(Func<ActionCenter, Task<T>> action) =>
        _applicant.Host.AsAsync(_employee, sp => action(sp.GetRequiredService<ActionCenter>()));

    private Task AsReviewer(Func<ActionCenter, Task> action) =>
        _applicant.Host.AsAsync(_employee, sp => action(sp.GetRequiredService<ActionCenter>()));

    private Task<Guid> OpenTaskAsync(Guid request) =>
        _applicant.Host.AsAsync("admin", sp => sp.GetRequiredService<WorkflowDbContext>().Tasks
            .Where(t => t.SubjectId == request && t.Status == Workflow.Domain.WorkTaskStatus.Open).Select(t => t.Id).SingleAsync());
}
