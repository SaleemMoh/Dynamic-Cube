using DynamicCube.Modules.AppsControl.Accounts;
using DynamicCube.Platform.PublicApi;

namespace DynamicCube.Modules.AppsControl.ConnectedApps;

/// <summary>
/// Partner systems' sign-in clients (ADR-0042) in the realm the public API trusts (the portal's), as service accounts that
/// get tokens with client credentials and the public API's audience. Maintained with the portal realm's service account.
/// </summary>
internal sealed class PartnerClients(ISignInClients clients) : IServiceClients
{
    /// <summary>The key of the portal realm's sign-in clients.</summary>
    public const string Key = "portal";

    public bool IsManaged => clients.IsManaged;

    public Task<string?> TokenUrlAsync(CancellationToken cancellationToken) =>
        Call(async () => await clients.IssuerAsync(cancellationToken) is { } issuer ? issuer.TrimEnd('/') + "/protocol/openid-connect/token" : null);

    public Task<ServiceClient?> FindAsync(string clientId, string audience, CancellationToken cancellationToken) =>
        Call(async () => await clients.FindAsync(clientId, audience, cancellationToken) is { } client
            ? new ServiceClient(client.ClientId, client.Enabled, client.ServiceAccount, client.HasAudience)
            : null);

    public Task<bool> SaveAsync(string clientId, string name, string audience, CancellationToken cancellationToken) =>
        Call(() => clients.SaveAsync(new SignInClientSpec(clientId, name, [], [], audience, ServiceOnly: true), cancellationToken));

    public Task<string> SecretAsync(string clientId, CancellationToken cancellationToken) => Call(() => clients.SecretAsync(clientId, cancellationToken));

    public Task<string> NewSecretAsync(string clientId, CancellationToken cancellationToken) => Call(() => clients.NewSecretAsync(clientId, cancellationToken));

    private static async Task<T> Call<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (AccountException ex)
        {
            throw new ServiceClientException(ex.Message, ex);
        }
    }
}
