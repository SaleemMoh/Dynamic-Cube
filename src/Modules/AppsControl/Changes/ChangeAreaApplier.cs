using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.ChangeSets;

namespace DynamicCube.Modules.AppsControl.Changes;

public sealed class ChangeAreaPayload : ConfigPayload
{
    public bool RequiresApproval { get; set; } = true;
}

/// <summary>Edits the approval policy of the configurable areas (rows are created by the migrator; they cannot be added or deleted).</summary>
internal sealed class ChangeAreaApplier : ConfigApplier<ChangeArea, ChangeAreaPayload>
{
    public override string EntityType => AppsControlChangeTypes.ChangeArea;

    public override string Area => AppsControlAreas.Governance;

    public override string ResourceCode => AppsControlResources.ApprovalPolicy;

    protected override IQueryable<ChangeArea> Query(AppsControlDbContext context) => context.ChangeAreas;

    protected override void ValidateKey(string entityCode, ValidationErrors errors)
    {
        if (AppsControlAreas.Configurable.All(a => a.Code != entityCode))
        {
            errors.Add("code", $"'{entityCode}' is not a configurable area.");
        }
    }

    protected override Task ApplyAsync(ChangeArea entity, ChangeAreaPayload payload, RefResolver refs, ValidationErrors errors, ChangeArea? existing, CancellationToken cancellationToken)
    {
        if (existing is null)
        {
            errors.Add("code", "Areas are created by the product.");
        }

        entity.RequiresApproval = payload.RequiresApproval;
        return Task.CompletedTask;
    }

    protected override Task<ChangeAreaPayload> ToPayloadAsync(AppsControlDbContext context, ChangeArea entity, CancellationToken cancellationToken) =>
        Task.FromResult(new ChangeAreaPayload { RequiresApproval = entity.RequiresApproval });

    protected override Task BeforeDeleteAsync(AppsControlDbContext context, ChangeArea entity, CancellationToken cancellationToken) =>
        throw new ChangeSetException("Areas cannot be deleted.");
}
