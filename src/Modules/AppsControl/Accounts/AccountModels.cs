namespace DynamicCube.Modules.AppsControl.Accounts;

/// <summary>Where employee accounts live and who maintains them.</summary>
public sealed class IdentityOptions
{
    public const string SectionName = "AppsControl:Identity";

    /// <summary><c>Keycloak</c> to maintain accounts through its admin API; <c>None</c> (default) when another team or system does.</summary>
    public string Provider { get; init; } = "None";

    /// <summary>
    /// <c>Managed</c>: Apps Control creates accounts and sets passwords (local accounts). <c>Federated</c>: accounts come from a
    /// directory or identity provider (Active Directory, LDAP, Entra ID, Nafath); Apps Control only enables and disables them.
    /// </summary>
    public string Mode { get; init; } = "Managed";

    /// <summary>Keycloak address, e.g. <c>https://sso.example.gov.sa</c>.</summary>
    public string? BaseUrl { get; init; }

    public string Realm { get; init; } = "dynamic-cube-internal";

    /// <summary>Confidential client with a service account allowed to manage users (realm-management: manage-users, view-users, query-users, view-realm, view-identity-providers).</summary>
    public string ClientId { get; init; } = "dynamic-cube-accounts";

    /// <summary>Secret name: the client secret is <c>Secrets:{name}:clientSecret</c> in the secret store, never in the database.</summary>
    public string SecretName { get; init; } = "KEYCLOAK_ADMIN";

    public bool IsFederated => string.Equals(Mode, "Federated", StringComparison.OrdinalIgnoreCase);
}

/// <summary>What the account screens may offer on this installation.</summary>
/// <param name="Provider">The identity provider, or <c>None</c> when accounts are maintained elsewhere.</param>
/// <param name="Mode"><c>Managed</c> or <c>Federated</c>.</param>
/// <param name="Federations">Directories and identity providers connected to the realm (e.g. <c>LDAP: ad.example.gov.sa</c>, <c>nafath</c>).</param>
public sealed record AccountCapabilities(string Provider, string Mode, bool CanCreate, bool CanSetPasswords, bool CanEnable, IReadOnlyList<string> Federations);

/// <summary>The sign-in account behind a user.</summary>
/// <param name="Source">Where the account comes from: <c>LOCAL</c>, or the directory / identity provider that owns it.</param>
public sealed record AccountView(
    string UserName,
    bool Exists,
    bool Enabled,
    string Source,
    bool CanSetPassword,
    string? Email,
    bool EmailVerified,
    IReadOnlyList<string> RequiredActions,
    DateTimeOffset? CreatedAt);

public sealed record NewAccountRequest(string UserName, string? Email, string? FirstName, string? LastName, string? TemporaryPassword, bool SendSetupEmail);

/// <summary>A new password the user must change on the next sign-in, or an email with a link to choose one.</summary>
public sealed record PasswordRequest(string? TemporaryPassword, bool SendEmail);

public sealed class AccountException(string message, AccountProblem problem = AccountProblem.Conflict, string? field = null) : Exception(message)
{
    public AccountProblem Problem { get; } = problem;

    public string? Field { get; } = field;
}

public enum AccountProblem
{
    NotFound,
    Conflict,
    Invalid,
    Unavailable,
}

/// <summary>The identity provider's accounts, as Apps Control maintains them.</summary>
public interface IAccountDirectory
{
    Task<AccountCapabilities> CapabilitiesAsync(CancellationToken cancellationToken);

    Task<AccountView> GetAsync(string userName, CancellationToken cancellationToken);

    Task<AccountView> CreateAsync(NewAccountRequest request, CancellationToken cancellationToken);

    Task SetEnabledAsync(string userName, bool enabled, CancellationToken cancellationToken);

    Task SetPasswordAsync(string userName, PasswordRequest request, CancellationToken cancellationToken);

    /// <summary>Ends the user's sessions everywhere.</summary>
    Task SignOutAsync(string userName, CancellationToken cancellationToken);
}

/// <summary>Accounts are maintained outside Apps Control: nothing can be changed from here.</summary>
internal sealed class UnmanagedAccountDirectory : IAccountDirectory
{
    public Task<AccountCapabilities> CapabilitiesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new AccountCapabilities("None", "Federated", false, false, false, []));

    public Task<AccountView> GetAsync(string userName, CancellationToken cancellationToken) =>
        Task.FromResult(new AccountView(userName, false, false, "EXTERNAL", false, null, false, [], null));

    public Task<AccountView> CreateAsync(NewAccountRequest request, CancellationToken cancellationToken) => throw Unavailable();

    public Task SetEnabledAsync(string userName, bool enabled, CancellationToken cancellationToken) => throw Unavailable();

    public Task SetPasswordAsync(string userName, PasswordRequest request, CancellationToken cancellationToken) => throw Unavailable();

    public Task SignOutAsync(string userName, CancellationToken cancellationToken) => throw Unavailable();

    private static AccountException Unavailable() =>
        new("Accounts are maintained in the identity provider, not in Apps Control (AppsControl:Identity:Provider is None).", AccountProblem.Unavailable);
}
