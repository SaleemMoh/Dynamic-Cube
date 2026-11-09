using DynamicCube.Platform.Persistence;

namespace DynamicCube.Modules.AppsControl.Domain;

public enum UserStatus
{
    Active,
    Locked,
    Disabled,
}

/// <summary>Who an account is: a person who signs in, or a system that acts as the account (ADR-0042).</summary>
public enum UserKind
{
    Person,

    /// <summary>
    /// A service account, e.g. an API client's: it holds roles, privileges and data grants like a person, but never signs in
    /// and has no sign-in account.
    /// </summary>
    Service,
}

/// <summary>
/// An employee known to Apps Control. Authentication stays in Keycloak; the account is linked by <see cref="IdpSubject"/>
/// and created on first sign-in. <see cref="ConfigEntity.Code"/> holds the username.
/// </summary>
public sealed class UserAccount : ConfigEntity
{
    public string? IdpSubject { get; set; }

    /// <summary>A person, or a service account that systems act as (it never signs in).</summary>
    public UserKind Kind { get; set; } = UserKind.Person;

    public string AuthSource { get; set; } = "IDP";

    public string? Email { get; set; }

    public string? Mobile { get; set; }

    public string? NationalId { get; set; }

    public UserStatus Status { get; set; } = UserStatus.Active;

    public string? PreferredLanguage { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>
    /// The company the person last chose to work in (ADR-0036 step 5): used when a request names none. Kept by the
    /// workspace switcher, not by change sets.
    /// </summary>
    public string? LastCompanyCode { get; set; }

    public List<UserAssignment> Assignments { get; } = [];

    public List<UserRole> Roles { get; } = [];

    /// <summary>The organizations the person may enter, one of them the default (legacy <c>SYS_USERINSTANCES</c>).</summary>
    public List<UserInstance> Instances { get; } = [];

    /// <summary>
    /// The companies the person may enter, with the default one of each organization (legacy
    /// <c>SYS_USERINSTANCES_COMPANIES</c>). Holding a role in a company also opens it.
    /// </summary>
    public List<UserCompany> Companies { get; } = [];

    /// <summary>Screens, actions and data given to this person alone (ADR-0036), besides those of their roles.</summary>
    public List<UserPrivilege> Privileges { get; } = [];
}

/// <summary>An organization (legacy instance) a user may enter (legacy <c>SYS_USERINSTANCES</c>).</summary>
public sealed class UserInstance : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    /// <summary>The organization's code (<c>instid</c>).</summary>
    public string EntityCode { get; set; } = string.Empty;

    /// <summary>The organization the person starts in (legacy <c>DEFAULTINSTFLAG</c>); one per person.</summary>
    public bool IsDefault { get; set; }
}

/// <summary>A company a user may enter (legacy <c>SYS_USERINSTANCES_COMPANIES</c>).</summary>
public sealed class UserCompany : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    /// <summary>The company's organization (<c>instid</c>).</summary>
    public string EntityCode { get; set; } = string.Empty;

    /// <summary>The company's number in its organization (<c>cmp_num</c>).</summary>
    public string CompanyCode { get; set; } = string.Empty;

    /// <summary>The company the person starts in within its organization (legacy <c>DEFAULT_CMP_FLAG</c>); one per organization.</summary>
    public bool IsDefault { get; set; }

    /// <summary>The person's default currency in this company (legacy <c>DEFAULT_CURRENCY</c>); optional.</summary>
    public string? DefaultCurrency { get; set; }

    /// <summary>The company's key, <c>instid/cmp_num</c>.</summary>
    public string CompanyKey => Domain.CompanyKey.Format(EntityCode, CompanyCode);
}

/// <summary>Placement of a user in an org unit with a position.</summary>
public sealed class UserAssignment : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public Guid OrgUnitId { get; set; }

    public OrgUnit? OrgUnit { get; set; }

    public Guid? PositionId { get; set; }

    public Position? Position { get; set; }

    public bool IsPrimary { get; set; }

    public DateOnly? EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }
}

/// <summary>
/// A role held by a user in a company, or in all of them (<see cref="CompanyScope.All"/>), and in some of its branches or all
/// of them, optionally only within an org unit. "All" is always chosen explicitly (ADR-0036): an empty choice is not valid.
/// </summary>
public sealed class UserRole : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public Guid RoleId { get; set; }

    public Role? Role { get; set; }

    public Guid? OrgUnitId { get; set; }

    public OrgUnit? OrgUnit { get; set; }

    public DateOnly? EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    /// <summary>The company the role is held in, or <see cref="CompanyScope.All"/>.</summary>
    public string CompanyCode { get; set; } = CompanyScope.All;

    /// <summary>Branches of that company the role is held in, or just <see cref="CompanyScope.All"/>.</summary>
    public List<string> Branches { get; set; } = [CompanyScope.All];
}

/// <summary>
/// Actions on a screen, and data on it, given to one person in a company or all of them (<see cref="CompanyScope.All"/>),
/// besides what their roles give; or actions their roles give that this person must not have. Optionally for a period.
/// </summary>
public sealed class UserPrivilege : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public string CompanyCode { get; set; } = CompanyScope.All;

    public Guid ResourceId { get; set; }

    public Resource? Resource { get; set; }

    /// <summary>Actions on the screen; may be empty when the person only receives data on it.</summary>
    public List<string> Actions { get; set; } = [];

    /// <summary>Actions taken away from this person on the screen although their roles give them (they win over any grant).</summary>
    public List<string> DeniedActions { get; set; } = [];

    /// <summary>First day the personal privilege holds (none: already).</summary>
    public DateOnly? EffectiveFrom { get; set; }

    /// <summary>Last day the personal privilege holds (none: until removed).</summary>
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>Values of the screen's lists the person may see, besides those of their roles.</summary>
    public List<UserPrivilegeGrant> Grants { get; } = [];
}

/// <summary>Values of a list (LOV) one person may see on the screen of a personal privilege: all, or some.</summary>
public sealed class UserPrivilegeGrant : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserPrivilegeId { get; set; }

    public Guid LovId { get; set; }

    public Lov? Lov { get; set; }

    /// <summary><see cref="GrantType.All"/> or <see cref="GrantType.Values"/>.</summary>
    public GrantType GrantType { get; set; }

    public List<string> GrantedValues { get; set; } = [];
}
