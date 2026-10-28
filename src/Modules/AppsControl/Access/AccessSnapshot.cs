using DynamicCube.Modules.AppsControl.Domain;

namespace DynamicCube.Modules.AppsControl.Access;

/// <summary>Everything Apps Control decided about the current user for one app; cached briefly.</summary>
public sealed class AccessSnapshot(
    UserContext user,
    string? nameAr,
    string? nameEn,
    UserStatus status,
    string? preferredLanguage,
    IReadOnlyList<Guid> roleIds,
    IReadOnlyDictionary<string, IReadOnlySet<string>> privileges)
{
    public UserContext User { get; } = user;

    public string? NameAr { get; } = nameAr;

    public string? NameEn { get; } = nameEn;

    public UserStatus Status { get; } = status;

    public string? PreferredLanguage { get; } = preferredLanguage;

    public IReadOnlyList<Guid> RoleIds { get; } = roleIds;

    /// <summary>Resource code → granted actions.</summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> Privileges { get; } = privileges;

    public bool IsActive => Status == UserStatus.Active;

    public bool Can(string resourceCode, string action) =>
        IsActive && Privileges.TryGetValue(resourceCode, out var actions) && actions.Contains(action);

    public static AccessSnapshot Anonymous(string app) =>
        new(UserContext.Anonymous(app), null, null, UserStatus.Active, null, [], new Dictionary<string, IReadOnlySet<string>>());
}
