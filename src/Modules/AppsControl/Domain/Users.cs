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

    public List<UserAssignment> Assignments { get; } = [];

    public List<UserRole> Roles { get; } = [];
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

/// <summary>A role held by a user, optionally only within an org unit (e.g. analyst in one branch).</summary>
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
}
