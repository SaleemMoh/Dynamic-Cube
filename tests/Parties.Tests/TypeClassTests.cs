using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.Parties.Changes;
using DynamicCube.Modules.Parties.Domain;
using DynamicCube.Modules.Parties.Services;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Configuration;
using DynamicCube.Platform.DataDictionary;
using DynamicCube.Platform.Parties;
using DynamicCube.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Parties.Tests;

/// <summary>Relationship, credential and sanction types are values of Party Registry classes, edited in Classes &amp; domains.</summary>
public sealed class TypeClassTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private TestHost _host = null!;

    public ValueTask InitializeAsync()
    {
        _host = PartyTestSupport.Host(fixture, new TestApplicant { NationalId = PartyTestSupport.NationalId() });
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    private Task<Guid> PublishAsync(string classCode, string code, object? payload, ChangeOperation operation = ChangeOperation.Create) =>
        _host.PublishAsync(AppsControlAreas.DataDictionary, new Change(AppsControlChangeTypes.Domain, $"{classCode}/{code}", operation, payload));

    private Task<DictionaryValue?> FindAsync(string classCode, string code) =>
        _host.AsAsync("admin", sp => sp.GetRequiredService<IDataDictionaryReader>().FindAsync(classCode, code, CancellationToken.None));

    [Fact]
    public async Task The_product_starts_with_the_relationship_types_and_their_provider_codes()
    {
        var owner = await FindAsync(PartyClasses.RelationshipType, "OWNER");

        owner.ShouldNotBeNull();
        owner.Flag(PartyClasses.CanAct).ShouldBeTrue();
        PartyClasses.ProviderCodes(owner).ShouldBe([("MCI", "1")]);
        (await FindAsync(PartyClasses.SanctionType, "APPLICATION_BAN"))!.Whole(PartyClasses.DefaultDays).ShouldBe(180);
    }

    [Fact]
    public async Task A_sanction_type_is_a_class_value_with_its_defaults()
    {
        var code = TestHost.Unique("LATE_REPORT");
        await PublishAsync(PartyClasses.SanctionType, code, new { nameAr = "تأخر التقارير", nameEn = "Late reports", attributes = new Dictionary<string, object> { [PartyClasses.DefaultDays] = 30 } });

        var value = await FindAsync(PartyClasses.SanctionType, code);
        value.ShouldNotBeNull();
        value.Whole(PartyClasses.DefaultDays).ShouldBe(30);
        PartyClasses.Flag(value, PartyClasses.BlocksApplications, false).ShouldBeTrue("blocking is the default");

        // Classes & domains lists it with the default the module's rule filled in.
        var rows = await _host.AsAsync("admin", sp => sp.GetServices<IConfigApplier>().Single(a => a.EntityType == AppsControlChangeTypes.Domain).ListAsync(PartyClasses.SanctionType, CancellationToken.None));
        var listed = rows.Single(r => r.GetProperty("code").GetString() == $"{PartyClasses.SanctionType}/{code}");
        listed.GetProperty("attributes").GetProperty(PartyClasses.DefaultDays).GetDecimal().ShouldBe(30);
        listed.GetProperty("attributes").GetProperty(PartyClasses.BlocksApplications).GetBoolean().ShouldBeTrue();

        var invalid = await Should.ThrowAsync<ChangeValidationException>(() =>
            PublishAsync(PartyClasses.SanctionType, TestHost.Unique("BAD"), new { nameAr = "س", nameEn = "x", attributes = new Dictionary<string, object> { [PartyClasses.DefaultDays] = 0 } }));
        invalid.Errors.Keys.ShouldContain("attributes." + PartyClasses.DefaultDays);
    }

    [Fact]
    public async Task A_provider_code_translates_to_one_relationship_type_and_a_type_in_use_is_kept()
    {
        var clash = await Should.ThrowAsync<ChangeValidationException>(() => PublishAsync(
            PartyClasses.RelationshipType,
            TestHost.Unique("CO_OWNER"),
            new { nameAr = "مالك مشارك", nameEn = "Co-owner", attributes = new Dictionary<string, object> { [PartyClasses.CanAct] = true, [PartyClasses.ExternalCodes] = new[] { new { provider = "MCI", code = "1" } } } }));
        clash.Errors["attributes." + PartyClasses.ExternalCodes].Single().ShouldContain("OWNER");

        var code = TestHost.Unique("AGENT");
        await PublishAsync(PartyClasses.RelationshipType, code, new { nameAr = "وكيل", nameEn = "Agent", attributes = new Dictionary<string, object> { [PartyClasses.CanAct] = true, [PartyClasses.ExternalCodes] = new[] { new { provider = "MCI", code = "9" + code[^3..] } } } });
        var establishment = await _host.AsAsync("admin", sp => sp.GetRequiredService<PartyAdminService>().RegisterAsync(
            new RegisterPartyRequest(PartyKind.Establishment, IdentifierTypes.CrNumber, PartyTestSupport.CrNumber(), "مؤسسة", "Est."), CancellationToken.None));
        var applicant = await _host.AsAsync("applicant", sp => sp.GetRequiredService<ContextService>().CurrentIndividualAsync(CancellationToken.None));
        await _host.AsAsync("admin", sp => sp.GetRequiredService<PartyAdminService>().AddRelationshipAsync(
            establishment, new AddRelationshipRequest(applicant.PrimaryIdentifier.Value, code, null, null, null), CancellationToken.None));

        var detail = await _host.AsAsync("admin", sp => sp.GetRequiredService<PartyAdminService>().GetAsync(establishment, CancellationToken.None));
        detail.Relationships.Single().RelationshipNameEn.ShouldBe("Agent");
        detail.Relationships.Single().CanAct.ShouldBeTrue();
        var inUse = await Should.ThrowAsync<ChangeSetException>(() => PublishAsync(PartyClasses.RelationshipType, code, null, ChangeOperation.Delete));
        inUse.Message.ShouldContain("relationships");
        (await FindAsync(PartyClasses.RelationshipType, code)).ShouldNotBeNull();
    }
}
