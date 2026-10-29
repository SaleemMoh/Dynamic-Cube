using System.Globalization;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Parties.Changes;
using DynamicCube.Modules.Parties.Domain;
using DynamicCube.Modules.Parties.Persistence;
using DynamicCube.Platform.Integration;
using DynamicCube.Platform.Lookups;
using DynamicCube.Platform.Parties;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DynamicCube.Modules.Parties.Services;

/// <summary>A party the individual may act for.</summary>
public sealed record ActingContext(
    Guid PartyId,
    PartyKind Kind,
    string NameAr,
    string NameEn,
    string IdentifierType,
    string Identifier,
    string? RelationshipCode,
    string? RelationshipNameAr,
    string? RelationshipNameEn,
    IReadOnlyList<string> Services,
    DateOnly? ValidTo);

/// <summary>A context source that could not be read; the contexts it gave before are kept.</summary>
public sealed record ContextWarning(string Source, IntegrationOutcome Outcome, string? MessageAr, string? MessageEn);

public sealed record ContextsResult(ActingContext Individual, IReadOnlyList<ActingContext> Contexts, DateTimeOffset? RefreshedAt, IReadOnlyList<ContextWarning> Warnings);

/// <summary>
/// Applicant identity and acting contexts: the individual is registered on first sign-in from the national ID claim;
/// the parties they may act for come from the configured context sources (e.g. MCI establishments by ID number), with
/// provider relation codes translated to relationship types, plus relationships recorded in the system.
/// </summary>
internal sealed partial class ContextService(
    PartiesDbContext context,
    PartyDirectory directory,
    IApplicantIdentity applicant,
    IIntegrationGateway gateway,
    TimeProvider clock,
    ILookupDirectory lookups,
    ILogger<ContextService> logger)
{
    /// <summary>The signed-in individual, registered when missing.</summary>
    public async Task<Party> CurrentIndividualAsync(CancellationToken cancellationToken)
    {
        var number = applicant.NationalId;
        if (string.IsNullOrWhiteSpace(number))
        {
            throw new ActingPartyException("The sign-in has no national ID.");
        }

        var type = IdentifierTypes.ForPersonNumber(number);
        var existing = await directory.FindAsync(type, number, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await directory.LockAsync(type, number, cancellationToken);
        var party = await directory.EnsureAsync(PartyKind.Individual, type, number, applicant.Name, applicant.Name, RecordSource.SignIn, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return party;
    }

    public async Task<ContextsResult> GetAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        var individual = await CurrentIndividualAsync(cancellationToken);
        var warnings = await RefreshIfDueAsync(individual, forceRefresh, cancellationToken);
        return new ContextsResult(Self(individual), await ListAsync(individual.Id, cancellationToken), individual.ContextsRefreshedAt, warnings);
    }

    /// <summary>Re-reads the context sources when they are older than their refresh period (or when forced).</summary>
    public async Task<IReadOnlyList<ContextWarning>> RefreshIfDueAsync(Party individual, bool force, CancellationToken cancellationToken)
    {
        var sources = await context.ContextSources.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.DisplayOrder).ToListAsync(cancellationToken);
        if (sources.Count == 0)
        {
            return [];
        }

        var now = clock.GetUtcNow();
        var due = force || individual.ContextsRefreshedAt is null || now - individual.ContextsRefreshedAt >= TimeSpan.FromMinutes(sources.Min(s => s.RefreshMinutes));
        if (!due)
        {
            return [];
        }

        var number = individual.PrimaryIdentifier.Value;
        var types = await lookups.ValuesAsync(PartyClasses.RelationshipType, cancellationToken);
        var results = new List<(ContextSource Source, IntegrationResult Result)>();
        foreach (var source in sources)
        {
            // Calls happen before the transaction: a slow provider must not hold database locks.
            results.Add((source, await gateway.InvokeAsync(source.OperationCode, new Dictionary<string, string?> { [source.InputParameter] = number }, cancellationToken)));
        }

        var warnings = new List<ContextWarning>();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await directory.LockAsync(individual.PrimaryIdentifier.Type, number, cancellationToken);
        var tracked = await context.Parties.Include(p => p.Identifiers).SingleAsync(p => p.Id == individual.Id, cancellationToken);
        foreach (var (source, result) in results)
        {
            if (result.Outcome is IntegrationOutcome.BusinessError or IntegrationOutcome.TechnicalError)
            {
                warnings.Add(new ContextWarning(source.Code, result.Outcome, result.MessageAr, result.MessageEn));
                continue;
            }

            var items = result.Outcome == IntegrationOutcome.NotFound ? [] : Items(JsonPaths.Read(result.Data, source.ItemsPath));
            await ApplyAsync(tracked, source, items, types, now, cancellationToken);
        }

        if (warnings.Count < results.Count)
        {
            tracked.ContextsRefreshedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        individual.ContextsRefreshedAt = tracked.ContextsRefreshedAt;
        return warnings;
    }

    private async Task ApplyAsync(Party individual, ContextSource source, IReadOnlyList<JsonNode?> items, IReadOnlyList<LookupValue> types, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await context.Relationships
            .Where(r => r.IndividualId == individual.Id && r.Source == RecordSource.Integration && r.SourceReference == source.Code && r.EndedAt == null)
            .ToListAsync(cancellationToken);
        var seen = new HashSet<Guid>();
        foreach (var item in items)
        {
            var identifier = JsonPaths.ReadText(item, source.IdentifierPath)?.Trim();
            if (string.IsNullOrEmpty(identifier))
            {
                continue;
            }

            var status = JsonPaths.ReadText(item, source.StatusPath);
            if (source.ActiveStatuses.Count > 0 && !source.ActiveStatuses.Contains(status?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var type = RelationshipTypeOf(source, item, types);
            if (type is null)
            {
                UnknownRelation(logger, source.Code, JsonPaths.ReadText(item, source.RelationCodePath));
                continue;
            }

            var identifierType = source.PrincipalKind == PartyKind.Individual ? IdentifierTypes.ForPersonNumber(identifier) : source.PrincipalIdentifierType;
            if (!IdentifierTypes.IsValid(identifierType, identifier))
            {
                InvalidIdentifier(logger, source.Code, identifierType);
                continue;
            }

            var principal = await directory.EnsureAsync(
                source.PrincipalKind,
                identifierType,
                identifier,
                JsonPaths.ReadText(item, source.NameArPath)?.Trim(),
                JsonPaths.ReadText(item, source.NameEnPath)?.Trim(),
                RecordSource.Integration,
                cancellationToken);
            if (principal.Id == individual.Id)
            {
                continue;
            }

            var relationship = existing.FirstOrDefault(r => r.PrincipalId == principal.Id && r.RelationshipCode == type.Code && !seen.Contains(r.Id));
            if (relationship is null)
            {
                relationship = new Relationship
                {
                    IndividualId = individual.Id,
                    PrincipalId = principal.Id,
                    RelationshipCode = type.Code,
                    Source = RecordSource.Integration,
                    SourceReference = source.Code,
                };
                context.Relationships.Add(relationship);
            }

            relationship.ValidFrom = Date(JsonPaths.ReadText(item, source.ValidFromPath));
            relationship.ValidTo = Date(JsonPaths.ReadText(item, source.ValidToPath));
            // No services path means the relationship covers every service (an empty path would read the whole item).
            var services = source.ServicesPath is null
                ? []
                : Items(JsonPaths.Read(item, source.ServicesPath)).Select(JsonPaths.Text).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).ToList();
            if (!relationship.Services.SequenceEqual(services))
            {
                relationship.Services = services;
            }

            seen.Add(relationship.Id);
        }

        foreach (var gone in existing.Where(r => !seen.Contains(r.Id)))
        {
            gone.EndedAt = now;
        }
    }

    /// <summary>Active relationships whose type allows acting, with active principals.</summary>
    public async Task<IReadOnlyList<ActingContext>> ListAsync(Guid individualId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var types = (await lookups.ValuesAsync(PartyClasses.RelationshipType, cancellationToken)).Where(t => t.Flag(PartyClasses.CanAct)).ToList();
        var codes = types.Select(t => t.Code).ToList();
        var rows = await context.Relationships.AsNoTracking()
            .Include(r => r.Principal!).ThenInclude(p => p.Identifiers)
            .Where(r => r.IndividualId == individualId && r.EndedAt == null && codes.Contains(r.RelationshipCode) && r.Principal!.Status == PartyStatus.Active)
            .Where(r => (r.ValidFrom == null || r.ValidFrom <= today) && (r.ValidTo == null || r.ValidTo >= today))
            .ToListAsync(cancellationToken);

        // One context per principal: the first (highest) relationship type wins; services are merged.
        return [.. rows.OrderBy(r => codes.IndexOf(r.RelationshipCode)).ThenBy(r => r.Principal!.NameAr, StringComparer.Ordinal).GroupBy(r => r.PrincipalId).Select(g =>
        {
            var first = g.First();
            var type = types[codes.IndexOf(first.RelationshipCode)];
            var services = g.Any(r => r.Services.Count == 0) ? [] : g.SelectMany(r => r.Services).Distinct().ToList();
            var identifier = first.Principal!.PrimaryIdentifier;
            return new ActingContext(first.PrincipalId, first.Principal.Kind, first.Principal.NameAr, first.Principal.NameEn, identifier.Type, identifier.Value,
                type.Code, type.NameAr, type.NameEn, services, g.Max(r => r.ValidTo));
        })];
    }

    public static ActingContext Self(Party individual) =>
        new(individual.Id, individual.Kind, individual.NameAr, individual.NameEn, individual.PrimaryIdentifier.Type, individual.PrimaryIdentifier.Value, null, null, null, [], null);

    private static LookupValue? RelationshipTypeOf(ContextSource source, JsonNode? item, IReadOnlyList<LookupValue> types)
    {
        var code = JsonPaths.ReadText(item, source.RelationCodePath)?.Trim();
        if (code is not null)
        {
            var mapped = types.FirstOrDefault(t => PartyClasses.ProviderCodes(t).Any(c => c.Provider == source.RelationProvider && c.Code == code));
            if (mapped is not null)
            {
                return mapped;
            }
        }

        return source.DefaultRelationshipCode is null ? null : types.FirstOrDefault(t => t.Code == source.DefaultRelationshipCode);
    }

    private static List<JsonNode?> Items(JsonNode? node) => node switch
    {
        null => [],
        JsonArray array => [.. array],
        _ => [node],
    };

    private static DateOnly? Date(string? text) =>
        DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value)
            ? DateOnly.FromDateTime(value)
            : null;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Context source {Source}: relation code {Code} is not mapped to a relationship type")]
    private static partial void UnknownRelation(ILogger logger, string source, string? code);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Context source {Source}: an item has an invalid {IdentifierType}")]
    private static partial void InvalidIdentifier(ILogger logger, string source, string identifierType);
}
