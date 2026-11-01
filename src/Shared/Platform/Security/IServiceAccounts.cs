namespace DynamicCube.Platform.Security;

/// <summary>A service account as other modules see it: a user of Apps Control that is no person and cannot sign in.</summary>
public sealed record ServiceAccountInfo(string Code, string NameAr, string NameEn, bool IsActive);

/// <summary>Where a service account holds a role: a company (or <c>*</c> for all of them) and some of its branches or all of them (<c>*</c>).</summary>
public sealed record ServiceAccountHolding(string Company, IReadOnlyList<string> Branches);

/// <summary>
/// Service accounts (ADR-0042, implemented by Apps Control): user accounts of kind SERVICE that systems act as — e.g. each
/// API client's. They hold roles, privileges and data grants like any user, are kept on the Users screen, and never sign in.
/// </summary>
public interface IServiceAccounts
{
    /// <summary>The service account with that username; null when there is none (or the username is a person's).</summary>
    Task<ServiceAccountInfo?> FindAsync(string code, CancellationToken cancellationToken);

    /// <summary>Whether the username belongs to a person's account.</summary>
    Task<bool> IsPersonAsync(string code, CancellationToken cancellationToken);

    /// <summary>
    /// Creates the service account when it does not exist yet, with no roles (called from a publish hook, inside the change
    /// set's transaction); an existing one is left as it is.
    /// </summary>
    Task EnsureAsync(string code, string nameAr, string nameEn, CancellationToken cancellationToken);

    /// <summary>
    /// Gives the account a role of its own that opens the resources (VIEW), held where given — used once, to move an API
    /// client's earlier endpoints, companies and branches to its account. The role is created, or brought in line.
    /// </summary>
    Task GiveRoleAsync(string code, string roleCode, string roleNameAr, string roleNameEn, IReadOnlyList<string> resources, IReadOnlyList<ServiceAccountHolding> holdings, CancellationToken cancellationToken);
}
