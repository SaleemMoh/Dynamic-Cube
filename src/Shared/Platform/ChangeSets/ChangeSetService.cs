using DynamicCube.Platform.Persistence;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DynamicCube.Platform.ChangeSets;

/// <summary>Drives the change set lifecycle for the configuration owned by one module context.</summary>
public sealed class ChangeSetService<TContext>(
    TContext context,
    IEnumerable<IChangeApplier<TContext>> appliers,
    IOptions<ChangeSetOptions> options,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    where TContext : ModuleDbContext
{
    private readonly Dictionary<string, IChangeApplier<TContext>> _appliers =
        appliers.ToDictionary(a => a.EntityType, StringComparer.OrdinalIgnoreCase);

    public async Task<ChangeSet> CreateAsync(string area, string title, CancellationToken cancellationToken)
    {
        var changeSet = ChangeSet.Create(area, title, options.Value.RequiresApproval(area), currentUser.UserId, timeProvider.GetUtcNow());
        context.ChangeSets.Add(changeSet);
        await context.SaveChangesAsync(cancellationToken);
        return changeSet;
    }

    public async Task<ChangeSet> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await context.ChangeSets.Include(c => c.Items).SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new ChangeSetException($"Change set {id} was not found.");

    public async Task<ChangeItem> AddItemAsync(Guid id, string entityType, string entityCode, ChangeOperation operation, string? before, string? after, CancellationToken cancellationToken)
    {
        if (!_appliers.ContainsKey(entityType))
        {
            throw new ChangeSetException($"No change applier is registered for entity type '{entityType}'.");
        }

        var changeSet = await GetAsync(id, cancellationToken);
        var item = changeSet.AddItem(entityType, entityCode, operation, before, after);
        await context.SaveChangesAsync(cancellationToken);
        return item;
    }

    public Task SubmitAsync(Guid id, CancellationToken cancellationToken) =>
        MutateAsync(id, c => c.Submit(currentUser.UserId, timeProvider.GetUtcNow()), cancellationToken);

    public Task ApproveAsync(Guid id, string? comment, CancellationToken cancellationToken) =>
        MutateAsync(id, c => c.Approve(currentUser.UserId, timeProvider.GetUtcNow(), comment), cancellationToken);

    public Task RejectAsync(Guid id, string comment, CancellationToken cancellationToken) =>
        MutateAsync(id, c => c.Reject(currentUser.UserId, timeProvider.GetUtcNow(), comment), cancellationToken);

    public Task ReopenAsync(Guid id, CancellationToken cancellationToken) =>
        MutateAsync(id, c => c.Reopen(), cancellationToken);

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

            await applier.ApplyAsync(context, item, cancellationToken);
        }

        changeSet.MarkPublished(currentUser.UserId, timeProvider.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task MutateAsync(Guid id, Action<ChangeSet> mutate, CancellationToken cancellationToken)
    {
        var changeSet = await GetAsync(id, cancellationToken);
        mutate(changeSet);
        await context.SaveChangesAsync(cancellationToken);
    }
}
