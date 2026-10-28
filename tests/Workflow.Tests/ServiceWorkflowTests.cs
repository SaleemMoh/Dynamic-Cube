using DynamicCube.Modules.Workflow.Changes;
using DynamicCube.Modules.Workflow.Domain;
using DynamicCube.Modules.Workflow.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Workflow;
using DynamicCube.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Workflow.Tests;

/// <summary>Every service has its own workflow, starting with one that completes requests without review.</summary>
public sealed class ServiceWorkflowTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task A_new_service_gets_a_workflow_that_completes_requests_without_review()
    {
        await using var host = new WorkflowHost(fixture);
        var service = await host.ServiceAsync();

        var workflow = await host.Host.AsAsync("admin", sp => sp.GetRequiredService<WorkflowDbContext>().Workflows.AsNoTracking().SingleAsync(w => w.ServiceCode == service));
        workflow.Code.ShouldBe(service);
        workflow.NameEn.ShouldBe($"Service {service} workflow");
        var version = await host.Host.AsAsync("admin", sp => sp.GetRequiredService<WorkflowDbContext>().Versions.AsNoTracking().SingleAsync(v => v.WorkflowId == workflow.Id));
        version.Code.ShouldBe(service + "/1");
        version.PublishedAt.ShouldNotBeNull();
        (await host.Host.AsAsync("admin", sp => sp.GetRequiredService<IServiceWorkflows>().WorkflowOfAsync(service, CancellationToken.None))).ShouldBe(service);

        // A request on it is approved as soon as it starts.
        var subject = await host.StartAsync(service);
        var instance = await host.InstanceAsync(subject);
        instance.Status.ShouldBe(InstanceStatus.Completed);
        instance.Outcome.ShouldBe("APPROVED");
        host.Subject.Last(subject).Status.ShouldBe(SubjectStatus.Approved);
        (await host.OpenTasksAsync(subject)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_service_keeps_exactly_one_workflow()
    {
        await using var host = new WorkflowHost(fixture);
        var service = await host.ServiceAsync();
        var other = await host.ServiceAsync();

        var second = await Should.ThrowAsync<ChangeValidationException>(() => host.Host.PublishAsync(
            WorkflowChangeTypes.Area,
            new Change(WorkflowChangeTypes.Workflow, TestHost.Unique("WF"), ChangeOperation.Create, new { nameAr = "مسار", nameEn = "Workflow", serviceCode = service })));
        second.Errors["serviceCode"].ShouldContain(m => m.Contains("already has its workflow"));

        var orphan = await Should.ThrowAsync<ChangeValidationException>(() => host.Host.PublishAsync(
            WorkflowChangeTypes.Area,
            new Change(WorkflowChangeTypes.Workflow, TestHost.Unique("WF"), ChangeOperation.Create, new { nameAr = "مسار", nameEn = "Workflow" }),
            new Change(WorkflowChangeTypes.Workflow, TestHost.Unique("WF"), ChangeOperation.Create, new { nameAr = "مسار", nameEn = "Workflow", serviceCode = "NO_SUCH_SERVICE" })));
        orphan.Errors.Keys.ShouldContain("serviceCode");

        var moved = await Should.ThrowAsync<ChangeValidationException>(() => host.Host.PublishAsync(
            WorkflowChangeTypes.Area,
            new Change(WorkflowChangeTypes.Workflow, service, ChangeOperation.Update, new { nameAr = "مسار", nameEn = "Workflow", serviceCode = other })));
        moved.Errors["serviceCode"].ShouldContain(m => m.Contains("does not move"));

        await Should.ThrowAsync<ChangeSetException>(() => host.Host.PublishAsync(WorkflowChangeTypes.Area, new Change(WorkflowChangeTypes.Workflow, service, ChangeOperation.Delete, null)));

        // Renaming it is fine.
        await host.Host.PublishAsync(WorkflowChangeTypes.Area, new Change(WorkflowChangeTypes.Workflow, service, ChangeOperation.Update, new { nameAr = "مراجعة", nameEn = "Review", serviceCode = service }));
    }
}
