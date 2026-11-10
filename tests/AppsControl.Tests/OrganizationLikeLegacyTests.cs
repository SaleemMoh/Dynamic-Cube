using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Modules.AppsControl.Tests.Infrastructure;
using DynamicCube.Platform.ChangeSets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.AppsControl.Tests;

/// <summary>
/// Organizations, companies and users kept like the legacy Apps Control: a parent organization (REF_INSTID), and the
/// organizations and companies a person may enter with their defaults (SYS_USERINSTANCES, SYS_USERINSTANCES_COMPANIES).
/// </summary>
public sealed class OrganizationLikeLegacyTests(AppsControlFixture fixture) : IAsyncLifetime
{
    private Harness _harness = null!;

    public ValueTask InitializeAsync()
    {
        _harness = new Harness(fixture);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _harness.DisposeAsync();

    private static Change Organization(string code, string? parent, ChangeOperation operation = ChangeOperation.Create) =>
        new(AppsControlChangeTypes.Entity, code, operation, new { nameAr = "جهة", nameEn = "Organization " + code, parentCode = parent, shortCode = "S" + code[..3] });

    [Fact]
    public async Task An_organization_belongs_to_a_parent_but_never_below_itself()
    {
        var (group, member) = (Harness.Unique("GRP"), Harness.Unique("ORG"));
        await _harness.PublishAsync(AppsControlAreas.System, Organization(group, null), Organization(member, group));

        var stored = await _harness.AsAsync("admin", sp => sp.GetRequiredService<AppsControlDbContext>().Entities.AsNoTracking().SingleAsync(e => e.Code == member));
        (stored.ParentCode, stored.ShortCode).ShouldBe((group, "S" + member[..3]));

        var ex = await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(AppsControlAreas.System, Organization(group, member, ChangeOperation.Update)));
        ex.Errors.Keys.ShouldContain("parentCode");
    }

    [Fact]
    public async Task A_person_enters_the_companies_they_are_admitted_to_and_starts_in_the_default_one()
    {
        var (first, second) = ("ENTITY/" + Harness.Unique("CO"), "ENTITY/" + Harness.Unique("CO"));
        var user = Harness.Unique("adm").ToLowerInvariant();
        await _harness.PublishAsync(AppsControlAreas.System,
            new Change(AppsControlChangeTypes.Company, first, ChangeOperation.Create, new { nameAr = "شركة", nameEn = "First", abbreviation = "FST", defaultBusinessUnit = "BU1" }),
            new Change(AppsControlChangeTypes.Company, second, ChangeOperation.Create, new { nameAr = "شركة", nameEn = "Second" }));
        await _harness.PublishAsync(AppsControlAreas.Security, new Change(AppsControlChangeTypes.User, user, ChangeOperation.Create, new
        {
            nameAr = "موظف",
            nameEn = "Staff",
            companies = new[] { new { companyCode = first, isDefault = false }, new { companyCode = second, isDefault = true } },
        }));

        var access = await _harness.AccessAsync(user, null);
        access.Companies.ShouldBe([first, second], ignoreOrder: true);
        (access.Company, access.CompanyChosen).ShouldBe((second, true), "the default company, as legacy DEFAULT_CMP_FLAG");

        await _harness.AsAsync("admin", async sp =>
        {
            var db = sp.GetRequiredService<AppsControlDbContext>();
            var stored = await db.Users.AsNoTracking().Include(u => u.Instances).SingleAsync(u => u.Code == user);
            stored.Instances.Select(i => i.EntityCode).ShouldBe(["ENTITY"], "a company admits its organization");
            var company = await db.Companies.AsNoTracking().SingleAsync(c => c.EntityCode + "/" + c.Code == first);
            (company.Abbreviation, company.DefaultBusinessUnit).ShouldBe(("FST", "BU1"));
        });

        // One default company per organization.
        var ex = await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(AppsControlAreas.Security, new Change(AppsControlChangeTypes.User, user, ChangeOperation.Update, new
        {
            nameAr = "موظف",
            nameEn = "Staff",
            companies = new[] { new { companyCode = first, isDefault = true }, new { companyCode = second, isDefault = true } },
        })));
        ex.Errors.Keys.ShouldContain("companies");
    }

    [Fact]
    public async Task Users_are_counted_per_role_of_each_company()
    {
        var (first, second) = ("ENTITY/" + Harness.Unique("CO"), "ENTITY/" + Harness.Unique("CO"));
        var (code, user) = (Harness.Unique("SALEEM"), Harness.Unique("u").ToLowerInvariant());
        await _harness.PublishAsync(AppsControlAreas.System,
            new Change(AppsControlChangeTypes.Company, first, ChangeOperation.Create, new { nameAr = "شركة", nameEn = "First" }),
            new Change(AppsControlChangeTypes.Company, second, ChangeOperation.Create, new { nameAr = "شركة", nameEn = "Second" }));
        await _harness.PublishAsync(AppsControlAreas.Security,
            new Change(AppsControlChangeTypes.Role, $"{first}/{code}", ChangeOperation.Create, new { nameAr = "دور", nameEn = "Role" }),
            new Change(AppsControlChangeTypes.Role, $"{second}/{code}", ChangeOperation.Create, new { nameAr = "دور", nameEn = "Role" }));
        await _harness.PublishAsync(AppsControlAreas.Security, new Change(AppsControlChangeTypes.User, user, ChangeOperation.Create, new
        {
            nameAr = "موظف",
            nameEn = "Staff",
            roles = new[] { new { roleCode = $"{first}/{code}", companyCode = first, branches = new[] { "*" } } },
        }));

        var counts = await _harness.AsAsync("admin", sp => Endpoints.AdminCounts.UsersPerRoleAsync(sp.GetRequiredService<AppsControlDbContext>(), CancellationToken.None));
        counts[$"{first}/{code}"].ShouldBe(1, "counted under the role's own key, as the roles list names it");
        counts.ShouldNotContainKey($"{second}/{code}");
    }
}
