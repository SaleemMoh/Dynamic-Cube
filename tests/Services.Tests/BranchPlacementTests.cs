using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.Services.Changes;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Parties;
using DynamicCube.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Services.Tests;

/// <summary>
/// Requests belong to the service's company and to the branch an answer names (ADR-0036 step 6); staff see them only in
/// the branches their roles cover.
/// </summary>
public sealed class BranchPlacementTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task A_request_goes_to_the_branch_its_answer_names_and_only_that_branchs_staff_see_it()
    {
        await using var applicant = await Applicant.CreateAsync(fixture);
        var host = applicant.Host;
        var (company, west, east) = (TestHost.Unique("CO"), TestHost.Unique("WEST"), TestHost.Unique("EAST"));
        var (region, service, role) = (TestHost.Unique("CITY"), TestHost.Unique("BRANCHED"), TestHost.Unique("CLERK"));
        await host.PublishAsync(AppsControlAreas.System,
            new Change(AppsControlChangeTypes.Company, company, ChangeOperation.Create, new { nameAr = "شركة", nameEn = "Company", displayOrder = 999 }),
            new Change(AppsControlChangeTypes.Branch, west, ChangeOperation.Create, new { nameAr = "الغربية", nameEn = "West", companyCode = company, city = "Jeddah" }),
            new Change(AppsControlChangeTypes.Branch, east, ChangeOperation.Create, new { nameAr = "الشرقية", nameEn = "East", companyCode = company, city = "Dammam", isDefault = true }));
        await host.PublishAsync(AppsControlAreas.Security, new Change(AppsControlChangeTypes.Role, role, ChangeOperation.Create, new { nameAr = "موظف", nameEn = "Clerk" }));
        var (westClerk, eastClerk) = (TestHost.Unique("w").ToLowerInvariant(), TestHost.Unique("e").ToLowerInvariant());
        await host.PublishAsync(AppsControlAreas.Security, Employee(westClerk, role, company, west), Employee(eastClerk, role, company, east));

        await host.PublishAsync(
            ServiceChangeTypes.Area,
            new Change(ServiceChangeTypes.Question, region, ChangeOperation.Create, new QuestionPayload
            {
                NameAr = "المدينة",
                NameEn = "City",
                Type = QuestionType.Select,
                Options = [new() { Value = "JEDDAH", LabelAr = "جدة", LabelEn = "Jeddah" }, new() { Value = "DAMMAM", LabelAr = "الدمام", LabelEn = "Dammam" }],
            }),
            new Change(ServiceChangeTypes.Service, service, ChangeOperation.Create, new ServicePayload
            {
                NameAr = "خدمة بفروع",
                NameEn = "Branched service",
                Archetype = "ATTESTATION",
                PartyKinds = [PartyKind.Establishment],
                CompanyCode = company,
                BranchQuestion = region,
            }),
            new Change(ServiceChangeTypes.Version, service + "/1", ChangeOperation.Create, new ServiceVersionPayload
            {
                NameAr = "الإصدار 1",
                NameEn = "Version 1",
                Definition = new VersionDefinition
                {
                    Steps =
                    [
                        new()
                        {
                            Code = "DETAILS",
                            Type = StepType.Form,
                            TitleAr = "البيانات",
                            TitleEn = "Details",
                            Sections = [new() { Code = "MAIN", TitleAr = "البيانات", TitleEn = "Details", Questions = [new() { Code = region, Required = true }] }],
                        },
                    ],
                },
            }));

        // Before the answer: the company's default branch; the answer names the city of the other branch.
        var draft = await applicant.AsAsync(sp => sp.GetRequiredService<RequestService>().CreateAsync(new CreateRequest(service), CancellationToken.None));
        (draft.Request.CompanyCode, draft.Request.BranchCode).ShouldBe((company, east));
        var saved = await applicant.AsAsync(sp => sp.GetRequiredService<RequestService>().SaveAsync(draft.Request.Id, new SaveRequest(new JsonObject { [region] = "JEDDAH" }, null, "DETAILS", "DETAILS"), CancellationToken.None));
        saved.Request.BranchCode.ShouldBe(west);
        var submitted = await applicant.AsAsync(sp => sp.GetRequiredService<RequestService>().SubmitAsync(draft.Request.Id, new SubmitRequest(true), CancellationToken.None));
        (submitted.Request.CompanyCode, submitted.Request.BranchCode).ShouldBe((company, west));

        // Staff see the request in the branch it belongs to only.
        Task<bool> Sees(string user) => host.AsAsync(user, async sp =>
            (await sp.GetRequiredService<RequestAdminService>().SearchAsync(service, null, null, 0, 50, CancellationToken.None)).Items.Any(r => r.Id == draft.Request.Id));
        (await Sees(westClerk)).ShouldBeTrue("the role held in the request's branch");
        (await Sees(eastClerk)).ShouldBeFalse("the role held in another branch");
        await Should.ThrowAsync<RequestNotFoundException>(() => host.AsAsync(eastClerk, sp => sp.GetRequiredService<RequestAdminService>().GetAsync(draft.Request.Id, CancellationToken.None)));

        // An unknown company is refused on the service.
        var refused = await Should.ThrowAsync<ChangeValidationException>(() => host.PublishAsync(ServiceChangeTypes.Area, new Change(ServiceChangeTypes.Service, TestHost.Unique("BAD"), ChangeOperation.Create, new ServicePayload
        {
            NameAr = "خدمة",
            NameEn = "Service",
            PartyKinds = [PartyKind.Establishment],
            CompanyCode = "NO_SUCH_COMPANY",
        })));
        refused.Errors.Keys.ShouldContain("companyCode");
    }

    private static Change Employee(string user, string role, string company, string branch) =>
        new(AppsControlChangeTypes.User, user, ChangeOperation.Create, new
        {
            nameAr = "موظف",
            nameEn = "Employee " + user,
            roles = new[] { new { roleCode = role, companyCode = company, branches = new[] { branch } } },
        });
}
