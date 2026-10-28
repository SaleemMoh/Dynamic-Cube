using System.Text.Json;
using System.Text.RegularExpressions;
using DynamicCube.Modules.Workflow.Definitions;
using DynamicCube.Modules.Workflow.Domain;
using DynamicCube.Modules.Workflow.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Expressions;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Workflow.Changes;

internal abstract class WorkflowApplierBase<TEntity, TPayload>(WorkflowDbContext context)
    : ConfigApplier<WorkflowDbContext, ConfigRefs<WorkflowDbContext>, TEntity, TPayload>(context)
    where TEntity : ConfigEntity, new()
    where TPayload : ConfigPayload, new()
{
    public override string Area => WorkflowChangeTypes.Area;

    public override string ResourceCode => WorkflowResources.Config;

    protected override Regex CodePattern => ValidationErrors.IdentifierCode();

    protected override ConfigRefs<WorkflowDbContext> CreateRefs(WorkflowDbContext context, ChangeSet? draft) => new(context, draft);
}

public sealed class WorkflowPayload : ConfigPayload
{
    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }
}

internal sealed class WorkflowApplier(WorkflowDbContext db) : WorkflowApplierBase<WorkflowConfig, WorkflowPayload>(db)
{
    public override string EntityType => WorkflowChangeTypes.Workflow;

    protected override IQueryable<WorkflowConfig> Query(WorkflowDbContext context) => context.Workflows;

    protected override Task ApplyAsync(WorkflowConfig entity, WorkflowPayload payload, ConfigRefs<WorkflowDbContext> refs, ValidationErrors errors, WorkflowConfig? existing, CancellationToken cancellationToken)
    {
        errors.MaxLength("descriptionAr", payload.DescriptionAr, 4000);
        errors.MaxLength("descriptionEn", payload.DescriptionEn, 4000);
        entity.DescriptionAr = string.IsNullOrWhiteSpace(payload.DescriptionAr) ? null : payload.DescriptionAr.Trim();
        entity.DescriptionEn = string.IsNullOrWhiteSpace(payload.DescriptionEn) ? null : payload.DescriptionEn.Trim();
        return Task.CompletedTask;
    }

    protected override Task<WorkflowPayload> ToPayloadAsync(WorkflowDbContext context, WorkflowConfig entity, CancellationToken cancellationToken) =>
        Task.FromResult(new WorkflowPayload { DescriptionAr = entity.DescriptionAr, DescriptionEn = entity.DescriptionEn });

    protected override async Task BeforeDeleteAsync(WorkflowDbContext context, WorkflowConfig entity, CancellationToken cancellationToken)
    {
        if (await context.Versions.AnyAsync(v => v.WorkflowId == entity.Id, cancellationToken))
        {
            throw new ChangeSetException($"Workflow '{entity.Code}' has versions; deactivate it instead.");
        }
    }
}

public sealed class WorkflowVersionPayload : ConfigPayload
{
    public bool IsRetired { get; set; }

    public WorkflowDefinition? Definition { get; set; }

    /// <summary>Read-only: when the version was published.</summary>
    public DateTimeOffset? PublishedAt { get; set; }
}

/// <summary>
/// Workflow versions, keyed <c>WORKFLOW/N</c>. A new version takes the next number; afterwards only its names and the
/// retired flag may change, so instances always run on the graph they started with.
/// </summary>
internal sealed partial class WorkflowVersionApplier(WorkflowDbContext db, JsonLogicEvaluator evaluator, TimeProvider clock)
    : WorkflowApplierBase<WorkflowVersion, WorkflowVersionPayload>(db)
{
    public override string EntityType => WorkflowChangeTypes.Version;

    protected override Regex CodePattern => Key();

    protected override IQueryable<WorkflowVersion> Query(WorkflowDbContext context) => context.Versions.Include(v => v.Workflow);

    protected override IQueryable<WorkflowVersion> ListQuery(WorkflowDbContext context, string? parent) =>
        (parent is null ? Query(context) : Query(context).Where(v => v.Workflow!.Code == parent)).OrderBy(v => v.Workflow!.Code).ThenByDescending(v => v.Number);

    protected override async Task ApplyAsync(WorkflowVersion entity, WorkflowVersionPayload payload, ConfigRefs<WorkflowDbContext> refs, ValidationErrors errors, WorkflowVersion? existing, CancellationToken cancellationToken)
    {
        var (workflowCode, number) = Parse(entity.Code);
        var definition = payload.Definition ?? new WorkflowDefinition();
        var serialized = ChangeJson.Serialize(definition);
        entity.IsRetired = payload.IsRetired;
        if (existing is not null)
        {
            if (!JsonElement.DeepEquals(JsonDocument.Parse(serialized).RootElement, JsonDocument.Parse(existing.Definition).RootElement))
            {
                errors.Add("definition", $"Published versions cannot change; add version {workflowCode}/{number + 1} instead.");
            }

            return;
        }

        entity.WorkflowId = await refs.ResolveAsync(refs.Context.Workflows, WorkflowChangeTypes.Workflow, workflowCode, "code", errors, cancellationToken) ?? Guid.Empty;
        entity.Number = number;
        entity.Definition = serialized;
        entity.PublishedAt = refs.IsValidating ? null : clock.GetUtcNow();

        var latest = await refs.Context.Versions.Where(v => v.Workflow!.Code == workflowCode).MaxAsync(v => (int?)v.Number, cancellationToken) ?? 0;
        var pendingBefore = refs.IsValidating ? Enumerable.Range(1, Math.Max(0, number - 1)).Count(n => refs.IsPendingCreate(WorkflowChangeTypes.Version, $"{workflowCode}/{n}")) : 0;
        if (number != latest + 1 + pendingBefore)
        {
            errors.Add("code", $"The next version of {workflowCode} is {latest + 1 + pendingBefore}.");
        }

        DefinitionValidator.Validate(definition, evaluator, errors);
    }

    protected override Task<WorkflowVersionPayload> ToPayloadAsync(WorkflowDbContext context, WorkflowVersion entity, CancellationToken cancellationToken) =>
        Task.FromResult(new WorkflowVersionPayload
        {
            IsRetired = entity.IsRetired,
            Definition = JsonSerializer.Deserialize<WorkflowDefinition>(entity.Definition, ChangeJson.Options),
            PublishedAt = entity.PublishedAt,
        });

    protected override async Task BeforeDeleteAsync(WorkflowDbContext context, WorkflowVersion entity, CancellationToken cancellationToken)
    {
        if (await context.Instances.AnyAsync(i => i.VersionId == entity.Id, cancellationToken))
        {
            throw new ChangeSetException($"Version {entity.Code} has instances; retire it instead.");
        }
    }

    internal static (string Workflow, int Number) Parse(string key)
    {
        var slash = key.LastIndexOf('/');
        return slash > 0 && int.TryParse(key[(slash + 1)..], System.Globalization.CultureInfo.InvariantCulture, out var number)
            ? (key[..slash], number)
            : (key, 0);
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_\\-]{0,50}/[1-9][0-9]{0,3}$")]
    private static partial Regex Key();
}
