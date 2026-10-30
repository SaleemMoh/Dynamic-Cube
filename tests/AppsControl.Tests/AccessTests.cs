using DynamicCube.Modules.AppsControl.Access;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Modules.AppsControl.Tests.Infrastructure;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.AppsControl.Tests;

public sealed class AccessTests(AppsControlFixture fixture) : IAsyncLifetime
{
    private Harness _harness = null!;

    public ValueTask InitializeAsync()
    {
        _harness = new Harness(fixture);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _harness.DisposeAsync();

    [Fact]
    public async Task First_sign_in_creates_the_account_and_bootstrap_roles_grant_privileges()
    {
        var access = await _harness.AccessAsync("admin");

        access.User.Roles.ShouldContain("SYSTEM_ADMIN");
        access.Can(AppsControlResources.Classes, PrivilegeActions.Edit).ShouldBeTrue();
        access.Can(AppsControlResources.ChangeSets, PrivilegeActions.Approve).ShouldBeTrue();

        var account = await _harness.AsAsync("admin", sp => sp.GetRequiredService<AppsControlDbContext>().Users.SingleAsync(u => u.Code == "admin"));
        account.IdpSubject.ShouldBe("sub-admin");
    }

    [Fact]
    public async Task A_known_username_with_a_new_subject_is_refused_unless_relinking_is_on()
    {
        var userName = Harness.Unique("moved").ToLowerInvariant();
        await _harness.AccessAsync(userName);
        await _harness.AsAsync(userName, sp => sp.GetRequiredService<AppsControlDbContext>().Users
            .Where(u => u.Code == userName).ExecuteUpdateAsync(u => u.SetProperty(x => x.IdpSubject, "subject-before-reimport")));

        // Fresh service providers: the first sign-in's access snapshot is cached per subject.
        await using var refusing = new Harness(fixture);
        await Should.ThrowAsync<IdentityMismatchException>(() => refusing.AccessAsync(userName));

        await using var relinking = new Harness(fixture, new Dictionary<string, string?> { ["AppsControl:RelinkAccountsByUserName"] = "true" });
        await relinking.AccessAsync(userName);
        var account = await relinking.AsAsync(userName, sp => sp.GetRequiredService<AppsControlDbContext>().Users.SingleAsync(u => u.Code == userName));
        account.IdpSubject.ShouldBe($"sub-{userName}");
    }

    [Fact]
    public async Task Users_without_roles_get_no_privileges_and_an_empty_menu()
    {
        var userName = Harness.Unique("nobody").ToLowerInvariant();
        var menu = await _harness.AsAsync(userName, async sp =>
        {
            var access = await sp.GetRequiredService<AccessResolver>().GetAsync(CancellationToken.None);
            access.Privileges.ShouldBeEmpty();
            return await sp.GetRequiredService<MenuService>().GetMenuAsync("INTERNAL", access, CancellationToken.None);
        });

        menu.ShouldBeEmpty();
    }

    [Fact]
    public async Task Menu_shows_the_items_the_user_may_view()
    {
        var menu = await _harness.AsAsync("checker", async sp =>
            await sp.GetRequiredService<MenuService>().GetMenuAsync(
                "INTERNAL",
                await sp.GetRequiredService<AccessResolver>().GetAsync(CancellationToken.None),
                CancellationToken.None));

        var group = menu.ShouldHaveSingleItem();
        group.Code.ShouldBe("APPS_CONTROL");
        group.Items.Select(i => i.Code).ShouldContain("apps-control.change-sets");
        group.Items.Select(i => i.Code).ShouldContain("apps-control.classes");
    }

    [Fact]
    public async Task Maker_cannot_approve_own_change_set()
    {
        var code = Harness.Unique("TEXT");
        var id = await _harness.DraftAsync(AppsControlAreas.Lookups, LookupTests.ClassChange(code, []));
        await _harness.AsAsync("maker", sp => sp.GetRequiredService<ChangeSetService<AppsControlDbContext>>().SubmitAsync(id, CancellationToken.None));

        await Should.ThrowAsync<ChangeSetException>(() => _harness.AsAsync("maker", sp =>
            sp.GetRequiredService<ChangeSetService<AppsControlDbContext>>().ApproveAsync(id, null, CancellationToken.None)));
    }

    [Fact]
    public async Task Pre_created_users_are_linked_on_first_sign_in_and_receive_their_roles()
    {
        var userName = Harness.Unique("analyst").ToLowerInvariant();
        var role = Harness.Unique("ANALYST");
        await _harness.PublishAsync(
            AppsControlAreas.Security,
            new Change(AppsControlChangeTypes.Role, role, ChangeOperation.Create, new
            {
                nameAr = "محلل",
                nameEn = "Analyst",
                privileges = new[] { new { resourceCode = AppsControlResources.Classes, actions = new[] { "VIEW" } } },
            }),
            new Change(AppsControlChangeTypes.User, userName, ChangeOperation.Create, new
            {
                nameAr = "محلل",
                nameEn = "Analyst",
                roles = new[] { new { roleCode = role, companyCode = "*", branches = new[] { "*" } } },
            }));

        var access = await _harness.AccessAsync(userName);

        access.User.Roles.ShouldBe([role]);
        access.Can(AppsControlResources.Classes, PrivilegeActions.View).ShouldBeTrue();
        access.Can(AppsControlResources.Classes, PrivilegeActions.Edit).ShouldBeFalse();
    }

    [Fact]
    public async Task A_menu_item_assigned_to_a_role_shows_in_the_menu_and_opens_its_screen()
    {
        var userName = Harness.Unique("clerk").ToLowerInvariant();
        var role = Harness.Unique("CLERK");
        await _harness.PublishAsync(
            AppsControlAreas.Security,
            new Change(AppsControlChangeTypes.Role, role, ChangeOperation.Create, new { nameAr = "كاتب", nameEn = "Clerk", menus = new[] { "apps-control.lovs" } }),
            new Change(AppsControlChangeTypes.User, userName, ChangeOperation.Create, new { nameAr = "كاتب", nameEn = "Clerk", roles = new[] { new { roleCode = role, companyCode = "*", branches = new[] { "*" } } } }));

        var (access, menu) = await _harness.AsAsync(userName, async sp =>
        {
            var snapshot = await sp.GetRequiredService<AccessResolver>().GetAsync(CancellationToken.None);
            return (snapshot, await sp.GetRequiredService<MenuService>().GetMenuAsync("INTERNAL", snapshot, CancellationToken.None));
        });

        access.Can(AppsControlResources.Lovs, PrivilegeActions.View).ShouldBeTrue("the item opens its screen");
        access.Can(AppsControlResources.Lovs, PrivilegeActions.Edit).ShouldBeFalse();
        menu.ShouldHaveSingleItem().Items.Select(i => i.Code).ShouldBe(["apps-control.lovs"]);

        var payload = await _harness.AsAsync("admin", sp => sp.GetServices<IConfigApplier>().Single(a => a.EntityType == AppsControlChangeTypes.Role).GetAsync(role, CancellationToken.None));
        payload!.Value.GetProperty("menus").EnumerateArray().Select(m => m.GetString()).ShouldBe(["apps-control.lovs"]);

        var ex = await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(
            AppsControlAreas.Security,
            new Change(AppsControlChangeTypes.Role, Harness.Unique("ROLE"), ChangeOperation.Create, new { nameAr = "دور", nameEn = "Role", menus = new[] { "NO_SUCH_MENU" } })));
        ex.Errors.Keys.ShouldContain("menus[0]");
    }

    [Fact]
    public async Task Privileges_must_use_actions_the_resource_offers()
    {
        var ex = await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(
            AppsControlAreas.Security,
            new Change(AppsControlChangeTypes.Role, Harness.Unique("ROLE"), ChangeOperation.Create, new
            {
                nameAr = "دور",
                nameEn = "Role",
                privileges = new[] { new { resourceCode = AppsControlResources.Root, actions = new[] { "DELETE" } } },
            })));

        ex.Errors.Keys.ShouldContain("privileges[0].actions");
    }

    [Fact]
    public async Task Moving_an_org_unit_moves_its_subtree_and_user_context_sees_sub_units()
    {
        var sector = Harness.Unique("SEC");
        var department = Harness.Unique("DEP");
        var section = Harness.Unique("SECT");
        var other = Harness.Unique("OTHER");
        var userName = Harness.Unique("manager").ToLowerInvariant();
        await _harness.PublishAsync(
            AppsControlAreas.Organization,
            new Change(AppsControlChangeTypes.OrgUnitType, sector, ChangeOperation.Create, new { nameAr = "قطاع", nameEn = "Sector" }),
            new Change(AppsControlChangeTypes.OrgUnit, department, ChangeOperation.Create, new { nameAr = "إدارة", nameEn = "Department", typeCode = sector, attributes = new { REGION = "RIYADH" } }),
            new Change(AppsControlChangeTypes.OrgUnit, section, ChangeOperation.Create, new { nameAr = "قسم", nameEn = "Section", typeCode = sector, parentCode = department }),
            new Change(AppsControlChangeTypes.OrgUnit, other, ChangeOperation.Create, new { nameAr = "أخرى", nameEn = "Other", typeCode = sector }));
        await _harness.PublishAsync(
            AppsControlAreas.Security,
            new Change(AppsControlChangeTypes.User, userName, ChangeOperation.Create, new
            {
                nameAr = "مدير",
                nameEn = "Manager",
                assignments = new[] { new { orgUnitCode = department, positionCode = "MANAGER" } },
            }));

        var access = await _harness.AccessAsync(userName);
        access.User.PrimaryOrgUnit.ShouldBe(department);
        access.User.Positions.ShouldBe(["MANAGER"]);
        access.User.SubtreeOrgUnits.ShouldBe([department, section], ignoreOrder: true);
        access.User.OrgUnitAttributes["REGION"]!.GetValue<string>().ShouldBe("RIYADH");

        await _harness.PublishAsync(
            AppsControlAreas.Organization,
            new Change(AppsControlChangeTypes.OrgUnit, department, ChangeOperation.Update, new { nameAr = "إدارة", nameEn = "Department", typeCode = sector, parentCode = other }));

        var paths = await _harness.AsAsync("admin", sp => sp.GetRequiredService<AppsControlDbContext>().OrgUnits
            .Where(u => u.Code == other || u.Code == department || u.Code == section)
            .ToDictionaryAsync(u => u.Code, u => u.Path.ToString()));
        paths[section].ShouldStartWith(paths[department] + ".");
        paths[department].ShouldStartWith(paths[other] + ".");

        await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(
            AppsControlAreas.Organization,
            new Change(AppsControlChangeTypes.OrgUnit, other, ChangeOperation.Update, new { nameAr = "أخرى", nameEn = "Other", typeCode = sector, parentCode = section })));
    }
}
