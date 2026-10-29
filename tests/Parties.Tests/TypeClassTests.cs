using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.Parties.Changes;
using DynamicCube.Modules.Parties.Domain;
using DynamicCube.Modules.Parties.Services;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Configuration;
using DynamicCube.Platform.Lookups;
using DynamicCube.Platform.Parties;
using DynamicCube.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Parties.Tests;

/// <summary>Relationship, credential and sanction types are values of Party Registry classes in Class/Domain, edited through their own screens.</summary>
public sealed class TypeClassTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private TestHost _host = null!;

    public ValueTask InitializeAsync()
    {
        _host = PartyTestSupport.Host(fixture, new TestApplicant { NationalId = PartyTestSupport.NationalId() });
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    private Task<Guid> PublishAsync(string type, string code, object? payload, ChangeOperation operation = ChangeOperation.Create) =>
        _host.PublishAsync(AppsControlAreas.Lookups, new Change(type, code, operation, payload));

    private Task<LookupValue?> FindAsync(string classCode, string code) =>
        _host.AsAsync("admin", sp => sp.GetRequiredService<ILookupDirectory>().FindAsync(classCode, code, CancellationToken.None));

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
    public async Task A_sanction_type_saved_on_its_screen_is_a_class_value_with_its_defaults()
    {
        var code = TestHost.Unique("LATE_REPORT");
        await PublishAsync(PartyChangeTypes.SanctionType, code, new { nameAr = "تأخر التقارير", nameEn = "Late reports", defaultDays = 30 });

        var value = await FindAsync(PartyClasses.SanctionType, code);
        value.ShouldNotBeNull();
        value.Whole(PartyClasses.DefaultDays).ShouldBe(30);
        PartyClasses.Flag(value, PartyClasses.BlocksApplications, false).ShouldBeTrue("blocking is the default");

        // The screen reads it back with its own field names.
        var row = await _host.AsAsync("admin", sp => sp.GetServices<IConfigApplier>().Single(a => a.EntityType == PartyChangeTypes.SanctionType).ListAsync(null, CancellationToken.None));
        var listed = row.Single(r => r.GetProperty("code").GetString() == code);
        listed.GetProperty("defaultDays").GetDecimal().ShouldBe(30);
        listed.GetProperty("blocksApplications").GetBoolean().ShouldBeTrue();

        var invalid = await Should.ThrowAsync<ChangeValidationException>(() =>
            PublishAsync(PartyChangeTypes.SanctionType, TestHost.Unique("BAD"), new { nameAr = "س", nameEn = "x", defaultDays = 0 }));
        invalid.Errors.Keys.ShouldContain("defaultDays");
    }

    [Fact]
    public async Task A_provider_code_translates_to_one_relationship_type_and_a_type_in_use_is_kept()
    {
        var clash = await Should.ThrowAsync<ChangeValidationException>(() => PublishAsync(
            PartyChangeTypes.RelationshipType,
            TestHost.Unique("CO_OWNER"),
            new { nameAr = "مالك مشارك", nameEn = "Co-owner", canAct = true, externalCodes = new[] { new { provider = "MCI", code = "1" } } }));
        clash.Errors["externalCodes"].Single().ShouldContain("OWNER");

        var code = TestHost.Unique("AGENT");
        await PublishAsync(PartyChangeTypes.RelationshipType, code, new { nameAr = "وكيل", nameEn = "Agent", canAct = true, externalCodes = new[] { new { provider = "MCI", code = "9" + code[^3..] } } });
        var establishment = await _host.AsAsync("admin", sp => sp.GetRequiredService<PartyAdminService>().RegisterAsync(
            new RegisterPartyRequest(PartyKind.Establishment, IdentifierTypes.CrNumber, PartyTestSupport.CrNumber(), "مؤسسة", "Est."), CancellationToken.None));
        var applicant = await _host.AsAsync("applicant", sp => sp.GetRequiredService<ContextService>().CurrentIndividualAsync(CancellationToken.None));
        await _host.AsAsync("admin", sp => sp.GetRequiredService<PartyAdminService>().AddRelationshipAsync(
            establishment, new AddRelationshipRequest(applicant.PrimaryIdentifier.Value, code, null, null, null), CancellationToken.None));

        var detail = await _host.AsAsync("admin", sp => sp.GetRequiredService<PartyAdminService>().GetAsync(establishment, CancellationToken.None));
        detail.Relationships.Single().RelationshipNameEn.ShouldBe("Agent");
        detail.Relationships.Single().CanAct.ShouldBeTrue();
        var inUse = await Should.ThrowAsync<ChangeSetException>(() => PublishAsync(PartyChangeTypes.RelationshipType, code, null, ChangeOperation.Delete));
        inUse.Message.ShouldContain("relationships");
        (await FindAsync(PartyClasses.RelationshipType, code)).ShouldNotBeNull();
    }
}
