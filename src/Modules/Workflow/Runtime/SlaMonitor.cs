using DynamicCube.Modules.Workflow.Domain;
using DynamicCube.Modules.Workflow.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Workflow.Runtime;

/// <summary>
/// Marks open tasks amber at their warning time and red at their due time (escalating them to the node's escalation
/// role), and instances red past their overall due date. Run periodically by the worker.
/// </summary>
internal sealed class SlaMonitor(WorkflowDbContext context, DefinitionCache definitions, TimeProvider clock)
{
    private const int BatchSize = 500;

    /// <summary>Returns how many tasks and instances changed state.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var changed = 0;

        var warnings = await context.Tasks
            .Where(t => t.Status == WorkTaskStatus.Open && t.Sla == SlaState.OnTrack && t.WarnAt <= now && t.DueAt > now)
            .Take(BatchSize).ToListAsync(cancellationToken);
        foreach (var task in warnings)
        {
            task.Sla = SlaState.Warning;
            context.Events.Add(Event(task, WorkflowEventTypes.SlaWarning, now));
        }

        var overdue = await context.Tasks
            .Where(t => t.Status == WorkTaskStatus.Open && t.Sla != SlaState.Breached && t.DueAt <= now)
            .Take(BatchSize).ToListAsync(cancellationToken);
        var instanceIds = overdue.Select(t => t.InstanceId).Distinct().ToList();
        var versions = await context.Instances.AsNoTracking()
            .Where(i => instanceIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.VersionId, cancellationToken);
        foreach (var task in overdue)
        {
            var node = (await definitions.GetAsync(context, versions[task.InstanceId], cancellationToken)).Node(task.NodeCode);
            task.Sla = SlaState.Breached;
            task.EscalatedToRole = node.Sla?.EscalateToRole;
            context.Events.Add(Event(task, WorkflowEventTypes.SlaBreached, now, task.EscalatedToRole));
        }

        // An instance's clock stands still while it waits for the applicant.
        var late = await context.Instances
            .Where(i => i.Status == InstanceStatus.Running && i.Sla != SlaState.Breached && i.DueAt <= now)
            .Take(BatchSize).ToListAsync(cancellationToken);
        foreach (var instance in late)
        {
            instance.Sla = SlaState.Breached;
            context.Events.Add(new WorkflowEvent { InstanceId = instance.Id, At = now, Actor = WorkflowEngine.System, Type = WorkflowEventTypes.SlaBreached });
        }

        changed += warnings.Count + overdue.Count + late.Count;
        if (changed > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }

    private static WorkflowEvent Event(WorkTask task, string type, DateTimeOffset now, string? target = null) => new()
    {
        InstanceId = task.InstanceId,
        TaskId = task.Id,
        At = now,
        Actor = WorkflowEngine.System,
        Type = type,
        NodeCode = task.NodeCode,
        Target = target,
    };
}
