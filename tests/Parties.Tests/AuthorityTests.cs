using DynamicCube.Modules.Parties.Changes;
using DynamicCube.Modules.Parties.Domain;
using DynamicCube.Modules.Parties.Persistence;
using DynamicCube.Modules.Parties.Services;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Parties;
using DynamicCube.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Parties.Tests;

/// <summary>Acting for a firm on an authority: given by the applicant, recorded as a dated relationship, renewed and ended.</summary>
public sealed class AuthorityTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly TestApplicant _applicant = new() { NationalId = PartyTestSupport.NationalId(), Name = "Huda Saleh" };
    private TestHost _host = null!;

    public ValueTask InitializeAsync()
    {
        _host = PartyTestSupport.Host(fixture, _applicant);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    private Task<T> Authorities<T>(Func<AuthorityService, Task<T>> action) => _host.AsAsync("applicant", sp => action(sp.GetRequiredService<AuthorityService>()));

    [Fact]
    public async Task An_authority_makes_the_firm_an_acting_context_until_it_ends()
    {
        var type = TestHost.Unique("AUTH");
        await _host.PublishAsync(PartyChangeTypes.Area, new Change(PartyChangeTypes.AuthorityType, type, ChangeOperation.Create, new AuthorityTypePayload
        {
            NameAr = "تفويض",
            NameEn = "Authorization",
            RelationshipCode = "DELEGATE",
            NumberLabelAr = "رقم التفويض",
            NumberLabelEn = "Authorization number",
            RequiresExpiry = true,
        }));
        var cr = PartyTestSupport.CrNumber();
        var firm = await _host.AsAsync("admin", async sp =>
        {
            var party = await sp.GetRequiredService<PartyDirectory>().EnsureAsync(PartyKind.Establishment, IdentifierTypes.CrNumber, cr, "مؤسسة البناء", "Building Est.", RecordSource.Manual, CancellationToken.None);
            await sp.GetRequiredService<PartiesDbContext>().SaveChangesAsync();
            return party.Id;
        });
        var expires = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(6);
        var expiresText = expires.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + "T00:00:00.000Z";

        (await Authorities(a => a.TypesAsync(CancellationToken.None))).ShouldContain(t => t.Code == type && t.RequiresExpiry && !t.Checked);
        (await Should.ThrowAsync<ChangeValidationException>(() => Authorities(a => a.AddAsync(new AuthorityRequest(type, cr, "A-1", null), CancellationToken.None)))).Errors.Keys.ShouldContain("expiresOn");
        (await Should.ThrowAsync<ChangeValidationException>(() => Authorities(a => a.AddAsync(new AuthorityRequest(type, PartyTestSupport.CrNumber(), "A-1", expiresText), CancellationToken.None)))).Errors.Keys.ShouldContain("principalIdentifier");

        var added = await Authorities(a => a.AddAsync(new AuthorityRequest(type, cr, "A-1", expiresText), CancellationToken.None));
        added.PrincipalId.ShouldBe(firm);
        added.ValidTo.ShouldBe(expires);

        // The firm is now one of the applicant's acting contexts, on that authority.
        var individual = await _host.AsAsync("applicant", sp => sp.GetRequiredService<ContextService>().CurrentIndividualAsync(CancellationToken.None));
        var context = (await _host.AsAsync("applicant", sp => sp.GetRequiredService<ContextService>().ListAsync(individual.Id, CancellationToken.None))).Single(c => c.PartyId == firm);
        context.RelationshipCode.ShouldBe("DELEGATE");
        context.Authority.ShouldBe($"{type}:A-1");
        var acting = await _host.AsAsync("applicant", sp =>
        {
            PartyTestSupport.Header(sp, firm.ToString());
            return sp.GetRequiredService<IActingPartyAccessor>().GetAsync(CancellationToken.None);
        });
        acting.Authority.ShouldBe($"{type}:A-1");

        // Giving it again renews the same authority with its new number.
        await Authorities(a => a.AddAsync(new AuthorityRequest(type, cr, "A-2", expires.AddMonths(6).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)), CancellationToken.None));
        var mine = await Authorities(a => a.MineAsync(CancellationToken.None));
        mine.ShouldHaveSingleItem().Number.ShouldBe("A-2");

        await Authorities(async a =>
        {
            await a.EndAsync(mine[0].Id, CancellationToken.None);
            return true;
        });
        (await _host.AsAsync("applicant", sp => sp.GetRequiredService<ContextService>().ListAsync(individual.Id, CancellationToken.None))).ShouldNotContain(c => c.PartyId == firm);
    }

    [Fact]
    public async Task Authority_types_need_a_relationship_that_allows_acting_and_a_check_for_their_inputs()
    {
        var ex = await Should.ThrowAsync<ChangeValidationException>(() => _host.PublishAsync(PartyChangeTypes.Area, new Change(PartyChangeTypes.AuthorityType, TestHost.Unique("AUTH"), ChangeOperation.Create, new AuthorityTypePayload
        {
            NameAr = "س",
            NameEn = "X",
            RelationshipCode = "OTHER",
            NumberLabelAr = "رقم",
            NumberLabelEn = "Number",
            Inputs = [new AuthorityInput { Parameter = "number", From = AuthorityInputSource.Number }],
        })));
        ex.Errors.Keys.ShouldContain("relationshipCode");
        ex.Errors.Keys.ShouldContain("operationCode");
    }
}
