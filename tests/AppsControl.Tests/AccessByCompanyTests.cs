using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Tests.Infrastructure;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.AppsControl.Tests;

/// <summary>A user's access (ADR-0036): roles by company and branches, department default roles, personal privileges.</summary>
public sealed class AccessByCompanyTests(AppsControlFixture fixture) : IAsyncLifetime
{
    private Harness _harness = null!;

    public ValueTask InitializeAsync()
    {
        _harness = new Harness(fixture);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _harness.DisposeAsync();

    private static Change Role(string code, string resource, params string[] actions) =>
        new(AppsControlChangeTypes.Role, code, ChangeOperation.Create, new { nameAr = "دور", nameEn = "Role " + code, privileges = new[] { new { resourceCode = resource, actions } } });

    [Fact]
    public async Task Access_comes_from_roles_in_a_company_the_department_and_personal_privileges()
    {
        var (company, riyadh, jeddah) = (Harness.Unique("CO"), Harness.Unique("RUH"), Harness.Unique("JED"));
        var (clerk, departmentRole, unitType, unit) = (Harness.Unique("CLERK"), Harness.Unique("DEPT"), Harness.Unique("TYPE"), Harness.Unique("UNIT"));
        var user = Harness.Unique("staff").ToLowerInvariant();
        await _harness.PublishAsync(AppsControlAreas.System,
            new Change(AppsControlChangeTypes.Company, company, ChangeOperation.Create, new { nameAr = "شركة", nameEn = "Company" }),
            new Change(AppsControlChangeTypes.Branch, riyadh, ChangeOperation.Create, new { nameAr = "فرع", nameEn = "Riyadh", companyCode = company }),
            new Change(AppsControlChangeTypes.Branch, jeddah, ChangeOperation.Create, new { nameAr = "فرع", nameEn = "Jeddah", companyCode = company }));
        await _harness.PublishAsync(AppsControlAreas.Security, Role(clerk, AppsControlResources.Classes, "VIEW"), Role(departmentRole, AppsControlResources.Lovs, "VIEW"));
        await _harness.PublishAsync(AppsControlAreas.Organization,
            new Change(AppsControlChangeTypes.OrgUnitType, unitType, ChangeOperation.Create, new { nameAr = "نوع", nameEn = "Type" }),
            new Change(AppsControlChangeTypes.OrgUnit, unit, ChangeOperation.Create, new { nameAr = "إدارة", nameEn = "Unit", typeCode = unitType, defaultRoles = new[] { departmentRole } }));
        await _harness.PublishAsync(AppsControlAreas.Security, new Change(AppsControlChangeTypes.User, user, ChangeOperation.Create, new
        {
            nameAr = "موظف",
            nameEn = "Staff",
            assignments = new[] { new { orgUnitCode = unit, isPrimary = true } },
            roles = new[] { new { roleCode = clerk, companyCode = company, branches = new[] { riyadh } } },
            privileges = new[] { new { companyCode = "*", resourceCode = AppsControlResources.Menus, actions = new[] { "VIEW", "EDIT" } } },
        }));

        var access = await _harness.AccessAsync(user, company);
        access.Can(AppsControlResources.Classes, PrivilegeActions.View).ShouldBeTrue("the role held in the company");
        access.Can(AppsControlResources.Lovs, PrivilegeActions.View).ShouldBeTrue("the department's default role");
        access.Can(AppsControlResources.Menus, PrivilegeActions.Edit).ShouldBeTrue("a personal privilege");
        access.User.Roles.ShouldBe([clerk, departmentRole], ignoreOrder: true);

        var staff = await _harness.AsAsync("admin", sp => sp.GetRequiredService<IStaffDirectory>().SearchAsync(new StaffQuery(Role: departmentRole), CancellationToken.None));
        staff.Single().UserName.ShouldBe(user, "staff with a department role are found for tasks of that role");

        var row = await _harness.AsAsync("admin", sp => sp.GetServices<DynamicCube.Platform.Configuration.IConfigApplier>()
            .Single(a => a.EntityType == AppsControlChangeTypes.User).GetAsync(user, CancellationToken.None));
        var roleRow = row!.Value.GetProperty("roles")[0];
        roleRow.GetProperty("companyCode").GetString().ShouldBe(company);
        roleRow.GetProperty("branches")[0].GetString().ShouldBe(riyadh);

        // Leaving the department takes its role away.
        await _harness.PublishAsync(AppsControlAreas.Security, new Change(AppsControlChangeTypes.User, user, ChangeOperation.Update, new
        {
            nameAr = "موظف",
            nameEn = "Staff",
            roles = new[] { new { roleCode = clerk, companyCode = company, branches = new[] { riyadh } } },
        }));
        (await _harness.AccessAsync(user)).Can(AppsControlResources.Lovs, PrivilegeActions.View).ShouldBeFalse();

        // A department's default role cannot be deleted.
        await Should.ThrowAsync<ChangeSetException>(() => _harness.PublishAsync(AppsControlAreas.Security, new Change(AppsControlChangeTypes.Role, departmentRole, ChangeOperation.Delete, null)));
    }

    [Fact]
    public async Task All_is_an_explicit_choice_branches_need_their_company_and_personal_actions_must_exist_on_the_screen()
    {
        var (company, other, branch) = (Harness.Unique("CO"), Harness.Unique("CO2"), Harness.Unique("BR"));
        var role = Harness.Unique("ROLE");
        await _harness.PublishAsync(AppsControlAreas.System,
            new Change(AppsControlChangeTypes.Company, company, ChangeOperation.Create, new { nameAr = "شركة", nameEn = "Company" }),
            new Change(AppsControlChangeTypes.Company, other, ChangeOperation.Create, new { nameAr = "شركة", nameEn = "Other" }),
            new Change(AppsControlChangeTypes.Branch, branch, ChangeOperation.Create, new { nameAr = "فرع", nameEn = "Branch", companyCode = company }));
        await _harness.PublishAsync(AppsControlAreas.Security, Role(role, AppsControlResources.Classes, "VIEW"));

        async Task<IEnumerable<string>> Problems(object payload) =>
            (await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(AppsControlAreas.Security,
                new Change(AppsControlChangeTypes.User, Harness.Unique("u").ToLowerInvariant(), ChangeOperation.Create, payload)))).Errors.Keys;
        object Held(string? companyCode, string[] branches) => new { nameAr = "م", nameEn = "U", roles = new[] { new { roleCode = role, companyCode, branches } } };

        (await Problems(Held(other, [branch]))).ShouldContain("roles[0].branches", "the branch belongs to another company");
        (await Problems(Held(null, ["*"]))).ShouldContain("roles[0].companyCode", "no company is not all companies");
        (await Problems(Held(company, []))).ShouldContain("roles[0].branches", "no branches is not all branches");
        (await Problems(Held("*", [branch]))).ShouldContain("roles[0].branches", "all companies come with all branches");
        (await Problems(Held(Harness.Unique("NONE"), ["*"]))).ShouldContain("roles[0].companyCode");
        (await Problems(new { nameAr = "م", nameEn = "U", privileges = new[] { new { companyCode = "*", resourceCode = AppsControlResources.Root, actions = new[] { "DELETE" } } } })).ShouldContain("privileges[0].actions");
        (await Problems(new { nameAr = "م", nameEn = "U", privileges = new[] { new { companyCode = (string?)null, resourceCode = AppsControlResources.Classes, actions = new[] { "VIEW" } } } })).ShouldContain("privileges[0].companyCode");

        // Stored as chosen: "*" stays "*".
        var user = Harness.Unique("u").ToLowerInvariant();
        await _harness.PublishAsync(AppsControlAreas.Security, new Change(AppsControlChangeTypes.User, user, ChangeOperation.Create, Held("*", ["*"])));
        var row = await _harness.AsAsync("admin", sp => sp.GetServices<DynamicCube.Platform.Configuration.IConfigApplier>()
            .Single(a => a.EntityType == AppsControlChangeTypes.User).GetAsync(user, CancellationToken.None));
        var held = row!.Value.GetProperty("roles")[0];
        held.GetProperty("companyCode").GetString().ShouldBe("*");
        held.GetProperty("branches").EnumerateArray().Select(b => b.GetString()).ShouldBe(["*"]);
        (await _harness.AccessAsync(user, company)).Can(AppsControlResources.Classes, PrivilegeActions.View).ShouldBeTrue();
    }

    [Fact]
    public async Task Access_applies_for_the_company_the_person_works_in()
    {
        var (first, second, branch, unitType, unit) = (Harness.Unique("CO1"), Harness.Unique("CO2"), Harness.Unique("BR"), Harness.Unique("TYPE"), Harness.Unique("UNIT"));
        var (clerk, everywhere, department) = (Harness.Unique("CLERK"), Harness.Unique("ALLCO"), Harness.Unique("DEPT"));
        var user = Harness.Unique("staff").ToLowerInvariant();
        await _harness.PublishAsync(AppsControlAreas.System,
            new Change(AppsControlChangeTypes.Company, first, ChangeOperation.Create, new { nameAr = "شركة ١", nameEn = "First" }),
            new Change(AppsControlChangeTypes.Company, second, ChangeOperation.Create, new { nameAr = "شركة ٢", nameEn = "Second" }));
        await _harness.PublishAsync(AppsControlAreas.Security,
            Role(clerk, AppsControlResources.Classes, "VIEW"),
            Role(department, AppsControlResources.Roles, "VIEW"),
            new Change(AppsControlChangeTypes.Role, everywhere, ChangeOperation.Create, new
            {
                nameAr = "دور",
                nameEn = "Everywhere",
                privileges = new[] { new { resourceCode = AppsControlResources.Lovs, actions = new[] { "VIEW" } } },
                overrides = new[] { new { companyCode = second, privileges = new[] { new { resourceCode = AppsControlResources.Lovs, actions = new[] { "VIEW", "EDIT" } } } } },
            }));
        await _harness.PublishAsync(AppsControlAreas.Organization,
            new Change(AppsControlChangeTypes.OrgUnitType, unitType, ChangeOperation.Create, new { nameAr = "نوع", nameEn = "Type" }),
            new Change(AppsControlChangeTypes.OrgUnit, unit, ChangeOperation.Create, new { nameAr = "فرع", nameEn = "Branch unit", typeCode = unitType, defaultRoles = new[] { department } }));
        await _harness.PublishAsync(AppsControlAreas.System,
            new Change(AppsControlChangeTypes.Branch, branch, ChangeOperation.Create, new { nameAr = "فرع", nameEn = "Branch", companyCode = second, orgUnitCode = unit }));
        await _harness.PublishAsync(AppsControlAreas.Security, new Change(AppsControlChangeTypes.User, user, ChangeOperation.Create, new
        {
            nameAr = "موظف",
            nameEn = "Staff",
            assignments = new[] { new { orgUnitCode = unit, isPrimary = true } },
            roles = new object[]
            {
                new { roleCode = clerk, companyCode = first, branches = new[] { "*" } },
                new { roleCode = everywhere, companyCode = "*", branches = new[] { "*" } },
            },
            privileges = new[] { new { companyCode = second, resourceCode = AppsControlResources.Menus, actions = new[] { "VIEW" } } },
        }));

        var inFirst = await _harness.AccessAsync(user, first);
        inFirst.Company.ShouldBe(first);
        inFirst.Companies.ShouldContain(first);
        inFirst.Companies.ShouldContain(second, "a role held in every company opens every company");
        inFirst.Can(AppsControlResources.Classes, PrivilegeActions.View).ShouldBeTrue("the role held in this company");
        inFirst.Can(AppsControlResources.Lovs, PrivilegeActions.View).ShouldBeTrue("the role held in every company");
        inFirst.Can(AppsControlResources.Lovs, PrivilegeActions.Edit).ShouldBeFalse("the role's own contents here");
        inFirst.Can(AppsControlResources.Menus, PrivilegeActions.View).ShouldBeFalse("a personal privilege of the other company");
        inFirst.Can(AppsControlResources.Roles, PrivilegeActions.View).ShouldBeFalse("the department serves a branch of the other company");
        inFirst.User.ToJson()["company"]!.GetValue<string>().ShouldBe(first);

        var inSecond = await _harness.AccessAsync(user, second);
        inSecond.Company.ShouldBe(second);
        inSecond.Can(AppsControlResources.Classes, PrivilegeActions.View).ShouldBeFalse("held in the other company only");
        inSecond.Can(AppsControlResources.Lovs, PrivilegeActions.Edit).ShouldBeTrue("the role's version for this company");
        inSecond.Can(AppsControlResources.Menus, PrivilegeActions.View).ShouldBeTrue("the personal privilege of this company");
        inSecond.Can(AppsControlResources.Roles, PrivilegeActions.View).ShouldBeTrue("the department's default role, in its branch's company");

        // A company the person may not work in is ignored; with none chosen yet they are asked to choose.
        var unknown = await _harness.AccessAsync(user, "NO_SUCH_COMPANY");
        unknown.Companies.ShouldContain(unknown.Company!);
        unknown.CompanyChosen.ShouldBeFalse();

        // The last choice applies when the request names no company.
        await _harness.AsAsync("admin", async sp =>
        {
            var db = sp.GetRequiredService<AppsControl.Persistence.AppsControlDbContext>();
            await db.Users.Where(u => u.Code == user).ExecuteUpdateAsync(u => u.SetProperty(x => x.LastCompanyCode, second));
            sp.GetRequiredService<AppsControl.Access.AccessCache>().Invalidate();
        });
        var remembered = await _harness.AccessAsync(user, null);
        (remembered.Company, remembered.CompanyChosen).ShouldBe((second, true));
    }

    [Fact]
    public void The_current_company_is_the_requested_one_else_the_last_choice_else_the_first()
    {
        string[] companies = ["A", "B"];
        AppsControl.Access.AccessResolver.CurrentCompany(companies, "B", "A").ShouldBe(("B", true));
        AppsControl.Access.AccessResolver.CurrentCompany(companies, "X", "B").ShouldBe(("B", true));
        AppsControl.Access.AccessResolver.CurrentCompany(companies, null, null).ShouldBe(("A", false));
        AppsControl.Access.AccessResolver.CurrentCompany(["A"], null, null).ShouldBe(("A", true));
        AppsControl.Access.AccessResolver.CurrentCompany([], "A", null).ShouldBe((null, true));
    }

    [Fact]
    public async Task A_person_can_lose_an_action_their_roles_give_and_personal_privileges_hold_in_their_period()
    {
        var role = Harness.Unique("EDITOR");
        var user = Harness.Unique("u").ToLowerInvariant();
        await _harness.PublishAsync(AppsControlAreas.Security, Role(role, AppsControlResources.Classes, "VIEW", "EDIT"));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        object Payload(object[] privileges) => new
        {
            nameAr = "م",
            nameEn = "U",
            roles = new[] { new { roleCode = role, companyCode = "*", branches = new[] { "*" } } },
            privileges,
        };

        await _harness.PublishAsync(AppsControlAreas.Security, new Change(AppsControlChangeTypes.User, user, ChangeOperation.Create, Payload(
        [
            new { companyCode = "*", resourceCode = AppsControlResources.Classes, actions = Array.Empty<string>(), deniedActions = new[] { "EDIT" }, effectiveFrom = (DateOnly?)null, effectiveTo = (DateOnly?)null },
            new { companyCode = "*", resourceCode = AppsControlResources.Menus, actions = new[] { "VIEW" }, deniedActions = Array.Empty<string>(), effectiveFrom = (DateOnly?)today.AddDays(5), effectiveTo = (DateOnly?)null },
            new { companyCode = "*", resourceCode = AppsControlResources.Lovs, actions = new[] { "VIEW" }, deniedActions = Array.Empty<string>(), effectiveFrom = (DateOnly?)today.AddDays(-5), effectiveTo = (DateOnly?)today.AddDays(-1) },
        ])));

        var access = await _harness.AccessAsync(user);
        access.Can(AppsControlResources.Classes, PrivilegeActions.View).ShouldBeTrue("the role's other actions stay");
        access.Can(AppsControlResources.Classes, PrivilegeActions.Edit).ShouldBeFalse("taken away from this person");
        access.Can(AppsControlResources.Menus, PrivilegeActions.View).ShouldBeFalse("starts in five days");
        access.Can(AppsControlResources.Lovs, PrivilegeActions.View).ShouldBeFalse("ended yesterday");

        (await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(AppsControlAreas.Security, new Change(AppsControlChangeTypes.User, user, ChangeOperation.Update, Payload(
        [
            new { companyCode = "*", resourceCode = AppsControlResources.Classes, actions = new[] { "EDIT" }, deniedActions = new[] { "EDIT" }, effectiveFrom = (DateOnly?)today, effectiveTo = (DateOnly?)today.AddDays(-1) },
        ]))))).Errors.Keys.ShouldBe(["privileges[0].deniedActions", "privileges[0].effectiveTo"], ignoreOrder: true);
    }
}
