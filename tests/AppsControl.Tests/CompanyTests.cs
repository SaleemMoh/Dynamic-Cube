using System.Text.Json;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Tests.Infrastructure;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.AppsControl.Tests;

/// <summary>Companies of an organization and their branches (ADR-0036).</summary>
public sealed class CompanyTests(AppsControlFixture fixture) : IAsyncLifetime
{
    private Harness _harness = null!;

    public ValueTask InitializeAsync()
    {
        _harness = new Harness(fixture);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _harness.DisposeAsync();

    private static Change Company(string code, string? organization = null) =>
        new(AppsControlChangeTypes.Company, code, ChangeOperation.Create, new { nameAr = "شركة", nameEn = "Company " + code, entityCode = organization, crNumber = "1010000000" });

    private static Change Branch(string code, string company, bool isDefault = false) =>
        new(AppsControlChangeTypes.Branch, code, ChangeOperation.Create, new { nameAr = "فرع", nameEn = "Branch " + code, companyCode = company, city = "Riyadh", isDefault });

    private Task<IReadOnlyList<JsonElement>> ListAsync(string type, string? parent = null) =>
        _harness.AsAsync("admin", sp => sp.GetServices<IConfigApplier>().Single(a => a.EntityType == type).ListAsync(parent, CancellationToken.None));

    [Fact]
    public async Task Branches_take_their_companys_organization_and_one_is_the_default()
    {
        var (organization, company) = (Harness.Unique("ORG"), Harness.Unique("CO"));
        var (riyadh, jeddah) = (Harness.Unique("RUH"), Harness.Unique("JED"));
        await _harness.PublishAsync(
            AppsControlAreas.System,
            new Change(AppsControlChangeTypes.Entity, organization, ChangeOperation.Create, new { nameAr = "جهة", nameEn = "Group" }),
            Company(company, organization),
            Branch(riyadh, company, isDefault: true));
        await _harness.PublishAsync(AppsControlAreas.System, Branch(jeddah, company, isDefault: true));

        var branches = await ListAsync(AppsControlChangeTypes.Branch, company);
        branches.Select(b => b.GetProperty("code").GetString()).ShouldBe([jeddah, riyadh], ignoreOrder: true);
        branches.ShouldAllBe(b => b.GetProperty("entityCode").GetString() == organization);
        branches.Single(b => b.GetProperty("isDefault").GetBoolean()).GetProperty("code").GetString().ShouldBe(jeddah, "setting a new default clears the old one");
        (await ListAsync(AppsControlChangeTypes.Company, organization)).Single().GetProperty("code").GetString().ShouldBe(company);
    }

    [Fact]
    public async Task A_branch_needs_an_existing_company_and_a_company_with_branches_cannot_be_deleted()
    {
        var (company, branch) = (Harness.Unique("CO"), Harness.Unique("BR"));
        (await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(AppsControlAreas.System, Branch(branch, Harness.Unique("NOPE")))))
            .Errors.Keys.ShouldContain("companyCode");

        await _harness.PublishAsync(AppsControlAreas.System, Company(company), Branch(branch, company));
        (await ListAsync(AppsControlChangeTypes.Company)).Single(c => c.GetProperty("code").GetString() == company)
            .GetProperty("entityCode").GetString().ShouldBe("ENTITY", "a company without an organization joins the default one");
        await Should.ThrowAsync<ChangeSetException>(() => _harness.PublishAsync(
            AppsControlAreas.System, new Change(AppsControlChangeTypes.Company, company, ChangeOperation.Delete, null)));
    }
}
