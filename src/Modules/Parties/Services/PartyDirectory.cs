using DynamicCube.Modules.Parties.Domain;
using DynamicCube.Modules.Parties.Persistence;
using DynamicCube.Platform.Parties;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Parties.Services;

/// <summary>Finds and registers parties by identifier.</summary>
internal sealed class PartyDirectory(PartiesDbContext context)
{
    public Task<Party?> FindAsync(string identifierType, string value, CancellationToken cancellationToken) =>
        context.Parties.Include(p => p.Identifiers)
            .SingleOrDefaultAsync(p => p.Identifiers.Any(i => i.Type == identifierType && i.Value == value), cancellationToken);

    /// <summary>
    /// The party with the identifier, created when missing. Names from an integration replace older names; manual and
    /// sign-in names are only filled in when empty. The caller saves (inside <see cref="LockAsync"/> when concurrent
    /// requests may register the same identifier).
    /// </summary>
    public async Task<Party> EnsureAsync(PartyKind kind, string identifierType, string value, string? nameAr, string? nameEn, RecordSource source, CancellationToken cancellationToken)
    {
        var party = context.Parties.Local.FirstOrDefault(p => p.Identifiers.Any(i => i.Type == identifierType && i.Value == value))
            ?? await FindAsync(identifierType, value, cancellationToken);
        if (party is null)
        {
            party = new Party { Kind = kind, Source = source, NameAr = nameAr ?? nameEn ?? value, NameEn = nameEn ?? nameAr ?? value };
            party.Identifiers.Add(new PartyIdentifier { PartyId = party.Id, Type = identifierType, Value = value, IsPrimary = true });
            context.Parties.Add(party);
            return party;
        }

        if (party.Kind != kind)
        {
            throw new PartyConflictException($"{identifierType} {value} belongs to a party of kind {party.Kind}.");
        }

        if (source == RecordSource.Integration)
        {
            party.NameAr = string.IsNullOrWhiteSpace(nameAr) ? party.NameAr : nameAr.Trim();
            party.NameEn = string.IsNullOrWhiteSpace(nameEn) ? party.NameEn : nameEn.Trim();
        }

        return party;
    }

    /// <summary>Serializes work on one identifier across instances (transaction-scoped PostgreSQL advisory lock).</summary>
    public async Task LockAsync(string identifierType, string value, CancellationToken cancellationToken)
    {
        var key = $"party:{identifierType}:{value}";
        await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
    }
}
