using DynamicCube.Modules.Workflow.Domain;
using DynamicCube.Modules.Workflow.Persistence;
using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Workflow.Runtime;

/// <summary>
/// Marks open tasks amber at their warning time and red at their due time (escalating them to the node's escalation
/// role), and instances red past their overall due date. Run periodically by the worker.
/// </summary>
internal sealed class SlaMonitor(WorkflowDbContext context, DefinitionCache definitions, IUnitOfWork unitOfWork, TimeProvider clock)
{
    private const int BatchSize = 500;

    /// <summary>Returns how many tasks and instances changed state.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var changed = 0;

        var warnings = await context.Tasks.AsNoTracking()
            .Where(t => t.Status == WorkTaskStatus.Open && t.Sla == SlaState.OnTrack && t.WarnAt <= now && t.DueAt > now)
            .Take(BatchSize).ToListAsync(cancellationToken);
        foreach (var task in warnings)
        {
            changed += await ChangeAsync(task.InstanceId, async () =>
                await context.Tasks.Where(t => t.Id == task.Id && t.Status == WorkTaskStatus.Open && t.Sla == SlaState.OnTrack)
                    .ExecuteUpdateAsync(u => u.SetProperty(t => t.Sla, SlaState.Warning), cancellationToken) == 1
                    ? Notify(TaskNotifications.SlaWarning, task, Event(task, WorkflowEventTypes.SlaWarning, now))
                    : null, cancellationToken);
        }

        var overdue = await context.Tasks.AsNoTracking()
            .Where(t => t.Status == WorkTaskStatus.Open && t.Sla != SlaState.Breached && t.DueAt <= now)
            .Take(BatchSize).ToListAsync(cancellationToken);
        var instanceIds = overdue.Select(t => t.InstanceId).Distinct().ToList();
        var versions = await context.Instances.AsNoTracking()
            .Where(i => instanceIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.VersionId, cancellationToken);
        foreach (var task in overdue)
        {
            var role = (await definitions.GetAsync(context, versions[task.InstanceId], cancellationToken)).Node(task.NodeCode).Sla?.EscalateToRole;
            changed += await ChangeAsync(task.InstanceId, async () =>
                await context.Tasks.Where(t => t.Id == task.Id && t.Status == WorkTaskStatus.Open && t.Sla != SlaState.Breached)
                    .ExecuteUpdateAsync(u => u.SetProperty(t => t.Sla, SlaState.Breached).SetProperty(t => t.EscalatedToRole, role), cancellationToken) == 1
                    ? Notify(TaskNotifications.SlaBreached, Escalated(task, role), Event(task, WorkflowEventTypes.SlaBreached, now, role))
                    : null, cancellationToken);
        }

        // An instance's clock stands still while it waits for the applicant.
        var late = await context.Instances.AsNoTracking()
            .Where(i => i.Status == InstanceStatus.Running && i.Sla != SlaState.Breached && i.DueAt <= now)
            .Select(i => i.Id)
            .Take(BatchSize).ToListAsync(cancellationToken);
        foreach (var id in late)
        {
            changed += await ChangeAsync(id, async () =>
                await context.Instances.Where(i => i.Id == id && i.Status == InstanceStatus.Running && i.Sla != SlaState.Breached && i.DueAt <= now)
                    .ExecuteUpdateAsync(u => u.SetProperty(i => i.Sla, SlaState.Breached), cancellationToken) == 1
                    ? new WorkflowEvent { InstanceId = id, At = now, Actor = WorkflowEngine.System, Type = WorkflowEventTypes.SlaBreached }
                    : null, cancellationToken);
        }

        return changed;
    }

    /// <summary>
    /// One change under the instance lock, so it never lands between an employee reading a task and saving it; the change
    /// re-checks its condition (the instance may have moved on) and returns its event, or null when it no longer applies.
    /// </summary>
    private async Task<int> ChangeAsync(Guid instanceId, Func<Task<WorkflowEvent?>> change, CancellationToken cancellationToken)
    {
        await using var work = await unitOfWork.BeginAsync(cancellationToken);
        await context.LockAsync(instanceId, cancellationToken);
        if (await change() is not { } recorded)
        {
            return 0;
        }

        context.Events.Add(recorded);
        await context.SaveChangesAsync(cancellationToken);
        await work.CommitAsync(cancellationToken);
        return 1;
    }

    /// <summary>Raises the task's notification event with the change (saved together by <see cref="ChangeAsync"/>).</summary>
    private WorkflowEvent Notify(string code, WorkTask task, WorkflowEvent recorded)
    {
        TaskNotifications.Raise(context, clock, code, task);
        return recorded;
    }

    private static WorkTask Escalated(WorkTask task, string? role)
    {
        task.EscalatedToRole = role;
        return task;
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
