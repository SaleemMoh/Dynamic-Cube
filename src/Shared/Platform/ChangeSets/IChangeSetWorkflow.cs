namespace DynamicCube.Platform.ChangeSets;

/// <summary>
/// Module-independent view of a module's change-set service, so one inbox and one admin API can drive the change sets
/// of every module. Each module context registers one.
/// </summary>
public interface IChangeSetWorkflow
{
    /// <summary>True when one of this module's appliers handles <paramref name="entityType"/>.</summary>
    bool OwnsEntityType(string entityType);

    Task<ChangeSet> CreateAsync(string area, string title, CancellationToken cancellationToken);

    Task<ChangeSet> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<ChangeItem> AddItemAsync(Guid id, string entityType, string entityCode, ChangeOperation operation, string? payload, CancellationToken cancellationToken);

    Task RemoveItemAsync(Guid id, Guid itemId, CancellationToken cancellationToken);

    Task SubmitAsync(Guid id, CancellationToken cancellationToken);

    Task ApproveAsync(Guid id, string? comment, CancellationToken cancellationToken);

    Task RejectAsync(Guid id, string comment, CancellationToken cancellationToken);

    Task ReopenAsync(Guid id, CancellationToken cancellationToken);

    Task PublishAsync(Guid id, CancellationToken cancellationToken);

    Task DiscardAsync(Guid id, CancellationToken cancellationToken);

    Task<ChangeSet> SaveAndPublishAsync(string area, string title, string entityType, string entityCode, ChangeOperation operation, string? payload, CancellationToken cancellationToken);

    Task<ChangeSet> SaveAndSubmitAsync(string area, string title, string entityType, string entityCode, ChangeOperation operation, string? payload, CancellationToken cancellationToken);
}

/// <summary>A configuration area whose approval policy the entity sets (registered by the module that owns it).</summary>
public sealed record ChangeAreaDefinition(string Code, string NameAr, string NameEn, bool RequiresApprovalByDefault = true);
