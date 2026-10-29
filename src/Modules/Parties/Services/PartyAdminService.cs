using System.Text.Json.Nodes;
using DynamicCube.Modules.Parties.Changes;
using DynamicCube.Modules.Parties.Domain;
using DynamicCube.Modules.Parties.Persistence;
using DynamicCube.Platform.Lookups;
using DynamicCube.Platform.Parties;
using DynamicCube.Platform.Security;
using DynamicCube.Platform.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Parties.Services;

public sealed record IdentifierInfo(string Type, string Value, bool IsPrimary);

public sealed record PartySummary(Guid Id, PartyKind Kind, string NameAr, string NameEn, PartyStatus Status, string IdentifierType, string Identifier, RecordSource Source);

public sealed record RelationshipInfo(
    Guid Id,
    Guid IndividualId,
    string IndividualName,
    string IndividualIdentifier,
    Guid PrincipalId,
    string PrincipalName,
    string PrincipalIdentifier,
    string RelationshipCode,
    string RelationshipNameAr,
    string RelationshipNameEn,
    bool CanAct,
    IReadOnlyList<string> Services,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    RecordSource Source,
    string? SourceReference,
    DateTimeOffset? EndedAt);

public sealed record CredentialInfo(Guid Id, string TypeCode, string TypeNameAr, string TypeNameEn, string Number, DateOnly? IssuedOn, DateOnly? ExpiresOn, CredentialStatus Status, JsonNode? Data, RecordSource Source);

public sealed record ProjectInfo(Guid Id, string? Number, string NameAr, string? NameEn, string? ClientName, decimal? ContractValue, DateOnly? StartOn, DateOnly? EndOn, ProjectStatus Status, RecordSource Source);

public sealed record SanctionInfo(Guid Id, string TypeCode, string TypeNameAr, string TypeNameEn, bool BlocksApplications, string Reason, DateOnly StartsOn, DateOnly? EndsOn, bool IsActive, DateTimeOffset? LiftedAt, string? LiftedBy, string? LiftReason, string CreatedBy);

public sealed record PartyDetail(
    PartySummary Party,
    IReadOnlyList<IdentifierInfo> Identifiers,
    IReadOnlyList<ProfileEntry> Profile,
    IReadOnlyList<RelationshipInfo> Relationships,
    IReadOnlyList<CredentialInfo> Credentials,
    IReadOnlyList<ProjectInfo> Projects,
    IReadOnlyList<SanctionInfo> Sanctions,
    DateTimeOffset? ContextsRefreshedAt);

public sealed record RegisterPartyRequest(PartyKind Kind, string IdentifierType, string Identifier, string NameAr, string NameEn);

public sealed record AddIdentifierRequest(string Type, string Value);

public sealed record AddRelationshipRequest(string IndividualIdentifier, string RelationshipCode, List<string>? Services, DateOnly? ValidFrom, DateOnly? ValidTo);

/// <summary>The establishments (and other parties) a person may act for, whether or not the person has signed in yet.</summary>
/// <param name="PartyId">The person's party, or null while the person is not registered (registered on the first link or sign-in).</param>
public sealed record PersonLinks(Guid? PartyId, string Identifier, DateTimeOffset? ContextsRefreshedAt, IReadOnlyList<RelationshipInfo> Links);

/// <summary>
/// Links a person to an establishment by its CR or unified (700) number. An establishment that is not registered yet is
/// registered with the names given; a person who has not signed in yet is registered with their names.
/// </summary>
public sealed record LinkEstablishmentRequest(
    string EstablishmentIdentifier,
    string? EstablishmentNameAr,
    string? EstablishmentNameEn,
    string RelationshipCode,
    List<string>? Services,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    string? PersonNameAr,
    string? PersonNameEn);

public sealed record AddCredentialRequest(string TypeCode, string Number, DateOnly? IssuedOn, DateOnly? ExpiresOn, CredentialStatus Status, JsonObject? Data);

public sealed record AddProjectRequest(string? Number, string NameAr, string? NameEn, string? ClientName, decimal? ContractValue, DateOnly? StartOn, DateOnly? EndOn, ProjectStatus Status, JsonObject? Data);

public sealed record AddSanctionRequest(string TypeCode, string Reason, DateOnly? StartsOn, DateOnly? EndsOn);

public sealed record LiftSanctionRequest(string Reason);

/// <summary>Employee-side registry operations: search, detail, manual registration and records.</summary>
internal sealed class PartyAdminService(PartiesDbContext context, PartyDirectory directory, ProfileService profiles, ICurrentUser user, IServiceProvider services, ILookupDirectory lookups, TimeProvider clock)
{
    /// <param name="app">Only parties with a request for a service of this business app.</param>
    public async Task<(IReadOnlyList<PartySummary> Items, int Total)> SearchAsync(string? search, PartyKind? kind, int skip, int take, CancellationToken cancellationToken, string? app = null)
    {
        var query = context.Parties.AsNoTracking().Include(p => p.Identifiers).AsQueryable();
        if (!string.IsNullOrWhiteSpace(app))
        {
            var ids = services.GetService<IServiceCatalog>() is { } catalog ? await catalog.PartiesOfAppAsync(app, cancellationToken) : [];
            query = query.Where(p => ids.Contains(p.Id));
        }

        if (kind is not null)
        {
            query = query.Where(p => p.Kind == kind);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();
            var pattern = "%" + text.Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
            query = query.Where(p => p.Identifiers.Any(i => i.Value == text) || EF.Functions.ILike(p.NameAr, pattern) || EF.Functions.ILike(p.NameEn, pattern));
        }

        var total = await query.CountAsync(cancellationToken);
        var parties = await query.OrderBy(p => p.NameAr).Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200)).ToListAsync(cancellationToken);
        return ([.. parties.Select(Summary)], total);
    }

    public async Task<PartyDetail> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var party = await context.Parties.AsNoTracking().Include(p => p.Identifiers).SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new PartyNotFoundException($"Party {id} does not exist.");
        var relationships = await context.Relationships.AsNoTracking()
            .Include(r => r.Individual!).ThenInclude(p => p.Identifiers)
            .Include(r => r.Principal!).ThenInclude(p => p.Identifiers)
            .Where(r => r.IndividualId == id || r.PrincipalId == id)
            .ToListAsync(cancellationToken);
        var relationshipTypes = await TypesAsync(PartyClasses.RelationshipType, cancellationToken);
        var credentials = await context.Credentials.AsNoTracking().Where(c => c.PartyId == id).OrderBy(c => c.ExpiresOn).ToListAsync(cancellationToken);
        var credentialTypes = await TypesAsync(PartyClasses.CredentialType, cancellationToken);
        var projects = await context.Projects.AsNoTracking().Where(p => p.PartyId == id).OrderByDescending(p => p.StartOn).ToListAsync(cancellationToken);
        var sanctions = await context.Sanctions.AsNoTracking().Where(s => s.PartyId == id).OrderByDescending(s => s.StartsOn).ToListAsync(cancellationToken);
        var sanctionTypes = await TypesAsync(PartyClasses.SanctionType, cancellationToken);
        var today = Today();
        return new PartyDetail(
            Summary(party),
            [.. party.Identifiers.OrderByDescending(i => i.IsPrimary).Select(i => new IdentifierInfo(i.Type, i.Value, i.IsPrimary))],
            await profiles.GetAsync(id, cancellationToken),
            [.. relationships.OrderBy(r => r.EndedAt != null).ThenBy(r => Order(relationshipTypes, r.RelationshipCode)).Select(r => Relationship(r, relationshipTypes))],
            [.. credentials.Select(c => new CredentialInfo(c.Id, c.CredentialTypeCode, NameAr(credentialTypes, c.CredentialTypeCode), NameEn(credentialTypes, c.CredentialTypeCode), c.Number, c.IssuedOn, c.ExpiresOn,
                c.Status == CredentialStatus.Active && c.ExpiresOn < today ? CredentialStatus.Expired : c.Status, c.Data is null ? null : JsonNode.Parse(c.Data), c.Source))],
            [.. projects.Select(p => new ProjectInfo(p.Id, p.Number, p.NameAr, p.NameEn, p.ClientName, p.ContractValue, p.StartOn, p.EndOn, p.Status, p.Source))],
            [.. sanctions.Select(s => new SanctionInfo(s.Id, s.SanctionTypeCode, NameAr(sanctionTypes, s.SanctionTypeCode), NameEn(sanctionTypes, s.SanctionTypeCode),
                !sanctionTypes.TryGetValue(s.SanctionTypeCode, out var type) || PartyClasses.Flag(type, PartyClasses.BlocksApplications, true),
                s.Reason, s.StartsOn, s.EndsOn, s.IsActiveOn(today), s.LiftedAt, s.LiftedBy, s.LiftReason, s.CreatedBy))],
            party.ContextsRefreshedAt);
    }

    public async Task<Guid> RegisterAsync(RegisterPartyRequest request, CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        ValidateIdentifier(errors, request.Kind, request.IdentifierType, request.Identifier?.Trim(), "identifier");
        errors.Required("nameAr", request.NameAr);
        errors.Required("nameEn", request.NameEn);
        errors.MaxLength("nameAr", request.NameAr, 500);
        errors.MaxLength("nameEn", request.NameEn, 500);
        errors.ThrowIfAny();

        var value = request.Identifier!.Trim();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await directory.LockAsync(request.IdentifierType, value, cancellationToken);
        if (await directory.FindAsync(request.IdentifierType, value, cancellationToken) is not null)
        {
            throw new PartyConflictException($"A party with {request.IdentifierType} {value} is already registered.");
        }

        var party = await directory.EnsureAsync(request.Kind, request.IdentifierType, value, request.NameAr.Trim(), request.NameEn.Trim(), RecordSource.Manual, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return party.Id;
    }

    public async Task AddIdentifierAsync(Guid partyId, AddIdentifierRequest request, CancellationToken cancellationToken)
    {
        var party = await RequireAsync(partyId, cancellationToken);
        var errors = new ValidationErrors();
        ValidateIdentifier(errors, party.Kind, request.Type, request.Value?.Trim(), "value");
        errors.ThrowIfAny();
        var value = request.Value!.Trim();
        if (await context.Identifiers.AnyAsync(i => i.Type == request.Type && i.Value == value, cancellationToken))
        {
            throw new PartyConflictException($"{request.Type} {value} is already registered.");
        }

        context.Identifiers.Add(new PartyIdentifier { PartyId = partyId, Type = request.Type, Value = value });
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Records a relationship managed in the system (e.g. an authorized delegate); refreshes never end it.</summary>
    public async Task<Guid> AddRelationshipAsync(Guid principalId, AddRelationshipRequest request, CancellationToken cancellationToken)
    {
        await RequireAsync(principalId, cancellationToken);
        var errors = new ValidationErrors();
        var number = request.IndividualIdentifier?.Trim() ?? string.Empty;
        var identifierType = IdentifierTypes.ForPersonNumber(number);
        if (!IdentifierTypes.IsValid(identifierType, number))
        {
            errors.Add("individualIdentifier", "A 10-digit national ID or iqama number is required.");
        }

        var type = await ActiveTypeAsync(PartyClasses.RelationshipType, request.RelationshipCode, cancellationToken);
        if (type is null)
        {
            errors.Add("relationshipCode", $"'{request.RelationshipCode}' does not exist.");
        }

        if (request.ValidFrom is not null && request.ValidTo is not null && request.ValidTo < request.ValidFrom)
        {
            errors.Add("validTo", "Must not be before validFrom.");
        }

        errors.ThrowIfAny();
        var individual = await directory.FindAsync(identifierType, number, cancellationToken)
            ?? throw new PartyNotFoundException($"No individual with {identifierType} {number} is registered; they register by signing in to the portal.");
        if (individual.Id == principalId)
        {
            throw new PartyConflictException("A party cannot be related to itself.");
        }

        var relationship = new Relationship
        {
            IndividualId = individual.Id,
            PrincipalId = principalId,
            RelationshipCode = type!.Code,
            Services = request.Services?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct().ToList() ?? [],
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            Source = RecordSource.Manual,
        };
        context.Relationships.Add(relationship);
        await context.SaveChangesAsync(cancellationToken);
        return relationship.Id;
    }

    public async Task EndRelationshipAsync(Guid partyId, Guid relationshipId, CancellationToken cancellationToken)
    {
        var relationship = await context.Relationships.SingleOrDefaultAsync(r => r.Id == relationshipId && (r.PrincipalId == partyId || r.IndividualId == partyId), cancellationToken)
            ?? throw new PartyNotFoundException($"Relationship {relationshipId} does not exist.");
        if (relationship.Source == RecordSource.Integration)
        {
            throw new PartyConflictException("Relationships from integrations end when the provider no longer reports them.");
        }

        relationship.EndedAt ??= clock.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PersonLinks> PersonLinksAsync(string personNumber, CancellationToken cancellationToken)
    {
        var (type, number) = PersonNumber(personNumber);
        var person = await directory.FindAsync(type, number, cancellationToken);
        if (person is null)
        {
            return new PersonLinks(null, number, null, []);
        }

        var relationships = await context.Relationships.AsNoTracking()
            .Include(r => r.Individual!).ThenInclude(p => p.Identifiers)
            .Include(r => r.Principal!).ThenInclude(p => p.Identifiers)
            .Where(r => r.IndividualId == person.Id)
            .ToListAsync(cancellationToken);
        var types = await TypesAsync(PartyClasses.RelationshipType, cancellationToken);
        return new PersonLinks(
            person.Id,
            number,
            person.ContextsRefreshedAt,
            [.. relationships.OrderBy(r => r.EndedAt != null).ThenBy(r => r.Principal!.NameAr).Select(r => Relationship(r, types))]);
    }

    public async Task<Guid> LinkEstablishmentAsync(string personNumber, LinkEstablishmentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (personType, number) = PersonNumber(personNumber);
        var establishmentNumber = request.EstablishmentIdentifier?.Trim() ?? string.Empty;
        var establishmentType = establishmentNumber.StartsWith('7') ? IdentifierTypes.UnifiedNumber : IdentifierTypes.CrNumber;
        var errors = new ValidationErrors();
        if (!IdentifierTypes.IsValid(establishmentType, establishmentNumber))
        {
            errors.Add("establishmentIdentifier", "A 10-digit commercial registration number, or a unified number starting with 7.");
        }

        errors.ThrowIfAny();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var establishment = await directory.FindAsync(establishmentType, establishmentNumber, cancellationToken);
        if (establishment is null)
        {
            errors.Required("establishmentNameAr", request.EstablishmentNameAr);
            errors.Required("establishmentNameEn", request.EstablishmentNameEn);
            errors.ThrowIfAny();
            await directory.LockAsync(establishmentType, establishmentNumber, cancellationToken);
            establishment = await directory.EnsureAsync(PartyKind.Establishment, establishmentType, establishmentNumber, request.EstablishmentNameAr!.Trim(), request.EstablishmentNameEn!.Trim(), RecordSource.Manual, cancellationToken);
        }
        else if (establishment.Kind != PartyKind.Establishment)
        {
            throw new PartyConflictException($"{establishmentNumber} is not an establishment.");
        }

        if (await directory.FindAsync(personType, number, cancellationToken) is null)
        {
            await directory.LockAsync(personType, number, cancellationToken);
            var nameAr = string.IsNullOrWhiteSpace(request.PersonNameAr) ? number : request.PersonNameAr.Trim();
            var nameEn = string.IsNullOrWhiteSpace(request.PersonNameEn) ? nameAr : request.PersonNameEn.Trim();
            await directory.EnsureAsync(PartyKind.Individual, personType, number, nameAr, nameEn, RecordSource.Manual, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        var person = (await directory.FindAsync(personType, number, cancellationToken))!;
        if (await context.Relationships.AnyAsync(r => r.IndividualId == person.Id && r.PrincipalId == establishment.Id && r.RelationshipCode == request.RelationshipCode && r.EndedAt == null, cancellationToken))
        {
            throw new PartyConflictException("This person already has this relationship with the establishment.");
        }

        var id = await AddRelationshipAsync(establishment.Id, new AddRelationshipRequest(number, request.RelationshipCode, request.Services, request.ValidFrom, request.ValidTo), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return id;
    }

    public async Task EndPersonLinkAsync(string personNumber, Guid relationshipId, CancellationToken cancellationToken)
    {
        var (type, number) = PersonNumber(personNumber);
        var person = await directory.FindAsync(type, number, cancellationToken) ?? throw new PartyNotFoundException($"No person with {type} {number} is registered.");
        await EndRelationshipAsync(person.Id, relationshipId, cancellationToken);
    }

    private static (string Type, string Number) PersonNumber(string? value)
    {
        var number = value?.Trim() ?? string.Empty;
        var type = IdentifierTypes.ForPersonNumber(number);
        if (!IdentifierTypes.IsValid(type, number))
        {
            var errors = new ValidationErrors();
            errors.Add("nationalId", "A 10-digit national ID or iqama number is required.");
            errors.ThrowIfAny();
        }

        return (type, number);
    }

    public async Task<Guid> AddCredentialAsync(Guid partyId, AddCredentialRequest request, CancellationToken cancellationToken)
    {
        var party = await RequireAsync(partyId, cancellationToken);
        var errors = new ValidationErrors();
        var type = await ActiveTypeAsync(PartyClasses.CredentialType, request.TypeCode, cancellationToken);
        if (type is null || !string.Equals(type.Text(PartyClasses.PartyKind), party.Kind.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("typeCode", $"'{request.TypeCode}' is not a credential type of {party.Kind} parties.");
        }

        errors.Required("number", request.Number);
        errors.MaxLength("number", request.Number, 100);
        if (type is not null && PartyClasses.Flag(type, PartyClasses.HasExpiry, true) && request.ExpiresOn is null)
        {
            errors.Add("expiresOn", "Required for this credential type.");
        }

        errors.ThrowIfAny();
        if (await context.Credentials.AnyAsync(c => c.PartyId == partyId && c.CredentialTypeCode == type!.Code && c.Number == request.Number.Trim(), cancellationToken))
        {
            throw new PartyConflictException($"Credential {request.Number} is already recorded.");
        }

        var credential = new Credential
        {
            PartyId = partyId,
            CredentialTypeCode = type!.Code,
            Number = request.Number.Trim(),
            IssuedOn = request.IssuedOn,
            ExpiresOn = request.ExpiresOn,
            Status = request.Status,
            Data = request.Data?.ToJsonString(),
            Source = RecordSource.Manual,
        };
        context.Credentials.Add(credential);
        await context.SaveChangesAsync(cancellationToken);
        return credential.Id;
    }

    public async Task<Guid> AddProjectAsync(Guid partyId, AddProjectRequest request, CancellationToken cancellationToken)
    {
        await RequireAsync(partyId, cancellationToken);
        var errors = new ValidationErrors();
        errors.Required("nameAr", request.NameAr);
        errors.MaxLength("nameAr", request.NameAr, 500);
        if (request.ContractValue < 0)
        {
            errors.Add("contractValue", "Must not be negative.");
        }

        if (request.StartOn is not null && request.EndOn is not null && request.EndOn < request.StartOn)
        {
            errors.Add("endOn", "Must not be before startOn.");
        }

        errors.ThrowIfAny();
        var project = new Project
        {
            PartyId = partyId,
            Number = request.Number?.Trim(),
            NameAr = request.NameAr.Trim(),
            NameEn = request.NameEn?.Trim(),
            ClientName = request.ClientName?.Trim(),
            ContractValue = request.ContractValue,
            StartOn = request.StartOn,
            EndOn = request.EndOn,
            Status = request.Status,
            Data = request.Data?.ToJsonString(),
            Source = RecordSource.Manual,
        };
        context.Projects.Add(project);
        await context.SaveChangesAsync(cancellationToken);
        return project.Id;
    }

    public async Task<Guid> AddSanctionAsync(Guid partyId, AddSanctionRequest request, CancellationToken cancellationToken)
    {
        await RequireAsync(partyId, cancellationToken);
        var errors = new ValidationErrors();
        var type = await ActiveTypeAsync(PartyClasses.SanctionType, request.TypeCode, cancellationToken);
        if (type is null)
        {
            errors.Add("typeCode", $"'{request.TypeCode}' does not exist.");
        }

        errors.Required("reason", request.Reason);
        errors.MaxLength("reason", request.Reason, 2000);
        var startsOn = request.StartsOn ?? Today();
        var endsOn = request.EndsOn ?? (type?.Whole(PartyClasses.DefaultDays) is int days ? startsOn.AddDays(days) : null);
        if (endsOn < startsOn)
        {
            errors.Add("endsOn", "Must not be before startsOn.");
        }

        errors.ThrowIfAny();
        var sanction = new Sanction { PartyId = partyId, SanctionTypeCode = type!.Code, Reason = request.Reason.Trim(), StartsOn = startsOn, EndsOn = endsOn };
        context.Sanctions.Add(sanction);
        await context.SaveChangesAsync(cancellationToken);
        return sanction.Id;
    }

    public async Task LiftSanctionAsync(Guid partyId, Guid sanctionId, LiftSanctionRequest request, CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        errors.Required("reason", request.Reason);
        errors.MaxLength("reason", request.Reason, 2000);
        errors.ThrowIfAny();
        var sanction = await context.Sanctions.SingleOrDefaultAsync(s => s.Id == sanctionId && s.PartyId == partyId, cancellationToken)
            ?? throw new PartyNotFoundException($"Sanction {sanctionId} does not exist.");
        if (sanction.LiftedAt is not null)
        {
            throw new PartyConflictException("The sanction is already lifted.");
        }

        if (sanction.CreatedBy == user.UserId)
        {
            // Maker-checker for penalties: whoever imposed a sanction cannot lift it alone.
            throw new PartyConflictException("A sanction must be lifted by someone other than who recorded it.");
        }

        sanction.LiftedAt = clock.GetUtcNow();
        sanction.LiftedBy = user.UserId;
        sanction.LiftReason = request.Reason.Trim();
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Party> RequireAsync(Guid partyId, CancellationToken cancellationToken) =>
        await context.Parties.AsNoTracking().SingleOrDefaultAsync(p => p.Id == partyId, cancellationToken)
            ?? throw new PartyNotFoundException($"Party {partyId} does not exist.");

    private static void ValidateIdentifier(ValidationErrors errors, PartyKind kind, string? type, string? value, string field)
    {
        var allowed = kind == PartyKind.Individual ? IdentifierTypes.Individual : IdentifierTypes.Establishment;
        if (type is null || !allowed.Contains(type))
        {
            errors.Add("identifierType", $"Use one of {string.Join(", ", allowed)}.");
        }
        else if (value is null || !IdentifierTypes.IsValid(type, value))
        {
            errors.Add(field, $"Not a valid {type}.");
        }
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    internal static PartySummary Summary(Party p) =>
        new(p.Id, p.Kind, p.NameAr, p.NameEn, p.Status, p.PrimaryIdentifier.Type, p.PrimaryIdentifier.Value, p.Source);

    private static RelationshipInfo Relationship(Relationship r, IReadOnlyDictionary<string, LookupValue> types) =>
        new(r.Id, r.IndividualId, r.Individual!.NameAr, r.Individual.PrimaryIdentifier.Value, r.PrincipalId, r.Principal!.NameAr, r.Principal.PrimaryIdentifier.Value,
            r.RelationshipCode, NameAr(types, r.RelationshipCode), NameEn(types, r.RelationshipCode), types.TryGetValue(r.RelationshipCode, out var type) && type.Flag(PartyClasses.CanAct),
            r.Services, r.ValidFrom, r.ValidTo, r.Source, r.SourceReference, r.EndedAt);

    /// <summary>The values of a type class by code, inactive ones included (records keep the types they were given).</summary>
    private async Task<IReadOnlyDictionary<string, LookupValue>> TypesAsync(string classCode, CancellationToken cancellationToken) =>
        (await lookups.ValuesAsync(classCode, cancellationToken, includeInactive: true)).ToDictionary(t => t.Code, StringComparer.Ordinal);

    private async Task<LookupValue?> ActiveTypeAsync(string classCode, string? code, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(code) ? null : await lookups.FindAsync(classCode, code.Trim(), cancellationToken) is { IsActive: true } type ? type : null;

    private static int Order(IReadOnlyDictionary<string, LookupValue> types, string code) => types.TryGetValue(code, out var type) ? type.DisplayOrder : int.MaxValue;

    private static string NameAr(IReadOnlyDictionary<string, LookupValue> types, string code) => types.TryGetValue(code, out var type) ? type.NameAr : code;

    private static string NameEn(IReadOnlyDictionary<string, LookupValue> types, string code) => types.TryGetValue(code, out var type) ? type.NameEn : code;
}
