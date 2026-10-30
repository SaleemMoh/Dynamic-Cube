using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Tests.Infrastructure;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Security;
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
            privileges = new[] { new { resourceCode = AppsControlResources.Menus, actions = new[] { "VIEW", "EDIT" } } },
        }));

        var access = await _harness.AccessAsync(user);
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
    public async Task Branches_need_their_company_and_personal_actions_must_exist_on_the_screen()
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

        (await Problems(new { nameAr = "م", nameEn = "U", roles = new[] { new { roleCode = role, companyCode = other, branches = new[] { branch } } } })).ShouldContain("roles[0].branches");
        (await Problems(new { nameAr = "م", nameEn = "U", roles = new[] { new { roleCode = role, companyCode = (string?)null, branches = new[] { branch } } } })).ShouldContain("roles[0].branches");
        (await Problems(new { nameAr = "م", nameEn = "U", roles = new[] { new { roleCode = role, companyCode = Harness.Unique("NONE"), branches = Array.Empty<string>() } } })).ShouldContain("roles[0].companyCode");
        (await Problems(new { nameAr = "م", nameEn = "U", privileges = new[] { new { resourceCode = AppsControlResources.Root, actions = new[] { "DELETE" } } } })).ShouldContain("privileges[0].actions");
    }
}
