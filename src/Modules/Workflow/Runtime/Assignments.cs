using DynamicCube.Modules.Workflow.Domain;
using DynamicCube.Modules.Workflow.Persistence;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Workflow.Runtime;

/// <summary>Who may take a task, and the least-loaded candidate for automatic assignment.</summary>
internal sealed class Assignments(WorkflowDbContext context, IStaffDirectory staff)
{
    public static bool IsCandidate(WorkTask task, StaffMember who) =>
        (task.CandidateUser is null || task.CandidateUser == who.UserName)
        && (task.CandidateRole is null || who.Roles.Contains(task.CandidateRole))
        && (task.CandidateOrgUnit is null || who.OrgUnits.Contains(task.CandidateOrgUnit))
        && (task.CandidatePosition is null || who.Positions.Contains(task.CandidatePosition))
        && !task.ExcludedUsers.Contains(who.UserName);

    /// <summary>Active employees who may take the task.</summary>
    public async Task<IReadOnlyList<StaffMember>> CandidatesAsync(WorkTask task, CancellationToken cancellationToken)
    {
        if (task.CandidateUser is not null)
        {
            var user = await staff.FindAsync(task.CandidateUser, cancellationToken);
            return user is not null && IsCandidate(task, user) ? [user] : [];
        }

        if (task.CandidateRole is null && task.CandidateOrgUnit is null && task.CandidatePosition is null)
        {
            return [];
        }

        var found = await staff.SearchAsync(new StaffQuery(task.CandidateRole, task.CandidateOrgUnit, task.CandidatePosition), cancellationToken);
        return [.. found.Where(s => !task.ExcludedUsers.Contains(s.UserName))];
    }

    /// <summary>The candidate with the fewest open tasks (ties by username), or null when there is none.</summary>
    public async Task<string?> LeastLoadedAsync(WorkTask task, CancellationToken cancellationToken)
    {
        var candidates = (await CandidatesAsync(task, cancellationToken)).Select(c => c.UserName).ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        var loads = await context.Tasks.AsNoTracking()
            .Where(t => t.Status == WorkTaskStatus.Open && t.Assignee != null && candidates.Contains(t.Assignee))
            .GroupBy(t => t.Assignee!)
            .Select(g => new { User = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.User, x => x.Count, cancellationToken);
        return candidates.OrderBy(c => loads.GetValueOrDefault(c)).ThenBy(c => c, StringComparer.Ordinal).First();
    }
}
