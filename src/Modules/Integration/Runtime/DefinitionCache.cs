using System.Collections.Concurrent;
using DynamicCube.Modules.Integration.Domain;
using DynamicCube.Modules.Integration.Persistence;
using DynamicCube.Platform.ChangeSets;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Integration.Runtime;

/// <summary>
/// Published operation definitions (with connection and envelope) for the gateway. Publishing Integration Manager
/// configuration clears this process's cache and bumps <see cref="Version"/> (which also retires cached responses);
/// other instances pick changes up when their entries expire.
/// </summary>
public sealed class DefinitionCache(TimeProvider clock)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);
    private readonly ConcurrentDictionary<string, (Operation Operation, DateTimeOffset LoadedAt)> _operations = new(StringComparer.Ordinal);
    private long _version;

    public long Version => Interlocked.Read(ref _version);

    internal async Task<Operation?> GetAsync(IntegrationDbContext context, string code, CancellationToken cancellationToken)
    {
        if (_operations.TryGetValue(code, out var entry) && clock.GetUtcNow() - entry.LoadedAt < Lifetime)
        {
            return entry.Operation;
        }

        var operation = await context.Operations.AsNoTracking()
            .Include(o => o.Connection)
            .Include(o => o.EnvelopeProfile)
            .SingleOrDefaultAsync(o => o.Code == code && o.IsActive && o.Connection!.IsActive, cancellationToken);
        if (operation is null)
        {
            _operations.TryRemove(code, out _);
            return null;
        }

        _operations[code] = (operation, clock.GetUtcNow());
        return operation;
    }

    public void Clear()
    {
        _operations.Clear();
        Interlocked.Increment(ref _version);
    }
}

internal sealed class DefinitionCacheInvalidator(DefinitionCache cache) : IChangeSetPublishHook<IntegrationDbContext>
{
    public Task OnPublishedAsync(IntegrationDbContext context, ChangeSet changeSet, CancellationToken cancellationToken)
    {
        cache.Clear();
        return Task.CompletedTask;
    }
}
