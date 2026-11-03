using DynamicCube.Platform.Parties;

namespace DynamicCube.Modules.Parties.Domain;

public enum ProfileDataType
{
    Text,
    Number,
    Date,
    Bool,
    Json,
}

/// <summary>Passes a party identifier to an operation parameter.</summary>
public sealed class IdentifierBinding
{
    public string Parameter { get; set; } = string.Empty;

    public string IdentifierType { get; set; } = string.Empty;
}

/// <summary>
/// A refreshable profile attribute (employee count, Saudization, capital, region…). Integration attributes are read from
/// an operation output; values older than <see cref="RefreshHours"/> are fetched again.
/// </summary>
public sealed class ProfileField : ConfigEntity
{
    public PartyKind PartyKind { get; set; }

    public ProfileDataType DataType { get; set; }

    public string? OperationCode { get; set; }

    public string? OutputName { get; set; }

    public List<IdentifierBinding> Inputs { get; set; } = [];

    public int RefreshHours { get; set; } = 24;

    public int DisplayOrder { get; set; }
}

/// <summary>
/// Where an individual's acting contexts come from: an operation called with the individual's identifier whose result
/// lists the parties they may represent — e.g. MCI <c>cr-information-id-number</c> (establishments with a relation type)
/// or NIC active delegations (delegators).
/// </summary>
public sealed class ContextSource : ConfigEntity
{
    public string OperationCode { get; set; } = string.Empty;

    /// <summary>Operation parameter that receives the individual's ID number.</summary>
    public string InputParameter { get; set; } = string.Empty;

    /// <summary>The list of items in the result, e.g. <c>Licenses</c>.</summary>
    public string ItemsPath { get; set; } = string.Empty;

    public PartyKind PrincipalKind { get; set; } = PartyKind.Establishment;

    /// <summary>Identifier type of the principals, e.g. <c>CR_NUMBER</c>; for individuals it is derived from the number.</summary>
    public string PrincipalIdentifierType { get; set; } = IdentifierTypes.CrNumber;

    public string IdentifierPath { get; set; } = string.Empty;

    public string NameArPath { get; set; } = string.Empty;

    public string? NameEnPath { get; set; }

    /// <summary>The provider's relation code, translated through the relationship types' external codes.</summary>
    public string? RelationCodePath { get; set; }

    /// <summary>Provider of <see cref="RelationCodePath"/> codes, e.g. <c>MCI</c>.</summary>
    public string? RelationProvider { get; set; }

    /// <summary>Relationship type when the source has no relation code (e.g. <c>DELEGATE</c> for delegations).</summary>
    public string? DefaultRelationshipCode { get; set; }

    public string? StatusPath { get; set; }

    /// <summary>Item statuses that count (e.g. <c>Active</c>); empty accepts every item.</summary>
    public List<string> ActiveStatuses { get; set; } = [];

    public string? ValidFromPath { get; set; }

    public string? ValidToPath { get; set; }

    /// <summary>Service references of the item (delegations limited to services).</summary>
    public string? ServicesPath { get; set; }

    /// <summary>Contexts are fetched again when older than this.</summary>
    public int RefreshMinutes { get; set; } = 60;

    public int DisplayOrder { get; set; }
}

/// <summary>What an authority check reads: the number typed, the firm's identifier, the applicant's ID number or the expiry.</summary>
public enum AuthorityInputSource
{
    Number,
    Principal,
    Individual,
    Expiry,
}

/// <summary>Passes one value of an authority to the check operation's parameter.</summary>
public sealed class AuthorityInput
{
    public string Parameter { get; set; } = string.Empty;

    public AuthorityInputSource From { get; set; }
}

/// <summary>
/// A document that lets an individual act for a firm they do not own (a municipal authorization, a notarised agency…):
/// the applicant gives its number (and expiry), it is checked through an operation when one is set, and a dated
/// relationship of <see cref="RelationshipCode"/> is recorded, so the firm becomes one of their acting contexts.
/// </summary>
public sealed class AuthorityType : ConfigEntity
{
    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    /// <summary>The relationship type recorded (a value of class <c>PARTY_RELATIONSHIP_TYPE</c> that allows acting).</summary>
    public string RelationshipCode { get; set; } = string.Empty;

    public PartyKind PrincipalKind { get; set; } = PartyKind.Establishment;

    public string PrincipalIdentifierType { get; set; } = IdentifierTypes.CrNumber;

    public string NumberLabelAr { get; set; } = string.Empty;

    public string NumberLabelEn { get; set; } = string.Empty;

    public bool RequiresExpiry { get; set; }

    /// <summary>Services it covers (empty: all).</summary>
    public List<string> Services { get; set; } = [];

    /// <summary>The operation that checks the authority; without one it is recorded as given.</summary>
    public string? OperationCode { get; set; }

    public List<AuthorityInput> Inputs { get; set; } = [];

    /// <summary>An output that must be true (or the call must succeed when not set).</summary>
    public string? ValidOutput { get; set; }

    /// <summary>An output giving the expiry date the provider knows (used instead of the typed one).</summary>
    public string? ExpiryOutput { get; set; }

    /// <summary>Outputs naming the firm, to register it when it is not known yet.</summary>
    public string? PrincipalNameArOutput { get; set; }

    public string? PrincipalNameEnOutput { get; set; }

    public int DisplayOrder { get; set; }
}
