using DynamicCube.Modules.Workflow.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Workflow.Runtime;

/// <summary>
/// One change at a time per workflow instance: the engine, task changes and the SLA monitor lock the instance row (in a
/// unit of work) before reading what they change, so parallel branches see each other and nobody's change is lost.
/// </summary>
internal static class InstanceLock
{
    public static async Task LockAsync(this WorkflowDbContext context, Guid instanceId, CancellationToken cancellationToken) =>
        await context.Database.ExecuteSqlAsync($"SELECT 1 FROM wf.instance WHERE id = {instanceId} FOR UPDATE", cancellationToken);
}
