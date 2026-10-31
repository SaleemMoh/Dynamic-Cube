using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.Workflow.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Tests.Infrastructure;
using Shouldly;
using static DynamicCube.Modules.Workflow.Tests.WorkflowHost;

namespace DynamicCube.Modules.Workflow.Tests;

/// <summary>Tasks belong to a company and branch (ADR-0036 step 6): queues show them to the role's holders in that branch.</summary>
public sealed class BranchTaskTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task A_queued_task_reaches_the_role_holders_of_its_branch_only()
    {
        await using var host = new WorkflowHost(fixture);
        var (company, north, south) = (Unique("CO"), Unique("NORTH"), Unique("SOUTH"));
        await host.Host.PublishAsync(AppsControlAreas.System,
            new Change(AppsControlChangeTypes.Company, company, ChangeOperation.Create, new { nameAr = "شركة", nameEn = "Company", displayOrder = 999 }),
            new Change(AppsControlChangeTypes.Branch, north, ChangeOperation.Create, new { nameAr = "شمال", nameEn = "North", companyCode = company }),
            new Change(AppsControlChangeTypes.Branch, south, ChangeOperation.Create, new { nameAr = "جنوب", nameEn = "South", companyCode = company }));
        var roles = await host.RolesAsync("REVIEWER");
        var (inNorth, inSouth, everywhere) = (User("north"), User("south"), User("all"));
        await host.Host.PublishAsync(AppsControlAreas.Security,
            Employee(inNorth, roles[0], company, north),
            Employee(inSouth, roles[0], company, south),
            Employee(everywhere, roles[0], "*", "*"));
        var workflow = await host.WorkflowAsync(
        [
            Start("REVIEW"),
            Human("REVIEW", Role(roles[0]), [Act("APPROVE", "OK", "APPROVE")]),
            End("OK", "APPROVED"),
        ]);

        var subject = await host.StartAsync(workflow, company: company, branch: north);
        var task = (await host.OpenTasksAsync(subject)).ShouldHaveSingleItem();
        (task.CompanyCode, task.BranchCode).ShouldBe((company, north));

        Task<bool> InQueue(string user) => host.AsAsync(user, async c => (await c.InboxAsync(InboxBox.Queue, null, 0, 200, CancellationToken.None)).Items.Any(t => t.Id == task.Id));
        (await InQueue(inNorth)).ShouldBeTrue("the role held in the task's branch");
        (await InQueue(inSouth)).ShouldBeFalse("the role held in another branch");
        await Should.ThrowAsync<WorkflowForbiddenException>(() => host.AsAsync(inSouth, c => c.ClaimAsync(task.Id, CancellationToken.None)));

        // Held in every company, the task shows while working in its company.
        host.Host.Company = company;
        (await InQueue(everywhere)).ShouldBeTrue("the role held in every company and branch");
        host.Host.Company = null;

        await host.AsAsync(inNorth, c => c.ClaimAsync(task.Id, CancellationToken.None));
        (await host.AsAsync(inNorth, c => c.InboxAsync(InboxBox.Mine, null, 0, 200, CancellationToken.None))).Items.ShouldContain(t => t.Id == task.Id && t.BranchCode == north);
    }

    private static Change Employee(string user, string role, string company, string branch) =>
        new(AppsControlChangeTypes.User, user, ChangeOperation.Create, new
        {
            nameAr = "موظف",
            nameEn = "Employee " + user,
            roles = new[] { new { roleCode = role, companyCode = company, branches = new[] { branch } } },
        });
}
