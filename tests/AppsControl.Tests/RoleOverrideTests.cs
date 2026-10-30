using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Tests.Infrastructure;
using DynamicCube.Platform.ChangeSets;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.AppsControl.Tests;

/// <summary>A role that differs in one company (ADR-0036 step 4): holders in that company get the company's version.</summary>
public sealed class RoleOverrideTests(AppsControlFixture fixture) : IAsyncLifetime
{
    private Harness _harness = null!;

    public ValueTask InitializeAsync()
    {
        _harness = new Harness(fixture);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _harness.DisposeAsync();

    [Fact]
    public async Task Holders_of_a_role_in_a_company_where_it_differs_get_that_companys_version()
    {
        var (main, sub, role) = (Harness.Unique("CO"), Harness.Unique("CO"), Harness.Unique("CLERK"));
        var (inMain, inSub, everywhere) = (Harness.Unique("main").ToLowerInvariant(), Harness.Unique("sub").ToLowerInvariant(), Harness.Unique("all").ToLowerInvariant());
        await _harness.PublishAsync(AppsControlAreas.System,
            new Change(AppsControlChangeTypes.Company, main, ChangeOperation.Create, new { nameAr = "شركة", nameEn = "Main" }),
            new Change(AppsControlChangeTypes.Company, sub, ChangeOperation.Create, new { nameAr = "شركة", nameEn = "Subsidiary" }));
        await _harness.PublishAsync(AppsControlAreas.Security, new Change(AppsControlChangeTypes.Role, role, ChangeOperation.Create, new
        {
            nameAr = "موظف",
            nameEn = "Clerk",
            privileges = new[] { new { resourceCode = AppsControlResources.Classes, actions = new[] { "VIEW", "EDIT" } } },
            overrides = new[]
            {
                new { companyCode = main, privileges = new[] { new { resourceCode = AppsControlResources.Lovs, actions = new[] { "VIEW" } } } },
            },
        }));
        Change User(string code, string? company) => new(AppsControlChangeTypes.User, code, ChangeOperation.Create, new
        {
            nameAr = "موظف",
            nameEn = "Staff",
            roles = new[] { new { roleCode = role, companyCode = company } },
        });
        await _harness.PublishAsync(AppsControlAreas.Security, User(inMain, main), User(inSub, sub), User(everywhere, null));

        var main1 = await _harness.AccessAsync(inMain);
        main1.Can(AppsControlResources.Lovs, PrivilegeActions.View).ShouldBeTrue("the role's version in its company");
        main1.Can(AppsControlResources.Classes, PrivilegeActions.View).ShouldBeFalse("the override replaces the role's own screens");
        var sub1 = await _harness.AccessAsync(inSub);
        sub1.Can(AppsControlResources.Classes, PrivilegeActions.Edit).ShouldBeTrue("other companies keep the role as it is");
        sub1.Can(AppsControlResources.Lovs, PrivilegeActions.View).ShouldBeFalse();
        (await _harness.AccessAsync(everywhere)).Can(AppsControlResources.Classes, PrivilegeActions.View).ShouldBeTrue("held in every company: the role as it is");

        var row = await _harness.AsAsync("admin", sp => sp.GetServices<DynamicCube.Platform.Configuration.IConfigApplier>()
            .Single(a => a.EntityType == AppsControlChangeTypes.Role).GetAsync(role, CancellationToken.None));
        var payload = System.Text.Json.JsonSerializer.SerializeToElement(row);
        payload.GetProperty("overrides")[0].GetProperty("companyCode").GetString().ShouldBe(main);
        payload.GetProperty("privileges")[0].GetProperty("resourceCode").GetString().ShouldBe(AppsControlResources.Classes);

        async Task<IEnumerable<string>> Problems(object overrides) =>
            (await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(AppsControlAreas.Security,
                new Change(AppsControlChangeTypes.Role, role, ChangeOperation.Update, new { nameAr = "موظف", nameEn = "Clerk", overrides })))).Errors.Keys;
        (await Problems(new[] { new { companyCode = Harness.Unique("NONE") } })).ShouldContain("overrides[0].companyCode");
        (await Problems(new[] { new { companyCode = sub }, new { companyCode = sub } })).ShouldContain("overrides");
        (await Problems(new[] { new { companyCode = sub, privileges = new[] { new { resourceCode = AppsControlResources.Lovs, actions = new[] { "FLY" } } } } }))
            .ShouldContain("overrides[0].privileges[0].actions");

        await Should.ThrowAsync<ChangeSetException>(() => _harness.PublishAsync(AppsControlAreas.System,
            new Change(AppsControlChangeTypes.Company, main, ChangeOperation.Delete, null)));

        // Without the override the company's holders get the role as it is again.
        await _harness.PublishAsync(AppsControlAreas.Security, new Change(AppsControlChangeTypes.Role, role, ChangeOperation.Update, new
        {
            nameAr = "موظف",
            nameEn = "Clerk",
            privileges = new[] { new { resourceCode = AppsControlResources.Classes, actions = new[] { "VIEW", "EDIT" } } },
        }));
        (await _harness.AccessAsync(inMain)).Can(AppsControlResources.Classes, PrivilegeActions.View).ShouldBeTrue();
    }
}
