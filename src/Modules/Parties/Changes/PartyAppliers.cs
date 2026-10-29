using DynamicCube.Modules.Parties.Domain;
using DynamicCube.Modules.Parties.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Integration;
using DynamicCube.Platform.Lookups;
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

internal sealed class ContextSourceApplier(PartiesDbContext db, ILookupDirectory lookups) : PartyApplier<ContextSource, ContextSourcePayload>(db)
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
        if (defaultType is not null && await lookups.FindAsync(PartyClasses.RelationshipType, defaultType, cancellationToken) is null)
        {
            errors.Add("defaultRelationshipCode", $"Relationship type '{defaultType}' does not exist.");
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
