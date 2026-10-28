using DynamicCube.Modules.Workflow.Definitions;
using DynamicCube.Modules.Workflow.Domain;
using DynamicCube.Modules.Workflow.Persistence;
using DynamicCube.Platform.Services;
using DynamicCube.Platform.Workflow;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Workflow.Runtime;

/// <summary>Each service's own workflow; a new service gets one that completes requests without review.</summary>
internal sealed class ServiceWorkflows(WorkflowDbContext context, TimeProvider clock) : IServiceWorkflows
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

    public async Task EnsureAsync(string serviceCode, string nameAr, string nameEn, CancellationToken cancellationToken)
    {
        if (await WorkflowOfAsync(serviceCode, cancellationToken) is not null)
        {
            return;
        }

        var code = await FreeCodeAsync(serviceCode, cancellationToken);
        var now = clock.GetUtcNow();
        var workflow = new WorkflowConfig { Code = code, ServiceCode = serviceCode, NameAr = "مسار " + nameAr, NameEn = nameEn + " workflow" };
        context.Workflows.Add(workflow);
        context.Versions.Add(new WorkflowVersion
        {
            Code = code + "/1",
            WorkflowId = workflow.Id,
            Number = 1,
            NameAr = "بدون مراجعة",
            NameEn = "No review",
            Definition = ChangeJson.Serialize(Default()),
            PublishedAt = now,
        });
        await context.SaveChangesAsync(cancellationToken);
    }

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
