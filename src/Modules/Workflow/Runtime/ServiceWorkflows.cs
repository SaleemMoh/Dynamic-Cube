using System.Text.Json;
using DynamicCube.Modules.Workflow.Definitions;
using DynamicCube.Modules.Workflow.Domain;
using DynamicCube.Modules.Workflow.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Configuration;
using DynamicCube.Platform.Services;
using DynamicCube.Platform.Workflow;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Workflow.Runtime;

/// <summary>Each service's own workflow; a new service gets one that completes requests without review.</summary>
internal sealed class ServiceWorkflows(WorkflowDbContext context, TimeProvider clock, DynamicCube.Platform.Expressions.JsonLogicEvaluator evaluator) : IServiceWorkflows
{
    /// <summary>The first version of every service's workflow: submitted requests complete (approved) at once.</summary>
    internal static WorkflowDefinition Default() => new()
    {
        Nodes =
        [
            new NodeDefinition { Code = "START", Type = NodeType.Start, NameAr = "البداية", NameEn = "Start", Next = "COMPLETED" },
            new NodeDefinition { Code = "COMPLETED", Type = NodeType.End, NameAr = "اكتمال الطلب", NameEn = "Request completed", Outcome = EndOutcome.Approved },
        ],
    };

    public Task<string?> WorkflowOfAsync(string serviceCode, CancellationToken cancellationToken) =>
        context.Workflows.Where(w => w.ServiceCode == serviceCode).Select(w => w.Code).SingleOrDefaultAsync(cancellationToken);

    public async Task EnsureAsync(string serviceCode, string nameAr, string nameEn, CancellationToken cancellationToken, string? definition = null)
    {
        if (await WorkflowOfAsync(serviceCode, cancellationToken) is not null)
        {
            return;
        }

        // The archetype's default workflow when it can run; otherwise requests complete without review.
        var start = definition is not null && Check(definition).Count == 0 ? Parse(definition) : null;

        var code = await FreeCodeAsync(serviceCode, cancellationToken);
        var now = clock.GetUtcNow();
        var workflow = new WorkflowConfig { Code = code, ServiceCode = serviceCode, NameAr = "مسار " + nameAr, NameEn = nameEn + " workflow" };
        context.Workflows.Add(workflow);
        context.Versions.Add(new WorkflowVersion
        {
            Code = code + "/1",
            WorkflowId = workflow.Id,
            Number = 1,
            NameAr = start is null ? "بدون مراجعة" : "من نمط الخدمة",
            NameEn = start is null ? "No review" : "From the service archetype",
            Definition = ChangeJson.Serialize(start ?? Default()),
            PublishedAt = now,
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ScopeAsync(string serviceCode, string? entityCode, string? businessApp, CancellationToken cancellationToken, IReadOnlyList<string>? sharedApps = null)
    {
        var shared = businessApp is null ? [] : (sharedApps ?? []).ToList();
        var workflow = await context.Workflows.SingleOrDefaultAsync(w => w.ServiceCode == serviceCode, cancellationToken);
        if (workflow is null || (workflow.EntityCode == entityCode && workflow.BusinessApp == businessApp && workflow.SharedApps.SequenceEqual(shared)))
        {
            return;
        }

        workflow.EntityCode = entityCode;
        workflow.BusinessApp = businessApp;
        workflow.SharedApps = shared;
        await context.Versions.Where(v => v.WorkflowId == workflow.Id)
            .ExecuteUpdateAsync(
                v => v.SetProperty(x => x.EntityCode, entityCode).SetProperty(x => x.BusinessApp, businessApp).SetProperty(x => x.SharedApps, shared),
                cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<string?> LatestDefinitionAsync(string workflowCode, CancellationToken cancellationToken) =>
        context.Versions.Where(v => v.Workflow!.Code == workflowCode && v.PublishedAt != null && !v.IsRetired)
            .OrderByDescending(v => v.Number).Select(v => v.Definition).FirstOrDefaultAsync(cancellationToken);

    public IReadOnlyDictionary<string, string[]> Check(string definition)
    {
        var errors = new ValidationErrors();
        if (Parse(definition) is not { } parsed)
        {
            errors.Add("workflow", "Not a workflow definition.");
        }
        else
        {
            DefinitionValidator.Validate(parsed, evaluator, errors);
        }

        return errors.ToDictionary();
    }

    private static WorkflowDefinition? Parse(string definition)
    {
        try
        {
            return JsonSerializer.Deserialize<WorkflowDefinition>(definition, ChangeJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public Task<bool> ExistsAsync(string workflowCode, CancellationToken cancellationToken) =>
        context.Workflows.AnyAsync(w => w.Code == workflowCode, cancellationToken);

    public async Task AdoptAsync(string serviceCode, string nameAr, string nameEn, string workflowCode, CancellationToken cancellationToken)
    {
        var named = await context.Workflows.SingleOrDefaultAsync(w => w.Code == workflowCode, cancellationToken)
            ?? throw new ChangeSetException($"Workflow '{workflowCode}' named by a version of service '{serviceCode}' does not exist; publish it first.");
        var own = await WorkflowOfAsync(serviceCode, cancellationToken);
        if (own == named.Code)
        {
            return;
        }

        var source = await context.Versions.AsNoTracking()
            .Where(v => v.WorkflowId == named.Id && v.PublishedAt != null && !v.IsRetired)
            .OrderByDescending(v => v.Number)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ChangeSetException($"Workflow '{workflowCode}' named by a version of service '{serviceCode}' has no published version; publish one first.");

        if (own is null && named.ServiceCode is null)
        {
            named.ServiceCode = serviceCode;
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        await EnsureAsync(serviceCode, nameAr, nameEn, cancellationToken);
        var target = await context.Workflows.SingleAsync(w => w.ServiceCode == serviceCode, cancellationToken);
        var latest = await context.Versions.AsNoTracking()
            .Where(v => v.WorkflowId == target.Id)
            .OrderByDescending(v => v.Number)
            .FirstOrDefaultAsync(cancellationToken);
        if (latest is { PublishedAt: not null, IsRetired: false } && SameDefinition(latest.Definition, source.Definition))
        {
            return;
        }

        var number = (latest?.Number ?? 0) + 1;
        context.Versions.Add(new WorkflowVersion
        {
            Code = $"{target.Code}/{number}",
            WorkflowId = target.Id,
            Number = number,
            NameAr = source.NameAr,
            NameEn = source.NameEn,
            Definition = source.Definition,
            PublishedAt = clock.GetUtcNow(),
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    private static bool SameDefinition(string a, string b) =>
        JsonElement.DeepEquals(JsonDocument.Parse(a).RootElement, JsonDocument.Parse(b).RootElement);

    /// <summary>
    /// Services that have no workflow yet (configured before 1f): the workflow their latest version named becomes theirs
    /// when no other service has it, otherwise they get the default one. Returns how many services were given one.
    /// </summary>
    internal async Task<int> BackfillAsync(IServiceCatalog catalog, CancellationToken cancellationToken)
    {
        var given = 0;
        foreach (var service in await catalog.ListAsync(cancellationToken))
        {
            if (await WorkflowOfAsync(service.Code, cancellationToken) is not null)
            {
                continue;
            }

            var named = service.NamedWorkflow is { } code ? await context.Workflows.SingleOrDefaultAsync(w => w.Code == code && w.ServiceCode == null, cancellationToken) : null;
            if (named is not null)
            {
                named.ServiceCode = service.Code;
                await context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                await EnsureAsync(service.Code, service.NameAr, service.NameEn, cancellationToken);
            }

            given++;
        }

        return given;
    }

    private async Task<string> FreeCodeAsync(string serviceCode, CancellationToken cancellationToken)
    {
        var code = serviceCode;
        for (var n = 2; await context.Workflows.AnyAsync(w => w.Code == code, cancellationToken); n++)
        {
            code = $"{serviceCode}_{n}";
        }

        return code;
    }
}
