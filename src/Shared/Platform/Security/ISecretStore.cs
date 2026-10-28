using Microsoft.Extensions.Configuration;

namespace DynamicCube.Platform.Security;

/// <summary>
/// Named credentials (consumer keys, API keys, passwords). Configuration rows only hold the secret <em>name</em>; the
/// values come from the deployment's secret store, never from database tables.
/// </summary>
public interface ISecretStore
{
    /// <summary>A value of a named secret (e.g. <c>MOMRAH_GATEWAY</c> / <c>clientSecret</c>), or null when not set.</summary>
    string? Find(string name, string key);
}

/// <summary>
/// Reads <c>Secrets:{name}:{key}</c> from configuration, so values come from environment variables
/// (<c>Secrets__MOMRAH_GATEWAY__clientSecret</c>), Kubernetes secrets mounted as files, or a vault configuration provider.
/// </summary>
internal sealed class ConfigurationSecretStore(IConfiguration configuration) : ISecretStore
{
    public string? Find(string name, string key)
    {
        var value = configuration[$"Secrets:{name}:{key}"];
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
