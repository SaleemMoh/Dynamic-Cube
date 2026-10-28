using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.ChangeSets;

namespace DynamicCube.Modules.AppsControl.Changes;

/// <summary>Base of the Apps Control appliers: the kernel config applier bound to the Apps Control context and resolver.</summary>
internal abstract class ConfigApplier<TEntity, TPayload>(AppsControlDbContext context)
    : ConfigApplier<AppsControlDbContext, RefResolver, TEntity, TPayload>(context)
    where TEntity : ConfigEntity, new()
    where TPayload : ConfigPayload, new()
{
    protected override RefResolver CreateRefs(AppsControlDbContext context, ChangeSet? draft) => new(context, draft);
}
