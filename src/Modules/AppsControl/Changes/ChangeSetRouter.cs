using DynamicCube.Platform.ChangeSets;

namespace DynamicCube.Modules.AppsControl.Changes;

/// <summary>
/// Finds the module that owns a change set (by its area) so the Apps Control inbox and admin API work for the
/// configuration of every module.
/// </summary>
internal sealed class ChangeSetRouter(IEnumerable<IConfigApplier> appliers, IEnumerable<IChangeSetWorkflow> workflows)
{
    private readonly List<IConfigApplier> _appliers = appliers.ToList();
    private readonly List<IChangeSetWorkflow> _workflows = workflows.ToList();

    public IReadOnlyList<IConfigApplier> Appliers => _appliers;

    public bool IsKnownArea(string area) => _appliers.Any(a => a.Area == area);

    public IConfigApplier? FindApplier(string entityType) =>
        _appliers.FirstOrDefault(a => string.Equals(a.EntityType, entityType, StringComparison.OrdinalIgnoreCase));

    public IChangeSetWorkflow ForEntityType(string entityType) =>
        _workflows.FirstOrDefault(w => w.OwnsEntityType(entityType))
            ?? throw new ChangeSetException($"No module handles entity type '{entityType}'.");

    public IChangeSetWorkflow ForArea(string area)
    {
        var applier = _appliers.FirstOrDefault(a => a.Area == area) ?? throw new ChangeSetException($"Unknown area '{area}'.");
        return ForEntityType(applier.EntityType);
    }

    /// <summary>The workflow of the module that owns the change set (change sets are stored once, for all modules).</summary>
    public async Task<IChangeSetWorkflow> ForChangeSetAsync(Guid id, CancellationToken cancellationToken)
    {
        var changeSet = await _workflows[0].GetAsync(id, cancellationToken);
        return ForArea(changeSet.Area);
    }
}
