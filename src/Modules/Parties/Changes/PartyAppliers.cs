using DynamicCube.Modules.Parties.Domain;
using DynamicCube.Modules.Parties.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Integration;
using DynamicCube.Platform.Parties;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Parties.Changes;

/// <summary>Base of the Party Registry configuration appliers.</summary>
internal abstract class PartyApplier<TEntity, TPayload>(PartiesDbContext context)
    : ConfigApplier<PartiesDbContext, ConfigRefs<PartiesDbContext>, TEntity, TPayload>(context)
    where TEntity : ConfigEntity, new()
    where TPayload : ConfigPayload, new()
{
    public override string Area => PartyChangeTypes.Area;

    public override string ResourceCode => PartyResources.Config;

    protected override System.Text.RegularExpressions.Regex CodePattern => ValidationErrors.IdentifierCode();

    protected override ConfigRefs<PartiesDbContext> CreateRefs(PartiesDbContext context, ChangeSet? draft) => new(context, draft);

    protected static void Path(ValidationErrors errors, string field, string? path, bool required = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            if (required)
            {
                errors.Add(field, "Required.");
            }

            return;
        }

        if (!JsonPaths.IsValid(path.Trim()))
        {
            errors.Add(field, "Use a dot path such as Licenses or RelationTypeInfo.Id.");
        }
    }

    protected static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class RelationshipTypePayload : ConfigPayload
{
    public bool CanAct { get; set; }

    public int DisplayOrder { get; set; }

    public List<ExternalCode> ExternalCodes { get; set; } = [];
}

internal sealed class RelationshipTypeApplier(PartiesDbContext db) : PartyApplier<RelationshipType, RelationshipTypePayload>(db)
{
    public override string EntityType => PartyChangeTypes.RelationshipType;

    protected override IQueryable<RelationshipType> Query(PartiesDbContext context) => context.RelationshipTypes;

    protected override IQueryable<RelationshipType> ListQuery(PartiesDbContext context, string? parent) =>
        Query(context).OrderBy(t => t.DisplayOrder).ThenBy(t => t.Code);

    protected override async Task ApplyAsync(RelationshipType entity, RelationshipTypePayload payload, ConfigRefs<PartiesDbContext> refs, ValidationErrors errors, RelationshipType? existing, CancellationToken cancellationToken)
    {
        ChildSync.RequireUnique(payload.ExternalCodes, c => (c.Provider, c.Code), "externalCodes", errors);
        foreach (var (code, i) in payload.ExternalCodes.Select((c, i) => (c, i)))
        {
            errors.Required($"externalCodes[{i}].provider", code.Provider);
            errors.Required($"externalCodes[{i}].code", code.Code);
        }

        // A provider code may point to one relationship type only, or translation would be ambiguous.
        var others = await refs.Context.RelationshipTypes.AsNoTracking().Where(t => t.Code != entity.Code).ToListAsync(cancellationToken);
        foreach (var code in payload.ExternalCodes)
        {
            var clash = others.FirstOrDefault(t => t.ExternalCodes.Any(c => c.Provider == code.Provider && c.Code == code.Code));
            if (clash is not null)
            {
                errors.Add("externalCodes", $"{code.Provider} code '{code.Code}' is already mapped to '{clash.Code}'.");
            }
        }

        entity.CanAct = payload.CanAct;
        entity.DisplayOrder = payload.DisplayOrder;
        entity.ExternalCodes = payload.ExternalCodes;
    }

    protected override Task<RelationshipTypePayload> ToPayloadAsync(PartiesDbContext context, RelationshipType entity, CancellationToken cancellationToken) =>
        Task.FromResult(new RelationshipTypePayload { CanAct = entity.CanAct, DisplayOrder = entity.DisplayOrder, ExternalCodes = entity.ExternalCodes });

    protected override async Task BeforeDeleteAsync(PartiesDbContext context, RelationshipType entity, CancellationToken cancellationToken)
    {
        if (await context.Relationships.AnyAsync(r => r.RelationshipTypeId == entity.Id, cancellationToken))
        {
            throw new ChangeSetException($"Relationship type '{entity.Code}' is in use; deactivate it instead.");
        }
    }
}

public sealed class ProfileFieldPayload : ConfigPayload
{
    public PartyKind PartyKind { get; set; }

    public ProfileDataType DataType { get; set; }

    public string? OperationCode { get; set; }

    public string? OutputName { get; set; }

    public List<IdentifierBinding> Inputs { get; set; } = [];

    public int RefreshHours { get; set; } = 24;

    public int DisplayOrder { get; set; }
}

internal sealed class ProfileFieldApplier(PartiesDbContext db) : PartyApplier<ProfileField, ProfileFieldPayload>(db)
{
    public override string EntityType => PartyChangeTypes.ProfileField;

    protected override IQueryable<ProfileField> Query(PartiesDbContext context) => context.ProfileFields;

    protected override IQueryable<ProfileField> ListQuery(PartiesDbContext context, string? parent) =>
        Query(context).OrderBy(a => a.PartyKind).ThenBy(a => a.DisplayOrder).ThenBy(a => a.Code);

    protected override Task ApplyAsync(ProfileField entity, ProfileFieldPayload payload, ConfigRefs<PartiesDbContext> refs, ValidationErrors errors, ProfileField? existing, CancellationToken cancellationToken)
    {
        var operation = Blank(payload.OperationCode);
        if (operation is not null)
        {
            errors.Code("operationCode", operation, ValidationErrors.IdentifierCode());
            errors.Required("outputName", payload.OutputName);
            if (payload.Inputs.Count == 0)
            {
                errors.Add("inputs", "Say which identifier is passed to the operation.");
            }
        }

        var allowed = payload.PartyKind == PartyKind.Individual ? IdentifierTypes.Individual : IdentifierTypes.Establishment;
        foreach (var (input, i) in payload.Inputs.Select((b, i) => (b, i)))
        {
            errors.Required($"inputs[{i}].parameter", input.Parameter);
            if (!allowed.Contains(input.IdentifierType))
            {
                errors.Add($"inputs[{i}].identifierType", $"Use one of {string.Join(", ", allowed)}.");
            }
        }

        if (payload.RefreshHours is < 0 or > 24 * 365)
        {
            errors.Add("refreshHours", "Must be between 0 and 8760.");
        }

        entity.PartyKind = payload.PartyKind;
        entity.DataType = payload.DataType;
        entity.OperationCode = operation;
        entity.OutputName = operation is null ? null : Blank(payload.OutputName);
        entity.Inputs = operation is null ? [] : payload.Inputs;
        entity.RefreshHours = payload.RefreshHours;
        entity.DisplayOrder = payload.DisplayOrder;
        return Task.CompletedTask;
    }

    protected override Task<ProfileFieldPayload> ToPayloadAsync(PartiesDbContext context, ProfileField entity, CancellationToken cancellationToken) =>
        Task.FromResult(new ProfileFieldPayload
        {
            PartyKind = entity.PartyKind,
            DataType = entity.DataType,
            OperationCode = entity.OperationCode,
            OutputName = entity.OutputName,
            Inputs = entity.Inputs,
            RefreshHours = entity.RefreshHours,
            DisplayOrder = entity.DisplayOrder,
        });
}

public sealed class CredentialTypePayload : ConfigPayload
{
    public PartyKind PartyKind { get; set; }

    public string? IssuerAr { get; set; }

    public string? IssuerEn { get; set; }

    public bool HasExpiry { get; set; } = true;
}

internal sealed class CredentialTypeApplier(PartiesDbContext db) : PartyApplier<CredentialType, CredentialTypePayload>(db)
{
    public override string EntityType => PartyChangeTypes.CredentialType;

    protected override IQueryable<CredentialType> Query(PartiesDbContext context) => context.CredentialTypes;

    protected override Task ApplyAsync(CredentialType entity, CredentialTypePayload payload, ConfigRefs<PartiesDbContext> refs, ValidationErrors errors, CredentialType? existing, CancellationToken cancellationToken)
    {
        errors.MaxLength("issuerAr", payload.IssuerAr, 500);
        errors.MaxLength("issuerEn", payload.IssuerEn, 500);
        entity.PartyKind = payload.PartyKind;
        entity.IssuerAr = Blank(payload.IssuerAr);
        entity.IssuerEn = Blank(payload.IssuerEn);
        entity.HasExpiry = payload.HasExpiry;
        return Task.CompletedTask;
    }

    protected override Task<CredentialTypePayload> ToPayloadAsync(PartiesDbContext context, CredentialType entity, CancellationToken cancellationToken) =>
        Task.FromResult(new CredentialTypePayload { PartyKind = entity.PartyKind, IssuerAr = entity.IssuerAr, IssuerEn = entity.IssuerEn, HasExpiry = entity.HasExpiry });

    protected override async Task BeforeDeleteAsync(PartiesDbContext context, CredentialType entity, CancellationToken cancellationToken)
    {
        if (await context.Credentials.AnyAsync(c => c.CredentialTypeId == entity.Id, cancellationToken))
        {
            throw new ChangeSetException($"Credential type '{entity.Code}' is in use; deactivate it instead.");
        }
    }
}

public sealed class SanctionTypePayload : ConfigPayload
{
    public bool BlocksApplications { get; set; } = true;

    public int? DefaultDays { get; set; }
}

internal sealed class SanctionTypeApplier(PartiesDbContext db) : PartyApplier<SanctionType, SanctionTypePayload>(db)
{
    public override string EntityType => PartyChangeTypes.SanctionType;

    protected override IQueryable<SanctionType> Query(PartiesDbContext context) => context.SanctionTypes;

    protected override Task ApplyAsync(SanctionType entity, SanctionTypePayload payload, ConfigRefs<PartiesDbContext> refs, ValidationErrors errors, SanctionType? existing, CancellationToken cancellationToken)
    {
        if (payload.DefaultDays is < 1 or > 3650)
        {
            errors.Add("defaultDays", "Must be between 1 and 3650.");
        }

        entity.BlocksApplications = payload.BlocksApplications;
        entity.DefaultDays = payload.DefaultDays;
        return Task.CompletedTask;
    }

    protected override Task<SanctionTypePayload> ToPayloadAsync(PartiesDbContext context, SanctionType entity, CancellationToken cancellationToken) =>
        Task.FromResult(new SanctionTypePayload { BlocksApplications = entity.BlocksApplications, DefaultDays = entity.DefaultDays });

    protected override async Task BeforeDeleteAsync(PartiesDbContext context, SanctionType entity, CancellationToken cancellationToken)
    {
        if (await context.Sanctions.AnyAsync(s => s.SanctionTypeId == entity.Id, cancellationToken))
        {
            throw new ChangeSetException($"Sanction type '{entity.Code}' is in use; deactivate it instead.");
        }
    }
}

public sealed class ContextSourcePayload : ConfigPayload
{
    public string OperationCode { get; set; } = string.Empty;

    public string InputParameter { get; set; } = string.Empty;

    public string ItemsPath { get; set; } = string.Empty;

    public PartyKind PrincipalKind { get; set; } = PartyKind.Establishment;

    public string PrincipalIdentifierType { get; set; } = IdentifierTypes.CrNumber;

    public string IdentifierPath { get; set; } = string.Empty;

    public string NameArPath { get; set; } = string.Empty;

    public string? NameEnPath { get; set; }

    public string? RelationCodePath { get; set; }

    public string? RelationProvider { get; set; }

    public string? DefaultRelationshipCode { get; set; }

    public string? StatusPath { get; set; }

    public List<string> ActiveStatuses { get; set; } = [];

    public string? ValidFromPath { get; set; }

    public string? ValidToPath { get; set; }

    public string? ServicesPath { get; set; }

    public int RefreshMinutes { get; set; } = 60;

    public int DisplayOrder { get; set; }
}

internal sealed class ContextSourceApplier(PartiesDbContext db) : PartyApplier<ContextSource, ContextSourcePayload>(db)
{
    public override string EntityType => PartyChangeTypes.ContextSource;

    protected override IQueryable<ContextSource> Query(PartiesDbContext context) => context.ContextSources;

    protected override async Task ApplyAsync(ContextSource entity, ContextSourcePayload payload, ConfigRefs<PartiesDbContext> refs, ValidationErrors errors, ContextSource? existing, CancellationToken cancellationToken)
    {
        errors.Code("operationCode", payload.OperationCode, ValidationErrors.IdentifierCode());
        errors.Required("inputParameter", payload.InputParameter);
        Path(errors, "itemsPath", payload.ItemsPath, required: true);
        Path(errors, "identifierPath", payload.IdentifierPath, required: true);
        Path(errors, "nameArPath", payload.NameArPath, required: true);
        Path(errors, "nameEnPath", payload.NameEnPath);
        Path(errors, "relationCodePath", payload.RelationCodePath);
        Path(errors, "statusPath", payload.StatusPath);
        Path(errors, "validFromPath", payload.ValidFromPath);
        Path(errors, "validToPath", payload.ValidToPath);
        Path(errors, "servicesPath", payload.ServicesPath);

        var identifierTypes = payload.PrincipalKind == PartyKind.Individual ? IdentifierTypes.Individual : IdentifierTypes.Establishment;
        if (!identifierTypes.Contains(payload.PrincipalIdentifierType))
        {
            errors.Add("principalIdentifierType", $"Use one of {string.Join(", ", identifierTypes)}.");
        }

        if (Blank(payload.RelationCodePath) is not null)
        {
            errors.Required("relationProvider", payload.RelationProvider);
        }
        else if (Blank(payload.DefaultRelationshipCode) is null)
        {
            errors.Add("defaultRelationshipCode", "Give the relation code path or a default relationship type.");
        }

        var defaultType = Blank(payload.DefaultRelationshipCode);
        if (defaultType is not null)
        {
            await refs.ResolveAsync(refs.Context.RelationshipTypes, PartyChangeTypes.RelationshipType, defaultType, "defaultRelationshipCode", errors, cancellationToken);
        }

        if (payload.RefreshMinutes is < 1 or > 7 * 24 * 60)
        {
            errors.Add("refreshMinutes", "Must be between 1 and 10080.");
        }

        entity.OperationCode = payload.OperationCode;
        entity.InputParameter = payload.InputParameter.Trim();
        entity.ItemsPath = payload.ItemsPath.Trim();
        entity.PrincipalKind = payload.PrincipalKind;
        entity.PrincipalIdentifierType = payload.PrincipalIdentifierType;
        entity.IdentifierPath = payload.IdentifierPath.Trim();
        entity.NameArPath = payload.NameArPath.Trim();
        entity.NameEnPath = Blank(payload.NameEnPath);
        entity.RelationCodePath = Blank(payload.RelationCodePath);
        entity.RelationProvider = Blank(payload.RelationProvider);
        entity.DefaultRelationshipCode = defaultType;
        entity.StatusPath = Blank(payload.StatusPath);
        entity.ActiveStatuses = [.. payload.ActiveStatuses.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim())];
        entity.ValidFromPath = Blank(payload.ValidFromPath);
        entity.ValidToPath = Blank(payload.ValidToPath);
        entity.ServicesPath = Blank(payload.ServicesPath);
        entity.RefreshMinutes = payload.RefreshMinutes;
        entity.DisplayOrder = payload.DisplayOrder;
    }

    protected override Task<ContextSourcePayload> ToPayloadAsync(PartiesDbContext context, ContextSource entity, CancellationToken cancellationToken) =>
        Task.FromResult(new ContextSourcePayload
        {
            OperationCode = entity.OperationCode,
            InputParameter = entity.InputParameter,
            ItemsPath = entity.ItemsPath,
            PrincipalKind = entity.PrincipalKind,
            PrincipalIdentifierType = entity.PrincipalIdentifierType,
            IdentifierPath = entity.IdentifierPath,
            NameArPath = entity.NameArPath,
            NameEnPath = entity.NameEnPath,
            RelationCodePath = entity.RelationCodePath,
            RelationProvider = entity.RelationProvider,
            DefaultRelationshipCode = entity.DefaultRelationshipCode,
            StatusPath = entity.StatusPath,
            ActiveStatuses = entity.ActiveStatuses,
            ValidFromPath = entity.ValidFromPath,
            ValidToPath = entity.ValidToPath,
            ServicesPath = entity.ServicesPath,
            RefreshMinutes = entity.RefreshMinutes,
            DisplayOrder = entity.DisplayOrder,
        });
}
