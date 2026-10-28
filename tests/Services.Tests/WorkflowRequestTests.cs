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
    private readonly string _workflow = TestHost.Unique("REVIEW_WF");
    private readonly string _role = TestHost.Unique("REVIEWER");
    private readonly string _employee = TestHost.Unique("reviewer").ToLowerInvariant();
    private Applicant _applicant = null!;

    public async ValueTask InitializeAsync()
    {
        _applicant = await Applicant.CreateAsync(fixture);
        var host = _applicant.Host;
        await host.PublishAsync(
            AppsControlAreas.Security,
            new Change(AppsControlChangeTypes.Role, _role, ChangeOperation.Create, new { nameAr = "مراجع", nameEn = "Reviewer" }),
            new Change(AppsControlChangeTypes.User, _employee, ChangeOperation.Create, new { nameAr = "مراجع", nameEn = "Reviewer", roles = new[] { new { roleCode = _role } } }));
        await host.PublishAsync(
            WorkflowChangeTypes.Area,
            new Change(WorkflowChangeTypes.Workflow, _workflow, ChangeOperation.Create, new { nameAr = "مراجعة", nameEn = "Review" }),
            new Change(WorkflowChangeTypes.Version, _workflow + "/1", ChangeOperation.Create, new
            {
                nameAr = "1",
                nameEn = "1",
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
        var definition = _sample.Definition();
        definition.Workflow = _workflow;
        await host.PublishAsync(ServiceChangeTypes.Area, [.. _sample.Questions(), _sample.ServiceChange(), _sample.Version(1, definition)]);
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
    }

    [Fact]
    public async Task A_service_whose_workflow_is_not_published_takes_no_requests()
    {
        var definition = _sample.Definition();
        definition.Workflow = TestHost.Unique("MISSING");
        await _applicant.Host.PublishAsync(ServiceChangeTypes.Area, _sample.Version(2, definition));

        await Should.ThrowAsync<RequestConflictException>(SubmitAsync);
    }

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
