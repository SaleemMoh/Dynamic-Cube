using System.Text.Json.Nodes;
using DynamicCube.Modules.Workflow.Changes;
using DynamicCube.Modules.Workflow.Domain;
using DynamicCube.Modules.Workflow.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Workflow;
using DynamicCube.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using static DynamicCube.Modules.Workflow.Tests.WorkflowHost;

namespace DynamicCube.Modules.Workflow.Tests;

public sealed class EngineTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Broken_graphs_are_refused()
    {
        await using var host = new WorkflowHost(fixture);
        var code = Unique("WF");

        var ex = await Should.ThrowAsync<ChangeValidationException>(() => host.Host.PublishAsync(
            WorkflowChangeTypes.Area,
            new Change(WorkflowChangeTypes.Workflow, code, ChangeOperation.Create, new { nameAr = "مسار", nameEn = "Workflow" }),
            new Change(WorkflowChangeTypes.Version, code + "/1", ChangeOperation.Create, new
            {
                nameAr = "1",
                nameEn = "1",
                definition = new
                {
                    nodes = new object[]
                    {
                        Start("NOWHERE"),
                        Human("REVIEW", Role("R", mode: "DISTRIBUTE"), [Act("OK", "DONE"), new { code = "BACK", kind = "RETURN", nameAr = "إعادة", nameEn = "Back", to = "REVIEW" }]),
                        End("DONE", "APPROVED"),
                        Split("SPLIT", "REVIEW"),
                    },
                },
            })));

        ex.Errors.Keys.ShouldContain("definition.nodes[0].next");
        ex.Errors.Keys.ShouldContain("definition.nodes[1].assignment.distributorRole");
        ex.Errors.Keys.ShouldContain("definition.nodes[1].actions[1].to");
        ex.Errors.Keys.ShouldContain("definition.nodes[3].branches");
    }

    [Fact]
    public async Task Maker_checker_keeps_the_analyst_off_the_audit_and_approval_ends_the_request()
    {
        await using var host = new WorkflowHost(fixture);
        var (analyst, auditor) = (User("ana"), User("aud"));
        var roles = await host.RolesAsync("ANALYST", "AUDITOR");
        await host.UsersAsync((analyst, roles), (auditor, [roles[1]]));
        var workflow = await host.WorkflowAsync(
        [
            Start("ANALYSIS"),
            Human("ANALYSIS", Role(roles[0]), [Act("SEND", "AUDIT"), Return()]),
            Human("AUDIT", Role(roles[1]), [Act("APPROVE", "OK", "APPROVE"), Act("REJECT", "NO", "REJECT")], exclude: ["ANALYSIS"]),
            End("OK", "APPROVED"),
            End("NO", "REJECTED"),
        ]);

        var subject = await host.StartAsync(workflow);
        host.Subject.Last(subject).ShouldBe(new SubjectUpdate(SubjectStatus.InProgress, "UNDER_REVIEW", "STATUS_CHANGED", null));
        var analysis = (await host.OpenTasksAsync(subject)).ShouldHaveSingleItem();
        await Should.ThrowAsync<WorkflowForbiddenException>(() => host.AsAsync(analyst, c => c.ActAsync(analysis.Id, new ActRequest("SEND", null), CancellationToken.None)));
        await host.AsAsync(analyst, c => c.ClaimAsync(analysis.Id, CancellationToken.None));
        await host.AsAsync(analyst, c => c.ActAsync(analysis.Id, new ActRequest("SEND", "ready"), CancellationToken.None));

        var audit = (await host.OpenTasksAsync(subject)).ShouldHaveSingleItem();
        audit.NodeCode.ShouldBe("AUDIT");
        audit.ExcludedUsers.ShouldBe([analyst]);
        (await host.AsAsync(analyst, c => c.InboxAsync(InboxBox.Queue, null, 0, 50, CancellationToken.None))).Items.ShouldNotContain(t => t.Id == audit.Id);
        await Should.ThrowAsync<WorkflowForbiddenException>(() => host.AsAsync(analyst, c => c.ClaimAsync(audit.Id, CancellationToken.None)));

        await host.AsAsync(auditor, c => c.ClaimAsync(audit.Id, CancellationToken.None));
        var refused = await Should.ThrowAsync<WorkflowValidationException>(() => host.AsAsync(auditor, c => c.ActAsync(audit.Id, new ActRequest("REJECT", " "), CancellationToken.None)));
        refused.Field.ShouldBe("comment");
        await host.AsAsync(auditor, c => c.ActAsync(audit.Id, new ActRequest("APPROVE", null), CancellationToken.None));

        var instance = await host.InstanceAsync(subject);
        instance.Status.ShouldBe(InstanceStatus.Completed);
        instance.Outcome.ShouldBe("APPROVED");
        host.Subject.Last(subject).Status.ShouldBe(SubjectStatus.Approved);
        (await host.OpenTasksAsync(subject)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_join_of_all_branches_waits_for_every_branch()
    {
        await using var host = new WorkflowHost(fixture);
        var user = User("reviewer");
        var roles = await host.RolesAsync("REVIEWER");
        await host.UsersAsync((user, roles));
        var workflow = await host.WorkflowAsync(
        [
            Start("SPLIT"),
            Split("SPLIT", "B1", "B2", "B3"),
            Human("B1", Role(roles[0], "AUTO"), [Act("DONE", "JOIN")]),
            Human("B2", Role(roles[0], "AUTO"), [Act("DONE", "JOIN")]),
            Human("B3", Role(roles[0], "AUTO"), [Act("DONE", "JOIN")]),
            Join("JOIN", "FINAL"),
            Human("FINAL", Role(roles[0], "AUTO"), [Act("OK", "END", "APPROVE")]),
            End("END", "APPROVED"),
        ]);

        var subject = await host.StartAsync(workflow);
        var branches = await host.OpenTasksAsync(subject);
        branches.Select(t => t.NodeCode).ShouldBe(["B1", "B2", "B3"]);
        branches.ShouldAllBe(t => t.Assignee == user);

        await host.AsAsync(user, c => c.ActAsync(branches[1].Id, new ActRequest("DONE", null), CancellationToken.None));
        await host.AsAsync(user, c => c.ActAsync(branches[0].Id, new ActRequest("DONE", null), CancellationToken.None));
        (await host.OpenTasksAsync(subject)).Select(t => t.NodeCode).ShouldBe(["B3"]);
        await host.AsAsync(user, c => c.ActAsync(branches[2].Id, new ActRequest("DONE", null), CancellationToken.None));

        var final = (await host.OpenTasksAsync(subject)).ShouldHaveSingleItem();
        final.NodeCode.ShouldBe("FINAL");
        await host.AsAsync(user, c => c.ActAsync(final.Id, new ActRequest("OK", null), CancellationToken.None));
        (await host.InstanceAsync(subject)).Outcome.ShouldBe("APPROVED");
    }

    [Fact]
    public async Task A_join_of_any_branch_cancels_the_others()
    {
        await using var host = new WorkflowHost(fixture);
        var user = User("reviewer");
        var roles = await host.RolesAsync("REVIEWER");
        await host.UsersAsync((user, roles));
        var workflow = await host.WorkflowAsync(
        [
            Start("SPLIT"),
            Split("SPLIT", "B1", "B2"),
            Human("B1", Role(roles[0], "AUTO"), [Act("DONE", "JOIN")]),
            Human("B2", Role(roles[0], "AUTO"), [Act("DONE", "JOIN")]),
            Join("JOIN", "END", mode: "ANY"),
            End("END", "CLOSED"),
        ]);

        var subject = await host.StartAsync(workflow);
        var first = (await host.OpenTasksAsync(subject))[0];
        await host.AsAsync(user, c => c.ActAsync(first.Id, new ActRequest("DONE", null), CancellationToken.None));

        (await host.OpenTasksAsync(subject)).ShouldBeEmpty();
        (await host.InstanceAsync(subject)).Outcome.ShouldBe("CLOSED");
        var b2 = await host.Host.AsAsync("admin", sp => Task.FromResult(sp.GetRequiredService<Persistence.WorkflowDbContext>().Tasks.Single(t => t.SubjectId == subject && t.NodeCode == "B2")));
        b2.Status.ShouldBe(WorkTaskStatus.Cancelled);
    }

    [Fact]
    public async Task A_distributor_assigns_tasks_only_to_candidates()
    {
        await using var host = new WorkflowHost(fixture);
        var (manager, first, second, outsider) = (User("mgr"), User("an"), User("an"), User("out"));
        var roles = await host.RolesAsync("MANAGER", "ANALYST", "OTHER");
        await host.UsersAsync((manager, [roles[0]]), (first, [roles[1]]), (second, [roles[1]]), (outsider, [roles[2]]));
        var workflow = await host.WorkflowAsync(
        [
            Start("REVIEW"),
            Human("REVIEW", Role(roles[1], "DISTRIBUTE", roles[0]), [Act("OK", "END")]),
            End("END", "APPROVED"),
        ]);

        var subject = await host.StartAsync(workflow);
        var task = (await host.OpenTasksAsync(subject)).ShouldHaveSingleItem();
        (await host.AsAsync(first, c => c.InboxAsync(InboxBox.Queue, null, 0, 50, CancellationToken.None))).Items.ShouldBeEmpty();
        (await host.AsAsync(manager, c => c.InboxAsync(InboxBox.Distribute, null, 0, 50, CancellationToken.None))).Items.ShouldHaveSingleItem().Id.ShouldBe(task.Id);
        (await host.AsAsync(manager, c => c.CandidatesAsync(task.Id, CancellationToken.None))).Select(c => c.UserName).ShouldBe([first, second], ignoreOrder: true);
        await Should.ThrowAsync<WorkflowForbiddenException>(() => host.AsAsync(first, c => c.ClaimAsync(task.Id, CancellationToken.None)));
        await Should.ThrowAsync<WorkflowValidationException>(() => host.AsAsync(manager, c => c.AssignAsync(task.Id, new AssignRequest(outsider), CancellationToken.None)));

        await host.AsAsync(manager, c => c.AssignAsync(task.Id, new AssignRequest(second), CancellationToken.None));

        var mine = await host.AsAsync(second, c => c.InboxAsync(InboxBox.Mine, null, 0, 50, CancellationToken.None));
        mine.Items.ShouldHaveSingleItem().Id.ShouldBe(task.Id);
        (await host.AsAsync(second, c => c.CountsAsync(CancellationToken.None))).Mine.ShouldBe(1);
    }

    [Fact]
    public async Task Automatic_assignment_picks_the_least_loaded_candidate()
    {
        await using var host = new WorkflowHost(fixture);
        var (first, second) = ("a" + User("an"), "b" + User("an"));
        var roles = await host.RolesAsync("ANALYST");
        await host.UsersAsync((first, roles), (second, roles));
        var workflow = await host.WorkflowAsync(
        [
            Start("REVIEW"),
            Human("REVIEW", Role(roles[0], "AUTO"), [Act("OK", "END")]),
            End("END", "APPROVED"),
        ]);

        var one = await host.StartAsync(workflow);
        var two = await host.StartAsync(workflow);
        var three = await host.StartAsync(workflow);

        (await host.OpenTasksAsync(one)).Single().Assignee.ShouldBe(first);
        (await host.OpenTasksAsync(two)).Single().Assignee.ShouldBe(second);
        (await host.OpenTasksAsync(three)).Single().Assignee.ShouldBe(first);
    }

    [Fact]
    public async Task A_return_pauses_the_clock_and_the_resubmission_goes_back_to_the_same_employee()
    {
        await using var host = new WorkflowHost(fixture);
        var (analyst, colleague) = (User("ana"), User("ana"));
        var roles = await host.RolesAsync("ANALYST");
        await host.UsersAsync((analyst, roles), (colleague, roles));
        var workflow = await host.WorkflowAsync(
        [
            Start("ANALYSIS"),
            Human("ANALYSIS", Role(roles[0]), [Act("OK", "END"), Return()], sla: new { workingDays = 5 }),
            End("END", "APPROVED"),
        ]);

        // Sunday 1 November 2026, 09:00 Riyadh: 10 working days → Sunday 15 November.
        var subject = await host.StartAsync(workflow, sla: 10);
        (await host.InstanceAsync(subject)).DueAt.ShouldBe(new DateTimeOffset(2026, 11, 15, 9, 0, 0, TimeSpan.FromHours(3)));
        var task = (await host.OpenTasksAsync(subject)).Single();
        await host.AsAsync(analyst, c => c.ClaimAsync(task.Id, CancellationToken.None));
        await host.AsAsync(analyst, c => c.ActAsync(task.Id, new ActRequest("RETURN", "Attach the audited statements."), CancellationToken.None));

        host.Subject.Last(subject).ShouldBe(new SubjectUpdate(SubjectStatus.PendingCustomer, "RETURNED", "RETURNED", "Attach the audited statements."));
        (await host.InstanceAsync(subject)).Status.ShouldBe(InstanceStatus.WaitingCustomer);
        (await host.OpenTasksAsync(subject)).ShouldBeEmpty();

        // The applicant answers three working days later (Wednesday 4 November).
        host.Clock.Now = new DateTimeOffset(2026, 11, 4, 9, 0, 0, TimeSpan.FromHours(3));
        await host.Host.AsAsync("applicant", sp => sp.GetRequiredService<IWorkflowRuntime>().ResumeAsync(TestSubject.Type, subject, CancellationToken.None));

        var instance = await host.InstanceAsync(subject);
        instance.Status.ShouldBe(InstanceStatus.Running);
        instance.DueAt.ShouldBe(new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.FromHours(3)));
        var again = (await host.OpenTasksAsync(subject)).ShouldHaveSingleItem();
        again.Assignee.ShouldBe(analyst);
        host.Subject.Last(subject).Status.ShouldBe(SubjectStatus.InProgress);
        var timeline = await host.AsAsync(analyst, c => c.GetAsync(again.Id, CancellationToken.None));
        timeline.Instance.Timeline.Select(e => e.Type).ShouldContain(WorkflowEventTypes.Resubmitted);
    }

    [Fact]
    public async Task Gateways_route_by_the_answers()
    {
        await using var host = new WorkflowHost(fixture);
        var roles = await host.RolesAsync("REVIEWER");
        var workflow = await host.WorkflowAsync(
        [
            Start("ROUTE"),
            Gateway("ROUTE", [new { when = JsonNode.Parse("""{">": [{"var": "data.AMOUNT"}, 1000]}"""), to = "BIG" }], "SMALL"),
            Human("BIG", Role(roles[0]), [Act("OK", "END")]),
            Human("SMALL", Role(roles[0]), [Act("OK", "END")]),
            End("END", "APPROVED"),
        ]);

        var small = await host.StartAsync(workflow, new JsonObject { ["AMOUNT"] = 500 });
        var big = await host.StartAsync(workflow, new JsonObject { ["AMOUNT"] = 5000 });

        (await host.OpenTasksAsync(small)).Single().NodeCode.ShouldBe("SMALL");
        (await host.OpenTasksAsync(big)).Single().NodeCode.ShouldBe("BIG");

        // A task waiting in a role's queue tells the role (notification event).
        var created = (await host.Host.RaisedAsync(big)).ShouldHaveSingleItem();
        created.Code.ShouldBe("TASK_CREATED");
        created.Facts["task"]!["role"]!.GetValue<string>().ShouldBe(roles[0]);
        created.Facts["task"]!["node"]!.GetValue<string>().ShouldBe("BIG");
    }

    [Fact]
    public async Task A_delegate_works_on_the_delegators_tasks()
    {
        await using var host = new WorkflowHost(fixture);
        var (boss, deputy) = (User("boss"), User("deputy"));
        var roles = await host.RolesAsync("HEAD");
        await host.UsersAsync((boss, roles), (deputy, []));
        var workflow = await host.WorkflowAsync(
        [
            Start("SIGN"),
            Human("SIGN", new { target = "USER", user = boss, mode = "QUEUE" }, [Act("OK", "END", "APPROVE")]),
            End("END", "APPROVED"),
        ]);
        var subject = await host.StartAsync(workflow);
        var task = (await host.OpenTasksAsync(subject)).Single();
        task.Assignee.ShouldBe(boss);

        await Should.ThrowAsync<WorkflowForbiddenException>(() => host.AsAsync(deputy, c => c.ActAsync(task.Id, new ActRequest("OK", null), CancellationToken.None)));
        await host.AsAsync(boss, c => c.DelegateAsync(new DelegationRequest(deputy, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 10), "Leave"), CancellationToken.None));

        (await host.AsAsync(deputy, c => c.InboxAsync(InboxBox.Mine, null, 0, 50, CancellationToken.None))).Items.ShouldHaveSingleItem().Id.ShouldBe(task.Id);
        (await host.AsAsync(deputy, c => c.GetAsync(task.Id, CancellationToken.None))).OnBehalfOf.ShouldBe(boss);
        await host.AsAsync(deputy, c => c.ActAsync(task.Id, new ActRequest("OK", null), CancellationToken.None));

        var events = await host.Host.AsAsync("admin", sp => Task.FromResult(sp.GetRequiredService<Persistence.WorkflowDbContext>().Events.Where(e => e.TaskId == task.Id && e.Type == WorkflowEventTypes.Action).ToList()));
        events.ShouldHaveSingleItem().OnBehalfOf.ShouldBe(boss);
        events[0].Actor.ShouldBe(deputy);
    }

    [Fact]
    public async Task Overdue_tasks_turn_amber_then_red_and_reach_the_escalation_role()
    {
        await using var host = new WorkflowHost(fixture);
        var escalation = User("esc");
        var roles = await host.RolesAsync("REVIEWER", "ESCALATION");
        await host.UsersAsync((escalation, [roles[1]]));
        var workflow = await host.WorkflowAsync(
        [
            Start("REVIEW"),
            Human("REVIEW", Role(roles[0]), [Act("OK", "END")], sla: new { workingDays = 2, warnPercent = 50, escalateToRole = roles[1] }),
            End("END", "APPROVED"),
        ]);

        var subject = await host.StartAsync(workflow, sla: 1);
        var task = (await host.OpenTasksAsync(subject)).Single();
        task.DueAt.ShouldBe(new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.FromHours(3)));

        host.Clock.Now = new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.FromHours(3));
        await host.Host.AsAsync("worker", sp => WorkflowModule.CheckSlaAsync(sp, CancellationToken.None));
        (await host.OpenTasksAsync(subject)).Single().Sla.ShouldBe(SlaState.Warning);
        (await host.AsAsync(escalation, c => c.InboxAsync(InboxBox.Escalated, null, 0, 50, CancellationToken.None))).Items.ShouldBeEmpty();

        host.Clock.Now = new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.FromHours(3));
        await host.Host.AsAsync("worker", sp => WorkflowModule.CheckSlaAsync(sp, CancellationToken.None));
        (await host.OpenTasksAsync(subject)).Single().Sla.ShouldBe(SlaState.Breached);
        (await host.InstanceAsync(subject)).Sla.ShouldBe(SlaState.Breached);
        (await host.AsAsync(escalation, c => c.InboxAsync(InboxBox.Escalated, null, 0, 50, CancellationToken.None))).Items.ShouldHaveSingleItem().Id.ShouldBe(task.Id);

        var raised = await host.Host.RaisedAsync(subject);
        raised.Select(e => e.Code).ShouldBe(["TASK_CREATED", "TASK_SLA_WARNING", "TASK_SLA_BREACHED"]);
        raised[^1].Facts["task"]!["escalatedToRole"]!.GetValue<string>().ShouldBe(roles[1]);
    }
}
