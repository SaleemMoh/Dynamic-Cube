using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.Workflow.Changes;
using DynamicCube.Modules.Workflow.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Tests.Infrastructure;
using Shouldly;
using static DynamicCube.Modules.Workflow.Tests.WorkflowHost;

namespace DynamicCube.Modules.Workflow.Tests;

/// <summary>Tasks routed through the organization structure: a unit by rule, the previous actor's unit and the one above, sub-units.</summary>
public sealed class OrgRoutingTests(DatabaseFixture fixture)
{
    private sealed record Org(string Root, string North, string South, string Analyst, string Director, string NorthManager, string NorthStaff, string SouthStaff);

    /// <summary>A department with two branches; the north branch has a manager, the south one does not.</summary>
    private static async Task<Org> OrgAsync(WorkflowHost host)
    {
        var (type, root, north, south) = (Unique("TYPE"), Unique("DEPT"), Unique("NORTH"), Unique("SOUTH"));
        var roles = await host.RolesAsync("ANALYST");
        var org = new Org(root, north, south, roles[0], User("dir"), User("nmgr"), User("nstaff"), User("sstaff"));
        await host.Host.PublishAsync(
            AppsControlAreas.Organization,
            new Change(AppsControlChangeTypes.OrgUnitType, type, ChangeOperation.Create, new { nameAr = "نوع", nameEn = "Type" }),
            new Change(AppsControlChangeTypes.OrgUnit, root, ChangeOperation.Create, new { nameAr = "إدارة", nameEn = "Department", typeCode = type }),
            new Change(AppsControlChangeTypes.OrgUnit, north, ChangeOperation.Create, new { nameAr = "الشمال", nameEn = "North", typeCode = type, parentCode = root }),
            new Change(AppsControlChangeTypes.OrgUnit, south, ChangeOperation.Create, new { nameAr = "الجنوب", nameEn = "South", typeCode = type, parentCode = root }));
        await host.Host.PublishAsync(
            AppsControlAreas.Security,
            Employee(org.Director, root, "MANAGER", []),
            Employee(org.NorthManager, north, "MANAGER", []),
            Employee(org.NorthStaff, north, null, [org.Analyst]),
            Employee(org.SouthStaff, south, null, [org.Analyst]));
        return org;
    }

    private static Change Employee(string user, string unit, string? position, string[] roles) =>
        new(AppsControlChangeTypes.User, user, ChangeOperation.Create, new
        {
            nameAr = "موظف " + user,
            nameEn = "Employee " + user,
            assignments = new[] { new { orgUnitCode = unit, positionCode = position } },
            roles = roles.Select(r => new { roleCode = r, companyCode = "*", branches = new[] { "*" } }).ToArray(),
        });

    [Fact]
    public async Task A_rule_picks_the_branch_and_approval_goes_to_its_manager_or_the_one_above()
    {
        await using var host = new WorkflowHost(fixture);
        var org = await OrgAsync(host);
        var workflow = await host.WorkflowAsync(
        [
            Start("INTAKE"),
            Human("INTAKE", new { target = "ORG_UNIT", unitFrom = "RULE", unitRule = new JsonObject { ["var"] = "data.branch" }, role = org.Analyst, mode = "AUTO" }, [Act("SEND", "APPROVE")]),
            Human("APPROVE", new { target = "ORG_UNIT", unitFrom = "ACTOR", step = "INTAKE", position = "MANAGER", climbWhenEmpty = true, mode = "AUTO" }, [Act("OK", "FINAL")]),
            Human("FINAL", new { target = "ORG_UNIT", unitFrom = "ACTOR", levelsUp = 1, position = "MANAGER", mode = "QUEUE" }, [Act("OK", "END")]),
            End("END", "APPROVED"),
        ]);

        // North: the branch's analyst, then the branch manager, then the department's manager (one level up).
        var north = await host.StartAsync(workflow, new JsonObject { ["branch"] = org.North });
        var intake = (await host.OpenTasksAsync(north)).Single();
        intake.Assignee.ShouldBe(org.NorthStaff);
        intake.CandidateOrgUnit.ShouldBe(org.North);
        await host.AsAsync(org.NorthStaff, c => c.ActAsync(intake.Id, new ActRequest("SEND", null), CancellationToken.None));
        var approve = (await host.OpenTasksAsync(north)).Single();
        approve.Assignee.ShouldBe(org.NorthManager, "the manager of the unit the analyst works in");
        await host.AsAsync(org.NorthManager, c => c.ActAsync(approve.Id, new ActRequest("OK", null), CancellationToken.None));
        var final = (await host.OpenTasksAsync(north)).Single();
        final.CandidateOrgUnit.ShouldBe(org.Root, "one level above the approver's unit");
        final.Assignee.ShouldBeNull();
        (await host.AsAsync(org.Director, c => c.InboxAsync(InboxBox.Queue, null, 0, 50, CancellationToken.None))).Items.ShouldContain(t => t.Id == final.Id);
        (await host.AsAsync(org.NorthManager, c => c.InboxAsync(InboxBox.Queue, null, 0, 50, CancellationToken.None))).Items.ShouldNotContain(t => t.Id == final.Id);
        await Should.ThrowAsync<WorkflowForbiddenException>(() => host.AsAsync(org.NorthStaff, c => c.ClaimAsync(final.Id, CancellationToken.None)));
        await host.AsAsync(org.Director, c => c.ClaimAsync(final.Id, CancellationToken.None));

        // South: no branch manager, so approval climbs to the department's manager.
        var south = await host.StartAsync(workflow, new JsonObject { ["branch"] = org.South });
        var southIntake = (await host.OpenTasksAsync(south)).Single();
        southIntake.Assignee.ShouldBe(org.SouthStaff);
        await host.AsAsync(org.SouthStaff, c => c.ActAsync(southIntake.Id, new ActRequest("SEND", null), CancellationToken.None));
        var southApprove = (await host.OpenTasksAsync(south)).Single();
        southApprove.CandidateOrgUnit.ShouldBe(org.Root);
        southApprove.Assignee.ShouldBe(org.Director);

        // An unknown branch leaves the task to the role alone.
        var unknown = await host.StartAsync(workflow, new JsonObject { ["branch"] = "NOWHERE" });
        var unknownIntake = (await host.OpenTasksAsync(unknown)).Single();
        unknownIntake.CandidateOrgUnits.ShouldBeEmpty();
        unknownIntake.CandidateRole.ShouldBe(org.Analyst);
    }

    [Fact]
    public async Task A_unit_with_its_sub_units_offers_the_task_to_everyone_below()
    {
        await using var host = new WorkflowHost(fixture);
        var org = await OrgAsync(host);
        var workflow = await host.WorkflowAsync(
        [
            Start("POOL"),
            Human("POOL", new { target = "ORG_UNIT", unitFrom = "FIXED", orgUnit = org.Root, includeSubUnits = true, role = org.Analyst, mode = "QUEUE" }, [Act("OK", "END")]),
            End("END", "APPROVED"),
        ]);

        var subject = await host.StartAsync(workflow);
        var task = (await host.OpenTasksAsync(subject)).Single();
        task.CandidateOrgUnits.ShouldBe([org.Root, org.North, org.South], ignoreOrder: true);
        foreach (var analyst in new[] { org.NorthStaff, org.SouthStaff })
        {
            (await host.AsAsync(analyst, c => c.InboxAsync(InboxBox.Queue, null, 0, 50, CancellationToken.None))).Items.ShouldContain(t => t.Id == task.Id);
        }

        (await host.AsAsync(org.Director, c => c.InboxAsync(InboxBox.Queue, null, 0, 50, CancellationToken.None))).Items.ShouldNotContain(t => t.Id == task.Id, "not an analyst");
    }

    [Fact]
    public async Task Org_unit_assignments_say_where_the_unit_comes_from()
    {
        await using var host = new WorkflowHost(fixture);
        var code = await host.ServiceAsync();
        var ex = await Should.ThrowAsync<ChangeValidationException>(() => host.Host.PublishAsync(
            WorkflowChangeTypes.Area,
            new Change(WorkflowChangeTypes.Version, code + "/2", ChangeOperation.Create, new
            {
                nameAr = "2",
                nameEn = "2",
                definition = new
                {
                    nodes = new object[]
                    {
                        Start("A"),
                        Human("A", new { target = "ORG_UNIT", mode = "QUEUE" }, [Act("OK", "B")]),
                        Human("B", new { target = "ORG_UNIT", unitFrom = "FIXED", mode = "QUEUE" }, [Act("OK", "C")]),
                        Human("C", new { target = "ORG_UNIT", unitFrom = "ACTOR", step = "END", levelsUp = 20, mode = "QUEUE" }, [Act("OK", "END")]),
                        End("END", "APPROVED"),
                    },
                },
            })));

        ex.Errors.Keys.ShouldContain("definition.nodes[1].assignment.unitFrom");
        ex.Errors.Keys.ShouldContain("definition.nodes[2].assignment.orgUnit");
        ex.Errors.Keys.ShouldContain("definition.nodes[3].assignment.step");
        ex.Errors.Keys.ShouldContain("definition.nodes[3].assignment.levelsUp");
    }
}
