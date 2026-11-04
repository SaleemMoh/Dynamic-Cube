using DynamicCube.Modules.Parties.Domain;
using DynamicCube.Modules.Parties.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.DataDictionary;
using DynamicCube.Platform.Parties;

namespace DynamicCube.Modules.Parties.Changes;

public sealed class AuthorityTypePayload : ConfigPayload
{
    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    public string RelationshipCode { get; set; } = string.Empty;

    public PartyKind PrincipalKind { get; set; } = PartyKind.Establishment;

    public string PrincipalIdentifierType { get; set; } = IdentifierTypes.CrNumber;

    public string NumberLabelAr { get; set; } = string.Empty;

    public string NumberLabelEn { get; set; } = string.Empty;

    public bool RequiresExpiry { get; set; }

    public List<string> Services { get; set; } = [];

    public string? OperationCode { get; set; }

    public List<AuthorityInput> Inputs { get; set; } = [];

    public string? ValidOutput { get; set; }

    public string? ExpiryOutput { get; set; }

    public string? PrincipalNameArOutput { get; set; }

    public string? PrincipalNameEnOutput { get; set; }

    public int DisplayOrder { get; set; }
}

/// <summary>Authority types: the relationship they record (one that allows acting), the firm's identifier and the check.</summary>
internal sealed class AuthorityTypeApplier(PartiesDbContext db, IDataDictionaryReader lookups) : PartyApplier<AuthorityType, AuthorityTypePayload>(db)
{
    public override string EntityType => PartyChangeTypes.AuthorityType;

    protected override IQueryable<AuthorityType> Query(PartiesDbContext context) => context.AuthorityTypes;

    protected override IQueryable<AuthorityType> ListQuery(PartiesDbContext context, string? parent) =>
        Query(context).OrderBy(t => t.DisplayOrder).ThenBy(t => t.Code);

    protected override async Task ApplyAsync(AuthorityType entity, AuthorityTypePayload payload, ConfigRefs<PartiesDbContext> refs, ValidationErrors errors, AuthorityType? existing, CancellationToken cancellationToken)
    {
        errors.MaxLength("descriptionAr", payload.DescriptionAr, 2000);
        errors.MaxLength("descriptionEn", payload.DescriptionEn, 2000);
        errors.Required("numberLabelAr", payload.NumberLabelAr);
        errors.Required("numberLabelEn", payload.NumberLabelEn);
        errors.Required("relationshipCode", payload.RelationshipCode);
        var relation = Blank(payload.RelationshipCode);
        if (relation is not null)
        {
            var type = await lookups.FindAsync(PartyClasses.RelationshipType, relation, cancellationToken);
            if (type is null)
            {
                errors.Add("relationshipCode", $"Relationship type '{relation}' does not exist.");
            }
            else if (!type.Flag(PartyClasses.CanAct))
            {
                errors.Add("relationshipCode", $"Relationship type '{relation}' does not allow acting for the party.");
            }
        }

        var identifierTypes = payload.PrincipalKind == PartyKind.Individual ? IdentifierTypes.Individual : IdentifierTypes.Establishment;
        if (!identifierTypes.Contains(payload.PrincipalIdentifierType))
        {
            errors.Add("principalIdentifierType", $"Use one of {string.Join(", ", identifierTypes)}.");
        }

        var operation = Blank(payload.OperationCode);
        if (operation is null)
        {
            if (payload.Inputs.Count > 0 || Blank(payload.ValidOutput) is not null || Blank(payload.ExpiryOutput) is not null)
            {
                errors.Add("operationCode", "Inputs and outputs need the operation that checks the authority.");
            }
        }
        else
        {
            errors.Code("operationCode", operation, ValidationErrors.IdentifierCode());
            foreach (var (input, i) in payload.Inputs.Select((x, i) => (x, i)))
            {
                errors.Required($"inputs[{i}].parameter", input.Parameter);
            }

            foreach (var duplicate in payload.Inputs.GroupBy(x => x.Parameter).Where(g => g.Count() > 1))
            {
                errors.Add("inputs", $"Parameter '{duplicate.Key}' appears twice.");
            }
        }

        entity.DescriptionAr = Blank(payload.DescriptionAr);
        entity.DescriptionEn = Blank(payload.DescriptionEn);
        entity.RelationshipCode = relation ?? string.Empty;
        entity.PrincipalKind = payload.PrincipalKind;
        entity.PrincipalIdentifierType = payload.PrincipalIdentifierType;
        entity.NumberLabelAr = payload.NumberLabelAr.Trim();
        entity.NumberLabelEn = payload.NumberLabelEn.Trim();
        entity.RequiresExpiry = payload.RequiresExpiry;
        entity.Services = [.. payload.Services.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct()];
        entity.OperationCode = operation;
        entity.Inputs = [.. payload.Inputs.Select(x => new AuthorityInput { Parameter = x.Parameter.Trim(), From = x.From })];
        entity.ValidOutput = Blank(payload.ValidOutput);
        entity.ExpiryOutput = Blank(payload.ExpiryOutput);
        entity.PrincipalNameArOutput = Blank(payload.PrincipalNameArOutput);
        entity.PrincipalNameEnOutput = Blank(payload.PrincipalNameEnOutput);
        entity.DisplayOrder = payload.DisplayOrder;
    }

    protected override Task<AuthorityTypePayload> ToPayloadAsync(PartiesDbContext context, AuthorityType entity, CancellationToken cancellationToken) =>
        Task.FromResult(new AuthorityTypePayload
        {
            DescriptionAr = entity.DescriptionAr,
            DescriptionEn = entity.DescriptionEn,
            RelationshipCode = entity.RelationshipCode,
            PrincipalKind = entity.PrincipalKind,
            PrincipalIdentifierType = entity.PrincipalIdentifierType,
            NumberLabelAr = entity.NumberLabelAr,
            NumberLabelEn = entity.NumberLabelEn,
            RequiresExpiry = entity.RequiresExpiry,
            Services = entity.Services,
            OperationCode = entity.OperationCode,
            Inputs = entity.Inputs,
            ValidOutput = entity.ValidOutput,
            ExpiryOutput = entity.ExpiryOutput,
            PrincipalNameArOutput = entity.PrincipalNameArOutput,
            PrincipalNameEnOutput = entity.PrincipalNameEnOutput,
            DisplayOrder = entity.DisplayOrder,
        });
}
