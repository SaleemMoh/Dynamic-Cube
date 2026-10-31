using System.Text.Json;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.Services.Changes;
using DynamicCube.Modules.Workflow.Changes;
using DynamicCube.Modules.Workflow.Domain;
using DynamicCube.Modules.Workflow.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Workflow;
using DynamicCube.Tests.Infrastructure;
using Shouldly;

namespace DynamicCube.Modules.Workflow.Tests;

/// <summary>The classification workflow package (deploy/config-packages/classification-workflow.json) runs as the reference service describes.</summary>
public sealed class ClassificationPackageTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task A_request_goes_through_distribution_three_checked_branches_and_the_general_manager()
    {
        await using var host = new WorkflowHost(fixture);
        // The service first: publishing it gives it its workflow, whose version 2 the workflow package adds.
        var service = Items("classification-new.json");
        await host.Host.PublishAsync(AppsControlAreas.Lookups, [.. service.Where(i => i.EntityType == ServiceChangeTypes.Category)]);
        await host.Host.PublishAsync(ServiceChangeTypes.Area, [.. service.Where(i => i.EntityType != ServiceChangeTypes.Category)]);
        var items = Items("classification-workflow.json");
        await host.Host.PublishAsync(AppsControlAreas.Security, [.. items.Where(i => i.EntityType.StartsWith("apps.", StringComparison.Ordinal))]);
        await host.Host.PublishAsync(WorkflowChangeTypes.Area, [.. items.Where(i => i.EntityType.StartsWith("wf.", StringComparison.Ordinal))]);
        var subject = await host.StartAsync("CLASSIFICATION_NEW", level: "PREMIUM", sla: 35);

        // The request manager takes the request from the queue and sends it for analysis.
        var distribute = await Single(host, subject, "DISTRIBUTE");
        await host.AsAsync("manager", c => c.ClaimAsync(distribute.Id, CancellationToken.None));
        await host.AsAsync("manager", c => c.ActAsync(distribute.Id, new ActRequest("DISTRIBUTE", null), CancellationToken.None));

        // Three analyses wait for the manager to distribute them.
        (await host.OpenTasksAsync(subject)).Select(t => t.NodeCode).ShouldBe(["FIN_ANALYSIS", "GOV_ANALYSIS", "PROJECTS_ANALYSIS"]);
        (await host.AsAsync("manager", c => c.InboxAsync(InboxBox.Distribute, null, 0, 50, CancellationToken.None))).Items.Count(t => t.SubjectId == subject).ShouldBe(3);
        await AssignAsync(host, subject, "GOV_ANALYSIS", "analyst2");
        await AssignAsync(host, subject, "PROJECTS_ANALYSIS", "analyst1");
        await AssignAsync(host, subject, "FIN_ANALYSIS", "analyst1");

        // Maker-checker: the governance analyst may audit governance, but not their own analysis.
        await ActAsync(host, subject, "GOV_ANALYSIS", "analyst2", "SEND_TO_AUDIT");
        var audit = await Single(host, subject, "GOV_AUDIT");
        (await host.AsAsync("manager", c => c.CandidatesAsync(audit.Id, CancellationToken.None))).Select(s => s.UserName).ShouldBe(["auditor1"]);
        await Should.ThrowAsync<WorkflowForbiddenException>(() => host.AsAsync("analyst2", c => c.ClaimAsync(audit.Id, CancellationToken.None)));

        // Sent back, the analysis returns to the same analyst without another distribution.
        await host.AsAsync("auditor1", c => c.ClaimAsync(audit.Id, CancellationToken.None));
        await host.AsAsync("auditor1", c => c.ActAsync(audit.Id, new ActRequest("BACK_TO_ANALYST", "Add the board resolution."), CancellationToken.None));
        (await Single(host, subject, "GOV_ANALYSIS")).Assignee.ShouldBe("analyst2");
        await ActAsync(host, subject, "GOV_ANALYSIS", "analyst2", "SEND_TO_AUDIT");
        await ClaimAndActAsync(host, subject, "GOV_AUDIT", "auditor1", "CONFIRM");

        await ActAsync(host, subject, "PROJECTS_ANALYSIS", "analyst1", "SEND_TO_AUDIT");
        await ActAsync(host, subject, "FIN_ANALYSIS", "analyst1", "SEND_TO_AUDIT");
        await ClaimAndActAsync(host, subject, "PROJECTS_AUDIT", "auditor1", "CONFIRM");
        (await host.OpenTasksAsync(subject)).Select(t => t.NodeCode).ShouldBe(["FIN_AUDIT"], "the join waits for every branch");
        var waiting = await host.AsAsync("director", c => c.InstanceAsync(TestSubject.Type, subject, CancellationToken.None));
        waiting.CurrentNodes.ShouldBe(["FIN_AUDIT", "JOIN"], ignoreOrder: true, "two branches wait at the join, one is still audited");
        waiting.VisitedNodes.ShouldContain("SPLIT", "steps without tasks are recorded too");
        waiting.VisitedNodes.ShouldNotContain("GM_APPROVAL");
        waiting.Definition.ShouldNotBeNull()["nodes"]!.AsArray().Count.ShouldBe(13);
        await ClaimAndActAsync(host, subject, "FIN_AUDIT", "auditor1", "CONFIRM");

        host.Subject.Last(subject).DisplayStatus.ShouldBe("PENDING_APPROVAL");
        await ClaimAndActAsync(host, subject, "GM_APPROVAL", "director", "APPROVE");
        host.Subject.Last(subject).Status.ShouldBe(SubjectStatus.Approved);
        (await host.InstanceAsync(subject)).Status.ShouldBe(InstanceStatus.Completed);
        var timeline = (await host.AsAsync("director", c => c.InstanceAsync(TestSubject.Type, subject, CancellationToken.None))).Timeline;
        timeline.Select(e => e.Type).Take(3).ShouldBe(["STARTED", "TASK_CREATED", "CLAIMED"], "events of one step keep the order they happened in");
        // SLA events are left out: other tests run the SLA monitor with clocks far ahead, over every instance in the database.
        timeline.Select(e => e.Type).Where(t => !t.StartsWith("SLA_", StringComparison.Ordinal)).TakeLast(3).ShouldBe(["CLAIMED", "ACTION", "COMPLETED"]);
        timeline.First(e => e.Type == "ASSIGNED" && e.Target == "analyst2").TargetNameEn.ShouldBe("Second Analyst");
        var done = await host.AsAsync("director", c => c.InstanceAsync(TestSubject.Type, subject, CancellationToken.None));
        done.CurrentNodes.ShouldBeEmpty();
        done.VisitedNodes.ShouldContain("START");
        done.VisitedNodes.ShouldContain("JOIN");
        done.VisitedNodes.ShouldContain("APPROVED");
        done.VisitedNodes.ShouldNotContain("REJECTED");
    }

    private static async Task<WorkTask> Single(WorkflowHost host, Guid subject, string node) =>
        (await host.OpenTasksAsync(subject)).Single(t => t.NodeCode == node);

    private static async Task AssignAsync(WorkflowHost host, Guid subject, string node, string user)
    {
        var task = await Single(host, subject, node);
        await host.AsAsync("manager", c => c.AssignAsync(task.Id, new AssignRequest(user), CancellationToken.None));
    }

    private static async Task ActAsync(WorkflowHost host, Guid subject, string node, string user, string action)
    {
        var task = await Single(host, subject, node);
        await host.AsAsync(user, c => c.ActAsync(task.Id, new ActRequest(action, null), CancellationToken.None));
    }

    private static async Task ClaimAndActAsync(WorkflowHost host, Guid subject, string node, string user, string action)
    {
        var task = await Single(host, subject, node);
        await host.AsAsync(user, c => c.ClaimAsync(task.Id, CancellationToken.None));
        await host.AsAsync(user, c => c.ActAsync(task.Id, new ActRequest(action, null), CancellationToken.None));
    }

    private static List<Change> Items(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory!.FullName, "DynamicCube.slnx")))
        {
            directory = directory.Parent;
        }

        using var package = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory.FullName, "deploy/config-packages/" + file)));
        return [.. package.RootElement.GetProperty("items").EnumerateArray().Select(i => new Change(
            i.GetProperty("entityType").GetString()!,
            i.GetProperty("entityCode").GetString()!,
            Enum.Parse<ChangeOperation>(i.GetProperty("operation").GetString()!, ignoreCase: true),
            i.GetProperty("payload").Clone()))];
    }
}
