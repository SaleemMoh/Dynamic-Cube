using System.Net;
using DynamicCube.Modules.Parties.Persistence;
using DynamicCube.Modules.Parties.Services;
using DynamicCube.Platform.Integration;
using DynamicCube.Platform.Parties;
using DynamicCube.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Parties.Tests;

public sealed class ContextTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly TestApplicant _applicant = new() { NationalId = PartyTestSupport.NationalId(), Name = "محمد العتيبي" };
    private TestHost _host = null!;

    public ValueTask InitializeAsync()
    {
        _host = PartyTestSupport.Host(fixture, _applicant);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    private Task<ContextsResult> ContextsAsync(bool refresh = true) =>
        _host.AsAsync("applicant", sp => sp.GetRequiredService<ContextService>().GetAsync(refresh, CancellationToken.None));

    [Fact]
    public async Task First_sign_in_registers_the_individual_once()
    {
        var parties = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            _host.AsAsync("applicant", sp => sp.GetRequiredService<ContextService>().CurrentIndividualAsync(CancellationToken.None))));

        parties.Select(p => p.Id).Distinct().Count().ShouldBe(1);
        parties[0].Kind.ShouldBe(PartyKind.Individual);
        parties[0].NameAr.ShouldBe("محمد العتيبي");
        parties[0].PrimaryIdentifier.Type.ShouldBe("NATIONAL_ID");
    }

    [Fact]
    public async Task Contexts_come_from_mci_with_relation_codes_translated()
    {
        var prefix = await PartyTestSupport.PublishMciContextSourceAsync(_host);
        var owned = PartyTestSupport.CrNumber();
        var partner = PartyTestSupport.CrNumber();
        var board = PartyTestSupport.CrNumber();
        var expired = PartyTestSupport.CrNumber();
        PartyTestSupport.Answer(_host, prefix, _ => (HttpStatusCode.OK, PartyTestSupport.Licenses(
            (owned, "مؤسسة مشعل للنظارات", "1", "Active"),
            (partner, "شركة الراجحي", "3", "Active"),
            (board, "شركة المجلس", "12", "Active"),
            (expired, "مؤسسة منتهية", "1", "Expired"))));

        var result = await ContextsAsync();

        result.Warnings.ShouldBeEmpty();
        result.Individual.Identifier.ShouldBe(_applicant.NationalId);
        result.Contexts.Select(c => (c.Identifier, c.RelationshipCode)).ShouldBe([(owned, "OWNER"), (partner, "PARTNER")]);
        result.Contexts[0].NameAr.ShouldBe("مؤسسة مشعل للنظارات");
        result.Contexts[0].Kind.ShouldBe(PartyKind.Establishment);
        result.Contexts.ShouldAllBe(c => c.Services.Count == 0);

        // Board membership is recorded but gives no acting context.
        var relationships = await _host.AsAsync("admin", sp => sp.GetRequiredService<PartiesDbContext>().Relationships
            .CountAsync(r => r.IndividualId == result.Individual.PartyId && r.EndedAt == null));
        relationships.ShouldBe(3);
    }

    [Fact]
    public async Task Refresh_ends_relationships_the_provider_no_longer_reports_and_keeps_them_on_failure()
    {
        var prefix = await PartyTestSupport.PublishMciContextSourceAsync(_host);
        var first = PartyTestSupport.CrNumber();
        var second = PartyTestSupport.CrNumber();
        PartyTestSupport.Answer(_host, prefix, _ => (HttpStatusCode.OK, PartyTestSupport.Licenses((first, "الأولى", "1", "Active"), (second, "الثانية", "1", "Active"))));
        (await ContextsAsync()).Contexts.Count.ShouldBe(2);

        PartyTestSupport.Answer(_host, prefix, _ => (HttpStatusCode.OK, PartyTestSupport.Licenses((second, "الثانية (محدث)", "1", "Active"))));
        var refreshed = await ContextsAsync();
        refreshed.Contexts.Select(c => c.Identifier).ShouldBe([second]);
        refreshed.Contexts[0].NameAr.ShouldBe("الثانية (محدث)");

        PartyTestSupport.Answer(_host, prefix, _ => (HttpStatusCode.ServiceUnavailable, "down"));
        var failed = await ContextsAsync();
        failed.Contexts.Select(c => c.Identifier).ShouldBe([second]);
        failed.Warnings.Single().Outcome.ShouldBe(IntegrationOutcome.TechnicalError);

        PartyTestSupport.Answer(_host, prefix, _ => (HttpStatusCode.OK, MomrahSamples.RecordNotFound));
        (await ContextsAsync()).Contexts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Contexts_are_read_again_only_when_due()
    {
        var prefix = await PartyTestSupport.PublishMciContextSourceAsync(_host);
        PartyTestSupport.Answer(_host, prefix, _ => (HttpStatusCode.OK, PartyTestSupport.Licenses((PartyTestSupport.CrNumber(), "مؤسسة", "1", "Active"))));

        await ContextsAsync(refresh: false);
        var calls = _host.Provider.CountTo(prefix);
        await ContextsAsync(refresh: false);

        calls.ShouldBe(1);
        _host.Provider.CountTo(prefix).ShouldBe(1);
    }

    [Fact]
    public async Task Acting_party_header_is_checked_against_the_contexts()
    {
        var prefix = await PartyTestSupport.PublishMciContextSourceAsync(_host);
        var cr = PartyTestSupport.CrNumber();
        PartyTestSupport.Answer(_host, prefix, _ => (HttpStatusCode.OK, PartyTestSupport.Licenses((cr, "مؤسسة", "1", "Active"))));
        var contexts = await ContextsAsync();
        var establishment = contexts.Contexts.Single().PartyId;

        var acting = await _host.AsAsync("applicant", sp =>
        {
            PartyTestSupport.Header(sp, establishment.ToString());
            return sp.GetRequiredService<IActingPartyAccessor>().GetAsync(CancellationToken.None);
        });
        var self = await _host.AsAsync("applicant", sp =>
        {
            PartyTestSupport.Header(sp, null);
            return sp.GetRequiredService<IActingPartyAccessor>().GetAsync(CancellationToken.None);
        });

        acting.PartyId.ShouldBe(establishment);
        acting.IndividualId.ShouldBe(contexts.Individual.PartyId);
        acting.RelationshipCode.ShouldBe("OWNER");
        acting.IsSelf.ShouldBeFalse();
        self.IsSelf.ShouldBeTrue();
        await Should.ThrowAsync<ActingPartyException>(() => _host.AsAsync("applicant", sp =>
        {
            PartyTestSupport.Header(sp, Guid.NewGuid().ToString());
            return sp.GetRequiredService<IActingPartyAccessor>().GetAsync(CancellationToken.None);
        }));
    }
}
