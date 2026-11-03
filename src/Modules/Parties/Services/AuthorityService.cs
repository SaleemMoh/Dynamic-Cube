using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Parties.Changes;
using DynamicCube.Modules.Parties.Domain;
using DynamicCube.Modules.Parties.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Integration;
using DynamicCube.Platform.Lookups;
using DynamicCube.Platform.Parties;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Parties.Services;

/// <summary>An authority type as the applicant chooses it.</summary>
public sealed record AuthorityTypeView(
    string Code,
    string NameAr,
    string NameEn,
    string? DescriptionAr,
    string? DescriptionEn,
    string NumberLabelAr,
    string NumberLabelEn,
    string PrincipalIdentifierType,
    bool RequiresExpiry,
    bool Checked);

/// <summary>An authority the applicant gave: who it is for, its number and until when.</summary>
public sealed record AuthorityView(
    Guid Id,
    string Type,
    string TypeNameAr,
    string TypeNameEn,
    Guid PrincipalId,
    string PrincipalNameAr,
    string PrincipalNameEn,
    string PrincipalIdentifier,
    string Number,
    DateOnly? ValidTo,
    bool Active);

/// <param name="ExpiresOn">The expiry date (<c>yyyy-MM-dd</c>; a date picker's full timestamp is read by its day).</param>
public sealed record AuthorityRequest(string? Type, string? PrincipalIdentifier, string? Number, string? ExpiresOn);

/// <summary>
/// Acting for a firm on an authority (a municipal authorization, a notarised agency…): the applicant gives the firm's
/// identifier and the authority's number and expiry; when the type has a check operation it must confirm the authority
/// (and may give the real expiry and the firm's name). A dated relationship is then recorded, so the firm is one of the
/// applicant's acting contexts until the authority ends; giving it again renews it.
/// </summary>
internal sealed class AuthorityService(
    PartiesDbContext context,
    ContextService contexts,
    PartyDirectory directory,
    IIntegrationGateway gateway,
    ILookupDirectory lookups,
    TimeProvider clock)
{
    private const int MaxNumber = 100;

    public async Task<IReadOnlyList<AuthorityTypeView>> TypesAsync(CancellationToken cancellationToken)
    {
        var types = await context.AuthorityTypes.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.DisplayOrder).ThenBy(t => t.Code).ToListAsync(cancellationToken);
        return [.. types.Select(t => new AuthorityTypeView(t.Code, t.NameAr, t.NameEn, t.DescriptionAr, t.DescriptionEn, t.NumberLabelAr, t.NumberLabelEn, t.PrincipalIdentifierType, t.RequiresExpiry, t.OperationCode is not null))];
    }

    public async Task<IReadOnlyList<AuthorityView>> MineAsync(CancellationToken cancellationToken)
    {
        var individual = await contexts.CurrentIndividualAsync(cancellationToken);
        var today = Today();
        var types = await context.AuthorityTypes.AsNoTracking().ToDictionaryAsync(t => t.Code, cancellationToken);
        var rows = await context.Relationships.AsNoTracking()
            .Include(r => r.Principal!).ThenInclude(p => p.Identifiers)
            .Where(r => r.IndividualId == individual.Id && r.Source == RecordSource.Authority && r.EndedAt == null)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(r =>
        {
            var (type, number) = Split(r.SourceReference);
            types.TryGetValue(type, out var t);
            return new AuthorityView(r.Id, type, t?.NameAr ?? type, t?.NameEn ?? type, r.PrincipalId, r.Principal!.NameAr, r.Principal.NameEn,
                r.Principal.PrimaryIdentifier.Value, number, r.ValidTo, r.ValidTo is null || r.ValidTo >= today);
        })];
    }

    public async Task<AuthorityView> AddAsync(AuthorityRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var type = await context.AuthorityTypes.AsNoTracking().SingleOrDefaultAsync(t => t.Code == request.Type && t.IsActive, cancellationToken)
            ?? throw ChangeValidationException.For("type", "Choose an authority type.");
        var identifier = request.PrincipalIdentifier?.Trim() ?? string.Empty;
        var number = request.Number?.Trim() ?? string.Empty;
        if (identifier.Length is 0 or > 50)
        {
            throw ChangeValidationException.For("principalIdentifier", "Give the firm's number.");
        }

        if (number.Length is 0 or > MaxNumber)
        {
            throw ChangeValidationException.For("number", $"Give the {type.NumberLabelEn.ToLowerInvariant()}.");
        }

        var today = Today();
        DateOnly? expiresOn = null;
        if (!string.IsNullOrWhiteSpace(request.ExpiresOn))
        {
            var text = request.ExpiresOn.Trim();
            expiresOn = text.Length >= 10 && DateOnly.TryParseExact(text[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                ? day
                : throw ChangeValidationException.For("expiresOn", "Give the expiry date as yyyy-mm-dd.");
        }

        if (type.RequiresExpiry && expiresOn is null)
        {
            throw ChangeValidationException.For("expiresOn", "Give the expiry date.");
        }

        if (expiresOn < today)
        {
            throw ChangeValidationException.For("expiresOn", "This authority has expired.");
        }

        var individual = await contexts.CurrentIndividualAsync(cancellationToken);
        var validTo = expiresOn;
        string? nameAr = null, nameEn = null;

        // The check: the provider must confirm the authority; it may know the real expiry and the firm's name.
        if (type.OperationCode is { } operation)
        {
            var inputs = type.Inputs.ToDictionary(i => i.Parameter, i => i.From switch
            {
                AuthorityInputSource.Number => number,
                AuthorityInputSource.Principal => identifier,
                AuthorityInputSource.Individual => individual.PrimaryIdentifier.Value,
                _ => expiresOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            }, StringComparer.Ordinal);
            IntegrationResult result;
            using (IntegrationSubject.Use("PARTY_AUTHORITY", individual.Id))
            {
                result = await gateway.InvokeAsync(operation, inputs, cancellationToken);
            }

            if (result.Outcome != IntegrationOutcome.Success)
            {
                throw ChangeValidationException.For("number", result.MessageEn ?? "The authority could not be confirmed.");
            }

            if (type.ValidOutput is { } valid && !Truthy(Output(result, valid)))
            {
                throw ChangeValidationException.For("number", "The provider does not confirm this authority for this firm.");
            }

            var expiryText = type.ExpiryOutput is { } expiry ? Text(Output(result, expiry)) : null;
            if (expiryText is { Length: >= 10 } && DateOnly.TryParseExact(expiryText[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var known))
            {
                if (known < today)
                {
                    throw ChangeValidationException.For("expiresOn", "The provider says this authority has expired.");
                }

                validTo = known;
            }

            nameAr = type.PrincipalNameArOutput is { } ar ? Text(Output(result, ar)) : null;
            nameEn = type.PrincipalNameEnOutput is { } en ? Text(Output(result, en)) : null;
        }

        await directory.LockAsync(type.PrincipalIdentifierType, identifier, cancellationToken);
        var principal = nameAr is not null || nameEn is not null
            ? await directory.EnsureAsync(type.PrincipalKind, type.PrincipalIdentifierType, identifier, nameAr, nameEn, RecordSource.Integration, cancellationToken)
            : await directory.FindAsync(type.PrincipalIdentifierType, identifier, cancellationToken)
                ?? throw ChangeValidationException.For("principalIdentifier", "No firm with this number is known yet.");
        if (principal.Id == individual.Id)
        {
            throw ChangeValidationException.For("principalIdentifier", "You act for yourself without an authority.");
        }

        if (await lookups.FindAsync(PartyClasses.RelationshipType, type.RelationshipCode, cancellationToken) is not { } relation || !relation.Flag(PartyClasses.CanAct))
        {
            throw new PartyConflictException($"Relationship type '{type.RelationshipCode}' does not allow acting.");
        }

        // Giving the same authority type for the same firm again renews it.
        var reference = $"{type.Code}:{number}";
        var relationship = await context.Relationships.SingleOrDefaultAsync(
            r => r.IndividualId == individual.Id && r.PrincipalId == principal.Id && r.Source == RecordSource.Authority && r.EndedAt == null && r.SourceReference!.StartsWith(type.Code + ":"),
            cancellationToken);
        if (relationship is null)
        {
            relationship = new Relationship { IndividualId = individual.Id, PrincipalId = principal.Id, Source = RecordSource.Authority };
            context.Relationships.Add(relationship);
        }

        relationship.RelationshipCode = type.RelationshipCode;
        relationship.Services = [.. type.Services];
        relationship.ValidFrom = today;
        relationship.ValidTo = validTo;
        relationship.SourceReference = reference;
        await context.SaveChangesAsync(cancellationToken);
        return new AuthorityView(relationship.Id, type.Code, type.NameAr, type.NameEn, principal.Id, principal.NameAr, principal.NameEn, identifier, number, validTo, true);
    }

    /// <summary>Ends one of the applicant's authorities (the firm is no longer an acting context).</summary>
    public async Task EndAsync(Guid id, CancellationToken cancellationToken)
    {
        var individual = await contexts.CurrentIndividualAsync(cancellationToken);
        var relationship = await context.Relationships.SingleOrDefaultAsync(r => r.Id == id && r.IndividualId == individual.Id && r.Source == RecordSource.Authority && r.EndedAt == null, cancellationToken)
            ?? throw new PartyNotFoundException("You have no such authority.");
        relationship.EndedAt = clock.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken);
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    private static (string Type, string Number) Split(string? reference)
    {
        var at = reference?.IndexOf(':', StringComparison.Ordinal) ?? -1;
        return at < 0 ? (reference ?? string.Empty, string.Empty) : (reference![..at], reference[(at + 1)..]);
    }

    private static JsonNode? Output(IntegrationResult result, string name) =>
        result.Outputs.TryGetValue(name, out var value) ? value : JsonPaths.Read(result.Data, name);

    private static string? Text(JsonNode? node) => node switch
    {
        null => null,
        JsonValue v when v.GetValueKind() == JsonValueKind.String => v.GetValue<string>(),
        _ => node.ToJsonString(),
    };

    private static bool Truthy(JsonNode? node) => node is JsonValue v && v.GetValueKind() switch
    {
        JsonValueKind.True => true,
        JsonValueKind.String => v.GetValue<string>() is "true" or "1" or "Y" or "YES" or "VALID" or "ACTIVE",
        JsonValueKind.Number => v.ToJsonString() != "0",
        _ => false,
    };
}
