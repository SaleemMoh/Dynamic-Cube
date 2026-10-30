using DynamicCube.Modules.Services.Domain;
using DynamicCube.Platform.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Services.Runtime;

/// <summary>
/// Which services the portal offers (ADR-0036): those of no app, and those of an app (their own or a shared one) that
/// ticks the portal channel. Without Apps Control every service is offered.
/// </summary>
public sealed class PortalChannel(IServiceProvider services)
{
    /// <summary>Code of the portal channel.</summary>
    public const string Code = "EXTERNAL";

    private IReadOnlySet<string>? _apps;

    public async Task<bool> OffersAsync(Service service, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        if (service.BusinessApp is null || services.GetService<IConfigScopes>() is not { } scopes)
        {
            return true;
        }

        _apps ??= await scopes.AppsOnChannelAsync(Code, cancellationToken);
        return service.AllApps.Any(_apps.Contains);
    }

    public async Task<List<T>> OfferedAsync<T>(IEnumerable<T> rows, Func<T, Service> service, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        var offered = new List<T>();
        foreach (var row in rows)
        {
            if (await OffersAsync(service(row), cancellationToken))
            {
                offered.Add(row);
            }
        }

        return offered;
    }
}
