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
    public async Task An_app_is_a_dynamic_cube_app_on_channels_or_a_connected_app_enabled_for_companies_of_its_organization()
    {
        var (organization, company, branch) = (Harness.Unique("ORG"), Harness.Unique("CO"), Harness.Unique("BR"));
        await _harness.PublishAsync(
            AppsControlAreas.System,
            new Change(AppsControlChangeTypes.Entity, organization, ChangeOperation.Create, new { nameAr = "جهة", nameEn = "Group" }),
            Company(company, organization),
            Branch(branch, company));
        Change App(string code, object payload) => new(AppsControlChangeTypes.BusinessApp, code, ChangeOperation.Create, payload);
        async Task<IEnumerable<string>> Problems(object payload) =>
            (await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(AppsControlAreas.System, App(Harness.Unique("APP"), payload)))).Errors.Keys;

        (await Problems(new { nameAr = "ت", nameEn = "A", kind = "DYNAMIC_CUBE", channels = Array.Empty<string>() })).ShouldContain("channels");
        (await Problems(new { nameAr = "ت", nameEn = "A", kind = "DYNAMIC_CUBE", channels = new[] { "MOBILE" } })).ShouldContain("channels");
        (await Problems(new { nameAr = "ت", nameEn = "A", kind = "CONNECTED", channels = new[] { "INTERNAL" } })).ShouldContain("channels");
        (await Problems(new { nameAr = "ت", nameEn = "A", kind = "DYNAMIC_CUBE", channels = new[] { "INTERNAL" }, companies = new[] { company }, branches = new[] { "*" } }))
            .ShouldContain("companies", "the company belongs to another organization than the default one");
        var unchosen = await Problems(new { nameAr = "ت", nameEn = "A", kind = "DYNAMIC_CUBE", channels = new[] { "INTERNAL" } });
        unchosen.ShouldContain("companies", "all companies is an explicit choice, not an empty list");
        unchosen.ShouldContain("branches");

        var app = Harness.Unique("APP");
        await _harness.PublishAsync(AppsControlAreas.System, App(app, new
        {
            nameAr = "ت",
            nameEn = "A",
            entityCode = organization,
            kind = "DYNAMIC_CUBE",
            channels = new[] { "INTERNAL", "EXTERNAL" },
            companies = new[] { company },
            branches = new[] { branch },
        }));
        var row = (await ListAsync(AppsControlChangeTypes.BusinessApp)).Single(a => a.GetProperty("code").GetString() == app);
        row.GetProperty("channels").EnumerateArray().Select(c => c.GetString()).ShouldBe(["INTERNAL", "EXTERNAL"]);
        row.GetProperty("companies").EnumerateArray().Single().GetString().ShouldBe(company);
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
