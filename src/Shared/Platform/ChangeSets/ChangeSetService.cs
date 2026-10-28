using DynamicCube.Platform.Persistence;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Platform.ChangeSets;

/// <summary>Drives the change set lifecycle for the configuration owned by one module context.</summary>
public sealed class ChangeSetService<TContext>(
    TContext context,
    IEnumerable<IChangeApplier<TContext>> appliers,
    IEnumerable<IChangeSetPublishHook<TContext>> publishHooks,
    IChangeApprovalPolicy approvalPolicy,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IChangeSetWorkflow
    where TContext : ModuleDbContext
{
    private readonly Dictionary<string, IChangeApplier<TContext>> _appliers =
        appliers.ToDictionary(a => a.EntityType, StringComparer.OrdinalIgnoreCase);

    public async Task<ChangeSet> CreateAsync(string area, string title, CancellationToken cancellationToken)
    {
        var changeSet = ChangeSet.Create(area, title, await approvalPolicy.RequiresApprovalAsync(area, cancellationToken), currentUser.UserId, timeProvider.GetUtcNow());
        context.ChangeSets.Add(changeSet);
        await context.SaveChangesAsync(cancellationToken);
        return changeSet;
    }

    /// <summary>
    /// For areas without approval: records one change as a change set (so its before/after is kept like any other) and
    /// publishes it at once. Nothing is stored when the change is invalid.
    /// </summary>
    public async Task<ChangeSet> SaveAndPublishAsync(string area, string title, string entityType, string entityCode, ChangeOperation operation, string? payload, CancellationToken cancellationToken)
    {
        if (await approvalPolicy.RequiresApprovalAsync(area, cancellationToken))
        {
            throw new ChangeSetException($"Changes in area '{area}' need approval; add them to a change set and submit it.");
        }

        if (!_appliers.TryGetValue(entityType, out var applier))
        {
            throw new ChangeSetException($"No change applier is registered for entity type '{entityType}'.");
        }

        var now = timeProvider.GetUtcNow();
        var changeSet = ChangeSet.Create(area, title, requiresApproval: false, currentUser.UserId, now);
        await applier.ValidateAsync(context, changeSet, entityCode, operation, payload, cancellationToken);
        var before = await applier.GetSnapshotAsync(context, entityCode, cancellationToken);
        changeSet.AddItem(entityType, entityCode, operation, before, operation == ChangeOperation.Delete ? null : payload);
        changeSet.Submit(currentUser.UserId, now);
        context.ChangeSets.Add(changeSet);
        await context.SaveChangesAsync(cancellationToken);

        await PublishAsync(changeSet.Id, cancellationToken);
        return changeSet;
    }

    public async Task<ChangeSet> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await context.ChangeSets.Include(c => c.Items).SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new ChangeSetException($"Change set {id} was not found.");

    public bool OwnsEntityType(string entityType) => _appliers.ContainsKey(entityType);

    /// <summary>
    /// Validates and adds an item. The "before" snapshot is taken from the current state by the applier.
    /// </summary>
    public async Task<ChangeItem> AddItemAsync(Guid id, string entityType, string entityCode, ChangeOperation operation, string? payload, CancellationToken cancellationToken)
    {
        if (!_appliers.TryGetValue(entityType, out var applier))
        {
            throw new ChangeSetException($"No change applier is registered for entity type '{entityType}'.");
        }

        var changeSet = await GetAsync(id, cancellationToken);
        await applier.ValidateAsync(context, changeSet, entityCode, operation, payload, cancellationToken);
        var before = await applier.GetSnapshotAsync(context, entityCode, cancellationToken);
        var item = changeSet.AddItem(entityType, entityCode, operation, before, operation == ChangeOperation.Delete ? null : payload);
        await context.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task RemoveItemAsync(Guid id, Guid itemId, CancellationToken cancellationToken)
    {
        var changeSet = await GetAsync(id, cancellationToken);
        var item = changeSet.Items.SingleOrDefault(i => i.Id == itemId);
        changeSet.RemoveItem(itemId);
        if (item is not null)
        {
            context.Remove(item);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public Task SubmitAsync(Guid id, CancellationToken cancellationToken) =>
        MutateAsync(id, c => c.Submit(currentUser.UserId, timeProvider.GetUtcNow()), cancellationToken);

    public Task ApproveAsync(Guid id, string? comment, CancellationToken cancellationToken) =>
        MutateAsync(id, c => c.Approve(currentUser.UserId, timeProvider.GetUtcNow(), comment), cancellationToken);

    public Task RejectAsync(Guid id, string comment, CancellationToken cancellationToken) =>
        MutateAsync(id, c => c.Reject(currentUser.UserId, timeProvider.GetUtcNow(), comment), cancellationToken);

    public Task ReopenAsync(Guid id, CancellationToken cancellationToken) =>
        MutateAsync(id, c => c.Reopen(), cancellationToken);

    /// <summary>Deletes a change set that was not published (draft, rejected, or approved but failed to publish).</summary>
    public async Task DiscardAsync(Guid id, CancellationToken cancellationToken)
    {
        var changeSet = await GetAsync(id, cancellationToken);
        if (changeSet.Status is not (ChangeSetStatus.Draft or ChangeSetStatus.Rejected or ChangeSetStatus.Approved))
        {
            throw new ChangeSetException($"A {changeSet.Status} change set cannot be discarded.");
        }

        context.RemoveRange(changeSet.Items);
        context.Remove(changeSet);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Applies all items in order and marks the change set published, atomically.</summary>
    public async Task PublishAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var changeSet = await GetAsync(id, cancellationToken);
        foreach (var item in changeSet.Items.OrderBy(i => i.Sequence))
        {
            if (!_appliers.TryGetValue(item.EntityType, out var applier))
            {
                throw new ChangeSetException($"No change applier is registered for entity type '{item.EntityType}'.");
            }

            try
            {
                await applier.ApplyAsync(context, item, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                throw new ChangeSetException(
                    $"Item {item.Sequence} ({item.EntityType} '{item.EntityCode}') could not be applied: {ex.InnerException?.Message ?? ex.Message}",
                    ex);
            }
        }

        changeSet.MarkPublished(currentUser.UserId, timeProvider.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);

        foreach (var hook in publishHooks)
        {
            await hook.OnPublishedAsync(context, changeSet, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task MutateAsync(Guid id, Action<ChangeSet> mutate, CancellationToken cancellationToken)
    {
        var changeSet = await GetAsync(id, cancellationToken);
        mutate(changeSet);
        await context.SaveChangesAsync(cancellationToken);
    }
}
