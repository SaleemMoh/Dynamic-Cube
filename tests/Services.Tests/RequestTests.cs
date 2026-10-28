using System.Text.Json.Nodes;
using DynamicCube.Modules.Parties.Services;
using DynamicCube.Modules.Services.Changes;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Persistence;
using DynamicCube.Modules.Services.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Parties;
using DynamicCube.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Services.Tests;

public sealed class RequestTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly ServiceSamples _sample = new();
    private Applicant _applicant = null!;

    public async ValueTask InitializeAsync()
    {
        _applicant = await Applicant.CreateAsync(fixture);
        await _applicant.Host.PublishAsync(ServiceChangeTypes.Area, _sample.All());
    }

    public async ValueTask DisposeAsync() => await _applicant.DisposeAsync();

    private Task<T> Requests<T>(Func<RequestService, Task<T>> action, bool self = false) =>
        _applicant.AsAsync(sp => action(sp.GetRequiredService<RequestService>()), self);

    private static JsonObject Answers(params (string Code, JsonNode? Value)[] answers) => new(answers.Select(a => KeyValuePair.Create(a.Code, a.Value)));

    [Fact]
    public async Task Catalog_shows_eligibility_for_the_acting_party()
    {
        var asEstablishment = await Requests(s => s.CatalogAsync(CancellationToken.None));
        var asSelf = await Requests(s => s.CatalogAsync(CancellationToken.None), self: true);

        asEstablishment.Single(e => e.Code == _sample.Service).IsEligible.ShouldBeTrue();
        var reasons = asSelf.Single(e => e.Code == _sample.Service).Reasons;
        reasons.Select(r => r.Code).ShouldBe(["PARTY_KIND"]);
    }

    [Fact]
    public async Task A_draft_is_prefilled_saved_and_submitted_with_fees_and_a_number()
    {
        var draft = await Requests(s => s.CreateAsync(new CreateRequest(_sample.Service), CancellationToken.None));
        draft.Data[_sample.CrNumber]!.GetValue<string>().ShouldBe(_applicant.CrNumber);
        draft.Request.Status.ShouldBe(StatusCategory.Draft);
        draft.Request.Number.ShouldBeNull();

        // Step check: sectors are required; the read-only CR number comes from the party even when the answer is cleared.
        var empty = await Requests(s => s.SaveAsync(draft.Request.Id, new SaveRequest(Answers(), "PREMIUM", "SECTORS"), CancellationToken.None));
        empty.StepErrors.Select(e => (e.Field, e.Code)).ShouldBe([(_sample.Sectors, "REQUIRED")]);

        // ICT only: the construction section and its answers drop out; the shared employee question is asked once.
        var answers = Answers(
            (_sample.Sectors, new JsonArray("ICT")),
            (_sample.CrNumber, "9999999999"),
            (_sample.IctCategory, "GOLD"),
            (_sample.Employees, 25),
            (_sample.BuildingProjects, 3),
            (_sample.Revenue, 20_000_000),
            ("NOT_A_QUESTION", "x"));
        var saved = await Requests(s => s.SaveAsync(draft.Request.Id, new SaveRequest(answers, "PREMIUM", "DETAILS"), CancellationToken.None));
        saved.StepErrors.ShouldBeEmpty();
        saved.Data.ContainsKey("NOT_A_QUESTION").ShouldBeFalse();
        saved.Fees.Single().Total.ShouldBe(5000m);

        var missingDeclaration = await Should.ThrowAsync<RequestValidationException>(() => Requests(s => s.SubmitAsync(draft.Request.Id, new SubmitRequest(false), CancellationToken.None)));
        missingDeclaration.Errors.Single().Field.ShouldBe("declaration");

        var submitted = await Requests(s => s.SubmitAsync(draft.Request.Id, new SubmitRequest(true), CancellationToken.None));

        submitted.Request.Status.ShouldBe(StatusCategory.Submitted);
        submitted.Request.Number!.ShouldMatch(@"^CLS-\d{4}-\d{6}$");
        submitted.Data.ContainsKey(_sample.BuildingProjects).ShouldBeFalse();
        submitted.Data[_sample.CrNumber]!.GetValue<string>().ShouldBe(_applicant.CrNumber);
        var fee = submitted.Fees.Single();
        (fee.Net, fee.Vat, fee.Total, fee.IsEstimate).ShouldBe((4347.83m, 652.17m, 5000m, false));
        submitted.Request.FeeTotal.ShouldBe(5000m);
        await Should.ThrowAsync<RequestConflictException>(() => Requests(s => s.SaveAsync(draft.Request.Id, new SaveRequest(answers, null, null), CancellationToken.None)));

        var detail = await _applicant.Host.AsAsync("admin", sp => sp.GetRequiredService<RequestAdminService>().GetAsync(draft.Request.Id, CancellationToken.None));
        detail.Timeline.Select(t => t.Type).ShouldBe(["CREATED", "SUBMITTED"]);
        detail.SubmitterPartyId.ShouldBe(_applicant.IndividualId);
        detail.BeneficiaryPartyId.ShouldBe(_applicant.EstablishmentId);
    }

    [Fact]
    public async Task Answers_are_checked_on_the_server()
    {
        var draft = await Requests(s => s.CreateAsync(new CreateRequest(_sample.Service), CancellationToken.None));
        await Requests(s => s.SaveAsync(draft.Request.Id, new SaveRequest(Answers(
            (_sample.Sectors, new JsonArray("BUILDING", "SPACE")),
            (_sample.Employees, 0),
            (_sample.BuildingProjects, 3),
            (_sample.Revenue, "lots")), null, null), CancellationToken.None));

        var invalid = await Should.ThrowAsync<RequestValidationException>(() => Requests(s => s.SubmitAsync(draft.Request.Id, new SubmitRequest(true), CancellationToken.None)));

        invalid.Errors.Select(e => (e.Field, e.Code)).ShouldBe(
            [(_sample.Sectors, "OPTION"), (_sample.Employees, "MIN"), (_sample.Revenue, "NUMBER"), ("level", "REQUIRED")],
            ignoreOrder: true);

        await Requests(s => s.SaveAsync(draft.Request.Id, new SaveRequest(Answers(
            (_sample.Sectors, new JsonArray("BUILDING")),
            (_sample.Employees, 1),
            (_sample.BuildingProjects, 30),
            (_sample.Revenue, 100)), "REGULAR", null), CancellationToken.None));
        var crossField = await Should.ThrowAsync<RequestValidationException>(() => Requests(s => s.SubmitAsync(draft.Request.Id, new SubmitRequest(true), CancellationToken.None)));
        crossField.Errors.Single().Code.ShouldBe("PROJECTS_FOR_EMPLOYEES");
        crossField.Errors.Single().MessageEn.ShouldBe("Too many projects for the number of employees.");
    }

    [Fact]
    public async Task A_sanction_stops_new_requests()
    {
        var draft = await Requests(s => s.CreateAsync(new CreateRequest(_sample.Service), CancellationToken.None));
        await _applicant.Host.AsAsync("admin", sp => sp.GetRequiredService<PartyAdminService>().AddSanctionAsync(
            _applicant.EstablishmentId, new AddSanctionRequest("APPLICATION_BAN", "مخالفة", null, null), CancellationToken.None));

        var blocked = await Should.ThrowAsync<NotEligibleException>(() => Requests(s => s.SubmitAsync(draft.Request.Id, new SubmitRequest(true), CancellationToken.None)));
        var created = await Should.ThrowAsync<NotEligibleException>(() => Requests(s => s.CreateAsync(new CreateRequest(_sample.Service), CancellationToken.None)));

        blocked.Reasons.Select(r => r.Code).ShouldBe(["SANCTION", "NOT_BANNED"]);
        created.Reasons.Select(r => r.Code).ShouldContain("SANCTION");
    }

    [Fact]
    public async Task Requests_belong_to_the_acting_party()
    {
        var draft = await Requests(s => s.CreateAsync(new CreateRequest(_sample.Service), CancellationToken.None));

        var mine = await Requests(s => s.ListMineAsync(null, CancellationToken.None));
        var asSelf = await Requests(s => s.ListMineAsync(null, CancellationToken.None), self: true);

        mine.Select(r => r.Id).ShouldContain(draft.Request.Id);
        asSelf.ShouldBeEmpty();
        await Should.ThrowAsync<RequestNotFoundException>(() => Requests(s => s.GetAsync(draft.Request.Id, CancellationToken.None), self: true));
    }

    [Fact]
    public async Task Expired_drafts_are_closed()
    {
        var draft = await Requests(s => s.CreateAsync(new CreateRequest(_sample.Service), CancellationToken.None));
        await _applicant.Host.AsAsync("admin", sp => sp.GetRequiredService<ServicesDbContext>().Requests.Where(r => r.Id == draft.Request.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.DraftExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1))));

        var view = await Requests(s => s.GetAsync(draft.Request.Id, CancellationToken.None));

        view.Request.Status.ShouldBe(StatusCategory.Cancelled);
        view.Request.DisplayStatus.ShouldBe("EXPIRED");
    }
}

public sealed class VersionPolicyTests(DatabaseFixture fixture)
{
    [Theory]
    [InlineData(NewVersionPolicy.Continue, 1)]
    [InlineData(NewVersionPolicy.Restart, 2)]
    public async Task Drafts_follow_the_new_version_policy(NewVersionPolicy policy, int expectedVersion)
    {
        await using var applicant = await Applicant.CreateAsync(fixture);
        var sample = new ServiceSamples();
        var service = sample.ServiceChange();
        ((ServicePayload)service.Payload!).OnNewVersion = policy;
        await applicant.Host.PublishAsync(ServiceChangeTypes.Area, [.. sample.Questions(), service, sample.Version(1)]);
        var draft = await applicant.AsAsync(sp => sp.GetRequiredService<RequestService>().CreateAsync(new CreateRequest(sample.Service), CancellationToken.None));
        await applicant.AsAsync(sp => sp.GetRequiredService<RequestService>().SaveAsync(draft.Request.Id,
            new SaveRequest(new JsonObject { [sample.Revenue] = 5, [sample.Employees] = 3 }, null, null), CancellationToken.None));

        var v2 = sample.Definition();
        v2.Steps[2].Sections.RemoveAt(2);
        await applicant.Host.PublishAsync(ServiceChangeTypes.Area, sample.Version(2, v2));
        var view = await applicant.AsAsync(sp => sp.GetRequiredService<RequestService>().GetAsync(draft.Request.Id, CancellationToken.None));

        view.VersionNumber.ShouldBe(expectedVersion);
        view.Data[sample.Employees]!.GetValue<int>().ShouldBe(3);
        view.Data.ContainsKey(sample.Revenue).ShouldBe(expectedVersion == 1);
    }

    [Fact]
    public async Task Delegations_limited_to_other_services_do_not_cover_this_one()
    {
        await using var applicant = await Applicant.CreateAsync(fixture, "DELEGATE", ["SOME_OTHER_SERVICE"]);
        var sample = new ServiceSamples();
        await applicant.Host.PublishAsync(ServiceChangeTypes.Area, sample.All());

        var error = await Should.ThrowAsync<NotEligibleException>(() =>
            applicant.AsAsync(sp => sp.GetRequiredService<RequestService>().CreateAsync(new CreateRequest(sample.Service), CancellationToken.None)));

        error.Reasons.Select(r => r.Code).ShouldBe(["NOT_DELEGATED"]);
    }
}
