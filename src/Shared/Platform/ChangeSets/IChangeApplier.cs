using DynamicCube.Platform.Persistence;

namespace DynamicCube.Platform.ChangeSets;

/// <summary>
/// Applies change items of one entity type to the owning module's context. Appliers must not call
/// <c>SaveChanges</c>; the change set is saved atomically after all items are applied.
/// </summary>
public interface IChangeApplier<in TContext>
    where TContext : ModuleDbContext
{
    /// <summary>Entity type key handled by this applier, e.g. <c>apps.domain</c>.</summary>
    string EntityType { get; }

    Task ApplyAsync(TContext context, ChangeItem item, CancellationToken cancellationToken);
}
