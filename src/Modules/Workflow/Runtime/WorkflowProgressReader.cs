using DynamicCube.Modules.Workflow.Domain;
using DynamicCube.Modules.Workflow.Persistence;
using DynamicCube.Platform.Workflow;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Workflow.Runtime;

/// <summary>
/// Where a subject stands in its latest workflow run (ADR-0042): status, dates and outcome, the steps its live branches
/// are at (named from the version, with the open task's deadline), and how many steps it went through.
/// </summary>
internal sealed class WorkflowProgressReader(WorkflowDbContext context, DefinitionCache definitions) : IWorkflowProgress
{
    public async Task<WorkflowProgress?> ProgressAsync(string subjectType, Guid subjectId, CancellationToken cancellationToken)
    {
        var instance = await context.Instances.AsNoTracking()
            .Where(i => i.SubjectType == subjectType && i.SubjectId == subjectId)
            .OrderByDescending(i => i.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (instance is null)
        {
            return null;
        }

        var version = await definitions.GetAsync(context, instance.VersionId, cancellationToken);
        var tokens = await context.Tokens.AsNoTracking()
            .Where(t => t.InstanceId == instance.Id && t.Status != TokenStatus.Done && t.Status != TokenStatus.Cancelled)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
        var tasks = await context.Tasks.AsNoTracking()
            .Where(t => t.InstanceId == instance.Id && t.Status == WorkTaskStatus.Open)
            .ToListAsync(cancellationToken);
        var steps = tokens
            .GroupBy(t => t.NodeCode, StringComparer.Ordinal)
            .Select(g =>
            {
                version.Nodes.TryGetValue(g.Key, out var node);
                var task = tasks.Where(t => t.NodeCode == g.Key).OrderBy(t => t.CreatedAt).FirstOrDefault();
                return new WorkflowStepNow(
                    g.Key,
                    node?.NameAr ?? task?.NameAr ?? g.Key,
                    node?.NameEn ?? task?.NameEn ?? g.Key,
                    task?.CreatedAt ?? g.Min(t => t.CreatedAt),
                    task?.DueAt);
            })
            .ToList();
        var current = steps.Select(s => s.Code).ToHashSet(StringComparer.Ordinal);
        return new WorkflowProgress(
            instance.WorkflowCode,
            Status(instance.Status),
            instance.StartedAt,
            instance.CompletedAt,
            instance.DueAt,
            instance.Outcome,
            steps,
            instance.VisitedNodes.Count(n => !current.Contains(n)));
    }

    private static string Status(InstanceStatus status) => status switch
    {
        InstanceStatus.Running => "RUNNING",
        InstanceStatus.WaitingCustomer => "WAITING_CUSTOMER",
        InstanceStatus.Completed => "COMPLETED",
        _ => "CANCELLED",
    };
}
