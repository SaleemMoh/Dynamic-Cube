using DynamicCube.Platform.Persistence;

namespace DynamicCube.Platform.ChangeSets;

/// <summary>
/// Applies change items of one entity type to the owning module's context. Appliers must not call
/// <c>SaveChanges</c>; the service saves after each item (so later items see earlier ones) inside one transaction.
/// </summary>
public interface IChangeApplier<in TContext>
    where TContext : ModuleDbContext
{
    /// <summary>Entity type key handled by this applier, e.g. <c>apps.domain</c>.</summary>
    string EntityType { get; }

    /// <summary>
    /// Checks an item when it is added to a draft so makers get feedback early. References may point to rows created by
    /// earlier items of the same <paramref name="changeSet"/>. Throw <see cref="ChangeValidationException"/> for invalid
    /// items. Everything is checked again on apply.
    /// </summary>
    Task ValidateAsync(TContext context, ChangeSet changeSet, string entityCode, ChangeOperation operation, string? payload, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>Current JSON state of the entity (stored as the item's "before"), or null when it does not exist.</summary>
    Task<string?> GetSnapshotAsync(TContext context, string entityCode, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    Task ApplyAsync(TContext context, ChangeItem item, CancellationToken cancellationToken);
}

/// <summary>Runs inside the publish transaction after all items were applied and saved (e.g. regenerate views, clear caches).</summary>
public interface IChangeSetPublishHook<in TContext>
    where TContext : ModuleDbContext
{
    Task OnPublishedAsync(TContext context, ChangeSet changeSet, CancellationToken cancellationToken);
}
