using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.ChangeSets;

namespace DynamicCube.Modules.AppsControl.Access;

/// <summary>
/// Version stamp of cached access decisions. Publishing Apps Control configuration bumps it, which invalidates this
/// process's cache immediately; other instances pick changes up when their short cache expires.
/// </summary>
public sealed class AccessCache
{
    private long _version;

    public long Version => Interlocked.Read(ref _version);

    public void Invalidate() => Interlocked.Increment(ref _version);
}

internal sealed class AccessCacheInvalidator(AccessCache cache) : IChangeSetPublishHook<AppsControlDbContext>
{
    public Task OnPublishedAsync(AppsControlDbContext context, ChangeSet changeSet, CancellationToken cancellationToken)
    {
        cache.Invalidate();
        return Task.CompletedTask;
    }
}
