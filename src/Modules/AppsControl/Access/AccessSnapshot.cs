using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Platform.Configuration;
using DynamicCube.Platform.Security;

namespace DynamicCube.Modules.AppsControl.Access;

/// <summary>Everything Apps Control decided about the current user for one app; cached briefly.</summary>
public sealed class AccessSnapshot(
    UserContext user,
    string? nameAr,
    string? nameEn,
    UserStatus status,
    string? preferredLanguage,
    IReadOnlyList<Guid> roleIds,
    IReadOnlyDictionary<string, IReadOnlySet<string>> privileges,
    ConfigScope? scope = null,
    IReadOnlyList<RoleSource>? roleSources = null,
    IReadOnlyList<string>? companies = null,
    bool companyChosen = true,
    IReadOnlyList<string>? branches = null,
    IReadOnlyList<RoleHolding>? holdings = null)
{
    /// <summary>The branches of <see cref="Company"/> the user works in (ADR-0036 step 6); null for every branch.</summary>
    public IReadOnlyList<string>? Branches { get; } = branches;

    /// <summary>The roles in force in <see cref="Company"/> with the company and branches each is held in.</summary>
    public IReadOnlyList<RoleHolding> Holdings { get; } = holdings ?? [];

    /// <summary>
    /// The company the user works in now (ADR-0036 step 5): the one the request names, else the one they last chose, else
    /// their first; null when they may work in none. Roles, personal privileges and lists apply for this company.
    /// </summary>
    public string? Company { get; } = user.Company;

    /// <summary>The active companies the user may work in (their roles or personal privileges open them), in order.</summary>
    public IReadOnlyList<string> Companies { get; } = companies ?? [];

    /// <summary>False when the user may work in several companies and has not chosen one yet (the switcher then asks).</summary>
    public bool CompanyChosen { get; } = companyChosen;

    /// <summary>
    /// Whose contents each held role contributes: the role's own (no company) or a company's override (ADR-0036), e.g.
    /// for the data permissions of lists of values.
    /// </summary>
    public IReadOnlyList<RoleSource> RoleSources { get; } = roleSources ?? [.. roleIds.Select(id => new RoleSource(id, null))];

    /// <summary>The entities and business apps whose configuration and data the user works with.</summary>
    public ConfigScope Scope { get; } = scope ?? ConfigScope.Everything;

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
        new(UserContext.Anonymous(app), null, null, UserStatus.Active, null, [], new Dictionary<string, IReadOnlySet<string>>(), new ConfigScope(new HashSet<string>(), new HashSet<string>()));
}

/// <summary>A role the user holds and the company whose override of it applies (none: the role's own contents).</summary>
public sealed record RoleSource(Guid RoleId, string? CompanyCode);
