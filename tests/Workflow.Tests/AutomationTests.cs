using System.Text.Json.Nodes;
using DynamicCube.Modules.Workflow.Changes;
using DynamicCube.Modules.Workflow.Domain;
using DynamicCube.Modules.Workflow.Persistence;
using DynamicCube.Modules.Workflow.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using static DynamicCube.Modules.Workflow.Tests.WorkflowHost;

namespace DynamicCube.Modules.Workflow.Tests;

public sealed class AutomationTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task A_rule_assigns_waiting_tasks_once_and_logs_it()
    {
        await using var host = new WorkflowHost(fixture);
        var (workflow, subject, users) = await QueueAsync(host);
        var rule = Unique("ASSIGN");
        await RuleAsync(host, rule, workflow, new JsonObject { [">="] = new JsonArray(new JsonObject { ["var"] = "task.unassignedHours" }, 2) }, AutomationAction.AssignLeastLoaded);

        (await TestAsync(host, workflow, 2, AutomationAction.AssignLeastLoaded)).ShouldBeEmpty();
        (await RunAsync(host)).ShouldBe(0);

        host.Clock.Now = host.Clock.Now.AddHours(3);
        var match = (await TestAsync(host, workflow, 2, AutomationAction.AssignLeastLoaded)).ShouldHaveSingleItem();
        match.Detail.ShouldStartWith("Assign to ");
        (await host.OpenTasksAsync(subject)).Single().Assignee.ShouldBeNull();

        (await RunAsync(host)).ShouldBeGreaterThanOrEqualTo(1);
        var task = (await host.OpenTasksAsync(subject)).Single();
        users.ShouldContain(task.Assignee!);
        task.AssignedBy.ShouldBe(AutomationRunner.Actor);

        await RunAsync(host);
        var log = await LogAsync(host, rule);
        log.Items.ShouldHaveSingleItem().Outcome.ShouldBe(AutomationOutcome.Done);
        log.Items[0].SubjectNumber.ShouldNotBeNullOrEmpty();
        var events = await host.Host.AsAsync("admin", sp => sp.GetRequiredService<WorkflowDbContext>().Events.Where(e => e.TaskId == task.Id && e.Type == WorkflowEventTypes.Assigned).ToListAsync());
        events.ShouldHaveSingleItem().Comment.ShouldBe(rule);
    }

    [Fact]
    public async Task A_reminder_repeats_after_its_hours_and_escalation_reaches_the_role()
    {
        await using var host = new WorkflowHost(fixture);
        var (workflow, subject, _) = await QueueAsync(host);
        var remind = Unique("REMIND");
        await RuleAsync(host, remind, workflow, new JsonObject { [">="] = new JsonArray(new JsonObject { ["var"] = "task.ageHours" }, 1) }, AutomationAction.Remind, repeatHours: 24);
        var escalate = Unique("ESC");
        await RuleAsync(host, escalate, workflow, new JsonObject { [">="] = new JsonArray(new JsonObject { ["var"] = "task.ageHours" }, 48) }, AutomationAction.Escalate, role: "SUPERVISORS");

        host.Clock.Now = host.Clock.Now.AddHours(2);
        await RunAsync(host);
        await RunAsync(host);
        (await LogAsync(host, remind)).Total.ShouldBe(1);

        host.Clock.Now = host.Clock.Now.AddHours(25);
        await RunAsync(host);
        (await LogAsync(host, remind)).Total.ShouldBe(2);
        (await host.OpenTasksAsync(subject)).Single().EscalatedToRole.ShouldBeNull();

        host.Clock.Now = host.Clock.Now.AddHours(24);
        await RunAsync(host);
        (await host.OpenTasksAsync(subject)).Single().EscalatedToRole.ShouldBe("SUPERVISORS");
        (await LogAsync(host, escalate)).Items.ShouldHaveSingleItem().Detail.ShouldBe("Escalated to SUPERVISORS");
    }

    [Fact]
    public async Task Broken_rules_are_refused()
    {
        await using var host = new WorkflowHost(fixture);
        var ex = await Should.ThrowAsync<ChangeValidationException>(() => host.Host.PublishAsync(
            WorkflowChangeTypes.Area,
            new Change(WorkflowChangeTypes.AutomationRule, Unique("BAD"), ChangeOperation.Create, new
            {
                nameAr = "خطأ",
                nameEn = "Bad",
                when = new JsonObject { ["nope"] = new JsonArray(1) },
                action = "ESCALATE",
                repeatHours = 0,
            })));
        ex.Errors.Keys.ShouldContain("when");
        ex.Errors.Keys.ShouldContain("escalateToRole");
        ex.Errors.Keys.ShouldContain("repeatHours");
    }

    private static async Task<(string Workflow, Guid Subject, string[] Users)> QueueAsync(WorkflowHost host)
    {
        var roles = await host.RolesAsync("AUTO_REVIEWER");
        string[] users = [User("ra"), User("rb")];
        await host.UsersAsync((users[0], roles), (users[1], roles));
        var workflow = await host.WorkflowAsync([Start("REVIEW"), Human("REVIEW", Role(roles[0]), [Act("OK", "END")]), End("END", "APPROVED")]);
        var subject = await host.StartAsync(workflow);
        return (workflow, subject, users);
    }

    private static Task<Guid> RuleAsync(WorkflowHost host, string code, string workflow, JsonNode when, AutomationAction action, int? repeatHours = null, string? role = null) =>
        host.Host.PublishAsync(
            WorkflowChangeTypes.Area,
            new Change(WorkflowChangeTypes.AutomationRule, code, ChangeOperation.Create, new
            {
                nameAr = code,
                nameEn = code,
                workflows = new[] { workflow },
                steps = new[] { "REVIEW" },
                when,
                action = action switch { AutomationAction.AssignLeastLoaded => "ASSIGN_LEAST_LOADED", AutomationAction.Remind => "REMIND", _ => "ESCALATE" },
                escalateToRole = role,
                repeatHours,
            }));

    private static Task<IReadOnlyList<AutomationMatch>> TestAsync(WorkflowHost host, string workflow, int hours, AutomationAction action) =>
        host.Host.AsAsync("admin", sp => sp.GetRequiredService<AutomationConsole>().TestAsync(
            new AutomationTestRequest(null, [workflow], ["REVIEW"], new JsonObject { [">="] = new JsonArray(new JsonObject { ["var"] = "task.unassignedHours" }, hours) }, action, null, null),
            CancellationToken.None));

    private static Task<int> RunAsync(WorkflowHost host) => host.Host.AsAsync("worker", sp => WorkflowModule.RunAutomationAsync(sp, CancellationToken.None));

    private static Task<AutomationLogPage> LogAsync(WorkflowHost host, string rule) =>
        host.Host.AsAsync("admin", sp => sp.GetRequiredService<AutomationConsole>().LogAsync(rule, null, 0, 50, CancellationToken.None));
}
