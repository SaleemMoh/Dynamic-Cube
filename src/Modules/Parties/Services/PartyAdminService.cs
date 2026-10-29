using System.Text.Json.Nodes;
using DynamicCube.Modules.Parties.Domain;
using DynamicCube.Modules.Parties.Persistence;
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

public sealed record AddCredentialRequest(string TypeCode, string Number, DateOnly? IssuedOn, DateOnly? ExpiresOn, CredentialStatus Status, JsonObject? Data);

public sealed record AddProjectRequest(string? Number, string NameAr, string? NameEn, string? ClientName, decimal? ContractValue, DateOnly? StartOn, DateOnly? EndOn, ProjectStatus Status, JsonObject? Data);

public sealed record AddSanctionRequest(string TypeCode, string Reason, DateOnly? StartsOn, DateOnly? EndsOn);

public sealed record LiftSanctionRequest(string Reason);

/// <summary>Employee-side registry operations: search, detail, manual registration and records.</summary>
internal sealed class PartyAdminService(PartiesDbContext context, PartyDirectory directory, ProfileService profiles, ICurrentUser user, IServiceProvider services, TimeProvider clock)
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
            .Include(r => r.RelationshipType)
            .Where(r => r.IndividualId == id || r.PrincipalId == id)
            .OrderBy(r => r.EndedAt != null).ThenBy(r => r.RelationshipType!.DisplayOrder)
            .ToListAsync(cancellationToken);
        var credentials = await context.Credentials.AsNoTracking().Include(c => c.CredentialType).Where(c => c.PartyId == id).OrderBy(c => c.ExpiresOn).ToListAsync(cancellationToken);
        var projects = await context.Projects.AsNoTracking().Where(p => p.PartyId == id).OrderByDescending(p => p.StartOn).ToListAsync(cancellationToken);
        var sanctions = await context.Sanctions.AsNoTracking().Include(s => s.SanctionType).Where(s => s.PartyId == id).OrderByDescending(s => s.StartsOn).ToListAsync(cancellationToken);
        var today = Today();
        return new PartyDetail(
            Summary(party),
            [.. party.Identifiers.OrderByDescending(i => i.IsPrimary).Select(i => new IdentifierInfo(i.Type, i.Value, i.IsPrimary))],
            await profiles.GetAsync(id, cancellationToken),
            [.. relationships.Select(Relationship)],
            [.. credentials.Select(c => new CredentialInfo(c.Id, c.CredentialType!.Code, c.CredentialType.NameAr, c.CredentialType.NameEn, c.Number, c.IssuedOn, c.ExpiresOn,
                c.Status == CredentialStatus.Active && c.ExpiresOn < today ? CredentialStatus.Expired : c.Status, c.Data is null ? null : JsonNode.Parse(c.Data), c.Source))],
            [.. projects.Select(p => new ProjectInfo(p.Id, p.Number, p.NameAr, p.NameEn, p.ClientName, p.ContractValue, p.StartOn, p.EndOn, p.Status, p.Source))],
            [.. sanctions.Select(s => new SanctionInfo(s.Id, s.SanctionType!.Code, s.SanctionType.NameAr, s.SanctionType.NameEn, s.SanctionType.BlocksApplications,
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

        var type = await context.RelationshipTypes.AsNoTracking().SingleOrDefaultAsync(t => t.Code == request.RelationshipCode && t.IsActive, cancellationToken);
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
            RelationshipTypeId = type!.Id,
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

    public async Task<Guid> AddCredentialAsync(Guid partyId, AddCredentialRequest request, CancellationToken cancellationToken)
    {
        var party = await RequireAsync(partyId, cancellationToken);
        var errors = new ValidationErrors();
        var type = await context.CredentialTypes.AsNoTracking().SingleOrDefaultAsync(t => t.Code == request.TypeCode && t.IsActive, cancellationToken);
        if (type is null || type.PartyKind != party.Kind)
        {
            errors.Add("typeCode", $"'{request.TypeCode}' is not a credential type of {party.Kind} parties.");
        }

        errors.Required("number", request.Number);
        errors.MaxLength("number", request.Number, 100);
        if (type?.HasExpiry == true && request.ExpiresOn is null)
        {
            errors.Add("expiresOn", "Required for this credential type.");
        }

        errors.ThrowIfAny();
        if (await context.Credentials.AnyAsync(c => c.PartyId == partyId && c.CredentialTypeId == type!.Id && c.Number == request.Number.Trim(), cancellationToken))
        {
            throw new PartyConflictException($"Credential {request.Number} is already recorded.");
        }

        var credential = new Credential
        {
            PartyId = partyId,
            CredentialTypeId = type!.Id,
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
        var type = await context.SanctionTypes.AsNoTracking().SingleOrDefaultAsync(t => t.Code == request.TypeCode && t.IsActive, cancellationToken);
        if (type is null)
        {
            errors.Add("typeCode", $"'{request.TypeCode}' does not exist.");
        }

        errors.Required("reason", request.Reason);
        errors.MaxLength("reason", request.Reason, 2000);
        var startsOn = request.StartsOn ?? Today();
        var endsOn = request.EndsOn ?? (type?.DefaultDays is int days ? startsOn.AddDays(days) : null);
        if (endsOn < startsOn)
        {
            errors.Add("endsOn", "Must not be before startsOn.");
        }

        errors.ThrowIfAny();
        var sanction = new Sanction { PartyId = partyId, SanctionTypeId = type!.Id, Reason = request.Reason.Trim(), StartsOn = startsOn, EndsOn = endsOn };
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

    private static RelationshipInfo Relationship(Relationship r) =>
        new(r.Id, r.IndividualId, r.Individual!.NameAr, r.Individual.PrimaryIdentifier.Value, r.PrincipalId, r.Principal!.NameAr, r.Principal.PrimaryIdentifier.Value,
            r.RelationshipType!.Code, r.RelationshipType.NameAr, r.RelationshipType.NameEn, r.RelationshipType.CanAct, r.Services, r.ValidFrom, r.ValidTo, r.Source, r.SourceReference, r.EndedAt);
}
