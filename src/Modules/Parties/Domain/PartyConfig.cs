using DynamicCube.Platform.Parties;

namespace DynamicCube.Modules.Parties.Domain;

/// <summary>A provider's code for a configuration value, e.g. MCI relation type <c>1</c> = OWNER.</summary>
public sealed class ExternalCode
{
    public string Provider { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
}

/// <summary>Owner, partner, manager, delegate…; <see cref="CanAct"/> decides whether it gives an acting context.</summary>
public sealed class RelationshipType : ConfigEntity
{
    public bool CanAct { get; set; }

    public int DisplayOrder { get; set; }

    public List<ExternalCode> ExternalCodes { get; set; } = [];
}

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

public sealed class CredentialType : ConfigEntity
{
    public PartyKind PartyKind { get; set; }

    public string? IssuerAr { get; set; }

    public string? IssuerEn { get; set; }

    public bool HasExpiry { get; set; } = true;
}

public sealed class SanctionType : ConfigEntity
{
    /// <summary>The party may not submit new requests while the sanction is active.</summary>
    public bool BlocksApplications { get; set; } = true;

    public int? DefaultDays { get; set; }
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
