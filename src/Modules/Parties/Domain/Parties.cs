using DynamicCube.Platform.Parties;
using DynamicCube.Platform.Persistence;

namespace DynamicCube.Modules.Parties.Domain;

public enum PartyStatus
{
    Active,
    Inactive,
}

/// <summary>Where a record came from; integration records are replaced on refresh, manual ones are kept.</summary>
public enum RecordSource
{
    Manual,
    Integration,
    SignIn,
}

/// <summary>Identifier types; the first identifier of each party is its primary one.</summary>
public static class IdentifierTypes
{
    public const string NationalId = "NATIONAL_ID";
    public const string Iqama = "IQAMA";
    public const string GccId = "GCC_ID";
    public const string Passport = "PASSPORT";
    public const string CrNumber = "CR_NUMBER";
    public const string UnifiedNumber = "UNIFIED_NUMBER";

    public static IReadOnlyList<string> Individual { get; } = [NationalId, Iqama, GccId, Passport];

    public static IReadOnlyList<string> Establishment { get; } = [CrNumber, UnifiedNumber];

    /// <summary>Saudi ID numbers: 1… citizens, 2… residents.</summary>
    public static string ForPersonNumber(string number) => number.StartsWith('2') ? Iqama : NationalId;

    public static bool IsValid(string type, string value) => type switch
    {
        NationalId => value.Length == 10 && value[0] == '1' && value.All(char.IsAsciiDigit),
        Iqama => value.Length == 10 && value[0] == '2' && value.All(char.IsAsciiDigit),
        CrNumber => value.Length == 10 && value.All(char.IsAsciiDigit),
        UnifiedNumber => value.Length == 10 && value[0] == '7' && value.All(char.IsAsciiDigit),
        GccId or Passport => value.Length is >= 5 and <= 20 && value.All(char.IsAsciiLetterOrDigit),
        _ => false,
    };
}

/// <summary>An individual or an establishment that applies for or holds something.</summary>
public sealed class Party : IAuditable, IHasAuditColumns
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public PartyKind Kind { get; set; }

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public PartyStatus Status { get; set; } = PartyStatus.Active;

    public RecordSource Source { get; set; }

    /// <summary>When the acting contexts of this individual were last fetched from the context sources.</summary>
    public DateTimeOffset? ContextsRefreshedAt { get; set; }

    public List<PartyIdentifier> Identifiers { get; } = [];

    /// <summary>The identifier the party was registered with (national ID, CR number…).</summary>
    public PartyIdentifier PrimaryIdentifier => Identifiers.FirstOrDefault(i => i.IsPrimary) ?? Identifiers[0];

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}

public sealed class PartyIdentifier : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid PartyId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }
}

/// <summary>
/// An individual's right to act for a party (owner, partner, manager, delegate…), optionally limited to some services and
/// to a validity period. Integration-sourced relationships are refreshed from the context sources.
/// </summary>
public sealed class Relationship : IAuditable, IHasAuditColumns
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid IndividualId { get; set; }

    public Party? Individual { get; set; }

    public Guid PrincipalId { get; set; }

    public Party? Principal { get; set; }

    /// <summary>A value of class <c>PARTY_RELATIONSHIP_TYPE</c>.</summary>
    public string RelationshipCode { get; set; } = string.Empty;

    /// <summary>Service codes the relationship covers; empty means all services.</summary>
    public List<string> Services { get; set; } = [];

    public DateOnly? ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public RecordSource Source { get; set; }

    /// <summary>Context source code or delegation reference.</summary>
    public string? SourceReference { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActiveOn(DateOnly day) => EndedAt is null && (ValidFrom is null || ValidFrom <= day) && (ValidTo is null || ValidTo >= day);
}

public sealed class ProfileValue : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid PartyId { get; set; }

    public Guid FieldId { get; set; }

    public ProfileField? Field { get; set; }

    /// <summary>JSON value.</summary>
    public string? Value { get; set; }

    public RecordSource Source { get; set; }

    public DateTimeOffset RefreshedAt { get; set; }
}

public enum CredentialStatus
{
    Active,
    Suspended,
    Expired,
    Cancelled,
}

/// <summary>A record issued by another authority (e.g. DGA vendor technical certificate, SCE license), with nested data.</summary>
public sealed class Credential : IAuditable, IHasAuditColumns
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid PartyId { get; set; }

    /// <summary>A value of class <c>PARTY_CREDENTIAL_TYPE</c>.</summary>
    public string CredentialTypeCode { get; set; } = string.Empty;

    public string Number { get; set; } = string.Empty;

    public DateOnly? IssuedOn { get; set; }

    public DateOnly? ExpiresOn { get; set; }

    public CredentialStatus Status { get; set; }

    /// <summary>JSON details (levels, categories, activities…).</summary>
    public string? Data { get; set; }

    public RecordSource Source { get; set; }

    public DateTimeOffset? RefreshedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}

public enum ProjectStatus
{
    Ongoing,
    Completed,
    Cancelled,
}

/// <summary>A project of the party's portfolio, reused across requests and renewals.</summary>
public sealed class Project : IAuditable, IHasAuditColumns
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid PartyId { get; set; }

    public string? Number { get; set; }

    public string NameAr { get; set; } = string.Empty;

    public string? NameEn { get; set; }

    public string? ClientName { get; set; }

    public decimal? ContractValue { get; set; }

    public DateOnly? StartOn { get; set; }

    public DateOnly? EndOn { get; set; }

    public ProjectStatus Status { get; set; }

    public string? Data { get; set; }

    public RecordSource Source { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}

/// <summary>A ban or penalty recorded against a party; checked by eligibility rules.</summary>
public sealed class Sanction : IAuditable, IHasAuditColumns
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid PartyId { get; set; }

    /// <summary>A value of class <c>PARTY_SANCTION_TYPE</c>.</summary>
    public string SanctionTypeCode { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public DateOnly StartsOn { get; set; }

    public DateOnly? EndsOn { get; set; }

    public DateTimeOffset? LiftedAt { get; set; }

    public string? LiftedBy { get; set; }

    public string? LiftReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActiveOn(DateOnly day) => LiftedAt is null && StartsOn <= day && (EndsOn is null || EndsOn >= day);
}
