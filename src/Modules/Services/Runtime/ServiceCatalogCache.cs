using System.Collections.Concurrent;
using System.Text.Json;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Services.Runtime;

/// <summary>
/// Resolved versions by id. Published versions never change, so entries stay valid; publishing service configuration
/// clears the cache anyway so retirements and new versions show at once on this instance.
/// </summary>
public sealed class ServiceCatalogCache
{
    private readonly ConcurrentDictionary<Guid, ResolvedVersion> _versions = new();

    internal async Task<ResolvedVersion> GetAsync(ServicesDbContext context, Guid versionId, CancellationToken cancellationToken)
    {
        if (_versions.TryGetValue(versionId, out var cached))
        {
            return cached;
        }

        var json = await context.Versions.AsNoTracking().Where(v => v.Id == versionId).Select(v => v.Resolved).SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Service version {versionId} is not published.");
        var resolved = JsonSerializer.Deserialize<ResolvedVersion>(json, ChangeJson.Options)!;
        _versions[versionId] = resolved;
        return resolved;
    }

    public void Clear() => _versions.Clear();
}
