using System.Net;
using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.Parties.Changes;
using DynamicCube.Modules.Parties.Domain;
using DynamicCube.Modules.Parties.Services;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Integration;
using DynamicCube.Platform.Parties;
using DynamicCube.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Parties.Tests;

public sealed class RegistryTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly TestApplicant _applicant = new() { NationalId = PartyTestSupport.NationalId(), Name = "Ahmed Khan" };
    private TestHost _host = null!;

    public ValueTask InitializeAsync()
    {
        _host = PartyTestSupport.Host(fixture, _applicant);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    private Task<T> AdminAsync<T>(Func<PartyAdminService, Task<T>> action, string user = "admin") =>
        _host.AsAsync(user, sp => action(sp.GetRequiredService<PartyAdminService>()));

    [Fact]
    public async Task Registers_establishments_with_valid_unique_identifiers()
    {
        var cr = PartyTestSupport.CrNumber();

        var id = await AdminAsync(s => s.RegisterAsync(new RegisterPartyRequest(PartyKind.Establishment, IdentifierTypes.CrNumber, cr, "مؤسسة الاختبار", "Test est."), CancellationToken.None));
        var invalid = await Should.ThrowAsync<ChangeValidationException>(() =>
            AdminAsync(s => s.RegisterAsync(new RegisterPartyRequest(PartyKind.Establishment, IdentifierTypes.NationalId, "123", "", "x"), CancellationToken.None)));
        await Should.ThrowAsync<PartyConflictException>(() =>
            AdminAsync(s => s.RegisterAsync(new RegisterPartyRequest(PartyKind.Establishment, IdentifierTypes.CrNumber, cr, "مكرر", "Duplicate"), CancellationToken.None)));
        await AdminAsync(async s =>
        {
            await s.AddIdentifierAsync(id, new AddIdentifierRequest(IdentifierTypes.UnifiedNumber, "7" + cr[1..]), CancellationToken.None);
            return true;
        });

        invalid.Errors.Keys.ShouldBe(["identifierType", "nameAr"], ignoreOrder: true);
        var found = await AdminAsync(s => s.SearchAsync("7" + cr[1..], null, 0, 10, CancellationToken.None));
        found.Items.Single().Id.ShouldBe(id);
        found.Items.Single().Identifier.ShouldBe(cr);
    }

    [Fact]
    public async Task Manual_delegations_give_contexts_limited_to_services()
    {
        var individual = await _host.AsAsync("applicant", sp => sp.GetRequiredService<ContextService>().CurrentIndividualAsync(CancellationToken.None));
        var establishment = await AdminAsync(s => s.RegisterAsync(new RegisterPartyRequest(PartyKind.Establishment, IdentifierTypes.CrNumber, PartyTestSupport.CrNumber(), "مكتب هندسي", "Engineering office"), CancellationToken.None));

        var relationship = await AdminAsync(s => s.AddRelationshipAsync(establishment, new AddRelationshipRequest(_applicant.NationalId!, "DELEGATE", ["CLASSIFICATION"], null, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))), CancellationToken.None));
        var contexts = await _host.AsAsync("applicant", sp => sp.GetRequiredService<ContextService>().ListAsync(individual.Id, CancellationToken.None));

        var context = contexts.Single(c => c.PartyId == establishment);
        context.RelationshipCode.ShouldBe("DELEGATE");
        context.Services.ShouldBe(["CLASSIFICATION"]);

        await AdminAsync(async s =>
        {
            await s.EndRelationshipAsync(establishment, relationship, CancellationToken.None);
            return true;
        });
        var after = await _host.AsAsync("applicant", sp => sp.GetRequiredService<ContextService>().ListAsync(individual.Id, CancellationToken.None));
        after.ShouldNotContain(c => c.PartyId == establishment);
    }

    [Fact]
    public async Task Staff_link_a_person_to_an_establishment_before_the_first_sign_in()
    {
        var cr = PartyTestSupport.CrNumber();
        var number = _applicant.NationalId!;
        var before = await AdminAsync(s => s.PersonLinksAsync(number, CancellationToken.None));

        // An establishment that is not registered needs its names.
        var unnamed = await Should.ThrowAsync<ChangeValidationException>(() =>
            AdminAsync(s => s.LinkEstablishmentAsync(number, new LinkEstablishmentRequest(cr, null, null, "MANAGER", null, null, null, "أحمد خان", "Ahmed Khan"), CancellationToken.None)));
        var link = await AdminAsync(s => s.LinkEstablishmentAsync(number, new LinkEstablishmentRequest(cr, "مؤسسة الربط", "Linked est.", "MANAGER", null, null, null, "أحمد خان", "Ahmed Khan"), CancellationToken.None));
        await Should.ThrowAsync<PartyConflictException>(() =>
            AdminAsync(s => s.LinkEstablishmentAsync(number, new LinkEstablishmentRequest(cr, null, null, "MANAGER", null, null, null, null, null), CancellationToken.None)));
        var after = await AdminAsync(s => s.PersonLinksAsync(number, CancellationToken.None));

        before.PartyId.ShouldBeNull();
        unnamed.Errors.Keys.ShouldBe(["establishmentNameAr", "establishmentNameEn"], ignoreOrder: true);
        after.Links.Single().PrincipalIdentifier.ShouldBe(cr);
        after.Links.Single().Source.ShouldBe(RecordSource.Manual);

        // Signing in finds the registered person, who may now act for the establishment.
        var individual = await _host.AsAsync("applicant", sp => sp.GetRequiredService<ContextService>().CurrentIndividualAsync(CancellationToken.None));
        individual.Id.ShouldBe(after.PartyId!.Value);
        var contexts = await _host.AsAsync("applicant", sp => sp.GetRequiredService<ContextService>().ListAsync(individual.Id, CancellationToken.None));
        contexts.ShouldContain(c => c.PartyId == after.Links[0].PrincipalId);

        await AdminAsync(async s =>
        {
            await s.EndPersonLinkAsync(number, link, CancellationToken.None);
            return true;
        });
        var ended = await _host.AsAsync("applicant", sp => sp.GetRequiredService<ContextService>().ListAsync(individual.Id, CancellationToken.None));
        ended.ShouldNotContain(c => c.PartyId == after.Links[0].PrincipalId);
    }

    [Fact]
    public async Task Sanctions_are_lifted_by_someone_else()
    {
        var party = await AdminAsync(s => s.RegisterAsync(new RegisterPartyRequest(PartyKind.Establishment, IdentifierTypes.CrNumber, PartyTestSupport.CrNumber(), "مؤسسة", "Est."), CancellationToken.None));

        var sanction = await AdminAsync(s => s.AddSanctionAsync(party, new AddSanctionRequest("APPLICATION_BAN", "مخالفة شروط التصنيف", null, null), CancellationToken.None));
        var detail = await AdminAsync(s => s.GetAsync(party, CancellationToken.None));
        await Should.ThrowAsync<PartyConflictException>(() => AdminAsync(async s =>
        {
            await s.LiftSanctionAsync(party, sanction, new LiftSanctionRequest("تم التصحيح"), CancellationToken.None);
            return true;
        }));
        await AdminAsync(
            async s =>
            {
                await s.LiftSanctionAsync(party, sanction, new LiftSanctionRequest("تم التصحيح"), CancellationToken.None);
                return true;
            },
            user: "checker");
        var lifted = await AdminAsync(s => s.GetAsync(party, CancellationToken.None));

        var active = detail.Sanctions.Single();
        active.IsActive.ShouldBeTrue();
        active.EndsOn.ShouldBe(active.StartsOn.AddDays(180));
        lifted.Sanctions.Single().IsActive.ShouldBeFalse();
        lifted.Sanctions.Single().LiftedBy.ShouldBe("sub-checker");
    }

    [Fact]
    public async Task Credentials_and_projects_are_recorded_with_their_data()
    {
        var credentialType = TestHost.Unique("DGA_TECH");
        // Credential types are values of class PARTY_CREDENTIAL_TYPE (Class/Domain); an expiry date is required by default.
        await _host.PublishAsync(AppsControlAreas.Lookups, new Change(PartyChangeTypes.CredentialType, credentialType, ChangeOperation.Create, new
        {
            nameAr = "شهادة التصنيف الفني",
            nameEn = "Technical classification certificate",
            partyKind = "ESTABLISHMENT",
            issuerAr = "هيئة الحكومة الرقمية",
            issuerEn = "Digital Government Authority",
        }));
        var party = await AdminAsync(s => s.RegisterAsync(new RegisterPartyRequest(PartyKind.Establishment, IdentifierTypes.CrNumber, PartyTestSupport.CrNumber(), "شركة تقنية", "Tech co."), CancellationToken.None));

        var data = JsonNode.Parse("""{ "levels": [{ "level": "A", "categories": [{ "code": "CLOUD", "activities": ["IAAS"] }] }] }""")!.AsObject();
        await AdminAsync(s => s.AddCredentialAsync(party, new AddCredentialRequest(credentialType, "DGA-1", new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), CredentialStatus.Active, data), CancellationToken.None));
        await AdminAsync(s => s.AddProjectAsync(party, new AddProjectRequest("C-1", "مشروع التحول الرقمي", null, "وزارة", 1_250_000m, new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31), ProjectStatus.Completed, null), CancellationToken.None));
        var noExpiry = await Should.ThrowAsync<ChangeValidationException>(() =>
            AdminAsync(s => s.AddCredentialAsync(party, new AddCredentialRequest(credentialType, "DGA-2", null, null, CredentialStatus.Active, null), CancellationToken.None)));

        var detail = await AdminAsync(s => s.GetAsync(party, CancellationToken.None));
        var credential = detail.Credentials.Single();
        credential.Status.ShouldBe(CredentialStatus.Expired);
        credential.Data!["levels"]![0]!["categories"]![0]!["code"]!.GetValue<string>().ShouldBe("CLOUD");
        detail.Projects.Single().ContractValue.ShouldBe(1_250_000m);
        noExpiry.Errors.Keys.ShouldBe(["expiresOn"]);
    }

    [Fact]
    public async Task Profile_fields_are_refreshed_from_integrations_when_stale()
    {
        var prefix = "/v1/" + TestHost.Unique("P").ToLowerInvariant();
        var operation = await MomrahSamples.PublishAsync(_host, retryCount: 0, operation: (c, e) => MomrahSamples.CrByIdNumber(c, e, path: prefix + "/{idNumber}"));
        var field = TestHost.Unique("CR_NUMBERS");
        var manual = TestHost.Unique("NOTES");
        await _host.PublishAsync(
            PartyChangeTypes.Area,
            new Change(PartyChangeTypes.ProfileField, field, ChangeOperation.Create, new ProfileFieldPayload
            {
                NameAr = "السجلات التجارية",
                NameEn = "Commercial registrations",
                PartyKind = PartyKind.Individual,
                DataType = ProfileDataType.Json,
                OperationCode = operation,
                OutputName = "crNumbers",
                Inputs = [new() { Parameter = "idNumber", IdentifierType = IdentifierTypes.NationalId }],
                RefreshHours = 24,
            }),
            new Change(PartyChangeTypes.ProfileField, manual, ChangeOperation.Create, new ProfileFieldPayload { NameAr = "ملاحظات", NameEn = "Notes", PartyKind = PartyKind.Individual }));
        PartyTestSupport.Answer(_host, prefix, _ => (HttpStatusCode.OK, MomrahSamples.CrInformation));
        var individual = await _host.AsAsync("applicant", sp => sp.GetRequiredService<ContextService>().CurrentIndividualAsync(CancellationToken.None));

        var refreshed = await _host.AsAsync("admin", sp => sp.GetRequiredService<ProfileService>().RefreshAsync(individual.Id, force: false, CancellationToken.None));
        var again = await _host.AsAsync("admin", sp => sp.GetRequiredService<ProfileService>().RefreshAsync(individual.Id, force: false, CancellationToken.None));
        await _host.AsAsync("admin", sp => sp.GetRequiredService<ProfileService>().SetAsync(individual.Id, manual, JsonValue.Create("VIP"), CancellationToken.None));
        await Should.ThrowAsync<PartyConflictException>(() => _host.AsAsync("admin", sp =>
            sp.GetRequiredService<ProfileService>().SetAsync(individual.Id, field, JsonValue.Create("x"), CancellationToken.None)));
        var profile = await _host.AsAsync("admin", sp => sp.GetRequiredService<ProfileService>().GetAsync(individual.Id, CancellationToken.None));

        refreshed.Single(r => r.OperationCode == operation).Outcome.ShouldBe(IntegrationOutcome.Success);
        again.ShouldNotContain(r => r.OperationCode == operation);
        _host.Provider.CountTo(prefix).ShouldBe(1);
        var value = profile.Single(p => p.Code == field);
        value.Value!.ToJsonString().ShouldBe("""["3350043331","1010012006"]""");
        value.Source.ShouldBe(RecordSource.Integration);
        value.IsStale.ShouldBeFalse();
        profile.Single(p => p.Code == manual).Value!.GetValue<string>().ShouldBe("VIP");
    }

    [Fact]
    public async Task Configuration_validation_catches_inconsistent_context_sources()
    {
        var errors = await Should.ThrowAsync<ChangeValidationException>(() => _host.PublishAsync(PartyChangeTypes.Area, new Change(PartyChangeTypes.ContextSource, TestHost.Unique("BAD"), ChangeOperation.Create, new ContextSourcePayload
        {
            NameAr = "س",
            NameEn = "x",
            OperationCode = "lower-case",
            InputParameter = "idNumber",
            ItemsPath = "Licenses",
            IdentifierPath = "a..b",
            NameArPath = "Commercial_Name",
            PrincipalIdentifierType = IdentifierTypes.NationalId,
            DefaultRelationshipCode = "NO_SUCH_TYPE",
        })));

        errors.Errors.Keys.ShouldBe(["operationCode", "identifierPath", "principalIdentifierType", "defaultRelationshipCode"], ignoreOrder: true);
    }
}
