using System.Text.Json.Nodes;
using DynamicCube.Modules.Parties.Changes;
using DynamicCube.Modules.Parties.Domain;
using DynamicCube.Modules.Parties.Persistence;
using DynamicCube.Platform.Lookups;
using DynamicCube.Platform.Parties;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Parties.Services;

/// <summary>Party facts for rules in other modules (eligibility, fees, prefill).</summary>
internal sealed class PartyFactsProvider(PartiesDbContext context, ProfileService profiles, ILookupDirectory lookups, TimeProvider clock) : IPartyFacts
{
    public async Task<PartyFacts?> GetAsync(Guid partyId, bool refreshProfile, CancellationToken cancellationToken)
    {
        var party = await context.Parties.AsNoTracking().Include(p => p.Identifiers).SingleOrDefaultAsync(p => p.Id == partyId, cancellationToken);
        if (party is null)
        {
            return null;
        }

        if (refreshProfile)
        {
            // Failed calls keep the stored values; the rules then see what was last known.
            await profiles.RefreshAsync(partyId, force: false, cancellationToken);
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var profile = await context.ProfileValues.AsNoTracking()
            .Where(v => v.PartyId == partyId && v.Field!.IsActive)
            .Select(v => new { v.Field!.Code, v.Value })
            .ToListAsync(cancellationToken);
        var sanctions = await context.Sanctions.AsNoTracking()
            .Where(s => s.PartyId == partyId && s.LiftedAt == null && s.StartsOn <= today && (s.EndsOn == null || s.EndsOn >= today))
            .ToListAsync(cancellationToken);
        var credentials = await context.Credentials.AsNoTracking()
            .Where(c => c.PartyId == partyId)
            .ToListAsync(cancellationToken);
        var sanctionTypes = (await lookups.ValuesAsync(PartyClasses.SanctionType, cancellationToken, includeInactive: true)).ToDictionary(t => t.Code, StringComparer.Ordinal);

        return new PartyFacts(
            party.Id,
            party.Kind,
            party.NameAr,
            party.NameEn,
            party.Identifiers.GroupBy(i => i.Type).ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.IsPrimary).First().Value),
            profile.ToDictionary(p => p.Code, p => p.Value is null ? null : JsonNode.Parse(p.Value)),
            [.. sanctions.Select(s => new SanctionFact(
                s.SanctionTypeCode, !sanctionTypes.TryGetValue(s.SanctionTypeCode, out var type) || PartyClasses.Flag(type, PartyClasses.BlocksApplications, true), s.StartsOn, s.EndsOn))],
            [.. credentials.Select(c => new CredentialFact(
                c.CredentialTypeCode,
                c.Number,
                (c.Status == CredentialStatus.Active && c.ExpiresOn < today ? CredentialStatus.Expired : c.Status).ToString().ToUpperInvariant(),
                c.IssuedOn,
                c.ExpiresOn,
                c.Data is null ? null : JsonNode.Parse(c.Data)))]);
    }
}
