using DynamicCube.Platform.Persistence;

namespace DynamicCube.Modules.AppsControl.Domain;

public enum UserStatus
{
    Active,
    Locked,
    Disabled,
}

/// <summary>
/// An employee known to Apps Control. Authentication stays in Keycloak; the account is linked by <see cref="IdpSubject"/>
/// and created on first sign-in. <see cref="ConfigEntity.Code"/> holds the username.
/// </summary>
public sealed class UserAccount : ConfigEntity
{
    public string? IdpSubject { get; set; }

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

    /// <summary>Screens, actions and data given to this person alone (ADR-0036), besides those of their roles.</summary>
    public List<UserPrivilege> Privileges { get; } = [];
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
