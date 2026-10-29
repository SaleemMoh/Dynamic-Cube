using DynamicCube.Modules.Workflow.Definitions;
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
        && Placed(task, who)
        && !task.ExcludedUsers.Contains(who.UserName);

    /// <summary>Works in one of the task's units, holding its position there (when both are set and placements are known).</summary>
    private static bool Placed(WorkTask task, StaffMember who)
    {
        var units = task.CandidateOrgUnits;
        if (units.Count > 0 && task.CandidatePosition is { } position && who.Placements is { } placements)
        {
            return placements.Any(p => units.Contains(p.OrgUnit) && p.Position == position);
        }

        return (units.Count == 0 || who.OrgUnits.Any(units.Contains))
            && (task.CandidatePosition is null || who.Positions.Contains(task.CandidatePosition));
    }

    /// <summary>Active employees who may take the task.</summary>
    public async Task<IReadOnlyList<StaffMember>> CandidatesAsync(WorkTask task, CancellationToken cancellationToken)
    {
        if (task.CandidateUser is not null)
        {
            var user = await staff.FindAsync(task.CandidateUser, cancellationToken);
            return user is not null && IsCandidate(task, user) ? [user] : [];
        }

        if (task.CandidateRole is null && task.CandidateOrgUnits.Count == 0 && task.CandidatePosition is null)
        {
            return [];
        }

        var found = await staff.SearchAsync(
            new StaffQuery(task.CandidateRole, Position: task.CandidatePosition, OrgUnits: task.CandidateOrgUnits.Count > 0 ? task.CandidateOrgUnits : null), cancellationToken);
        return [.. found.Where(s => !task.ExcludedUsers.Contains(s.UserName))];
    }

    /// <summary>The unit an employee works in (the primary one first), or null.</summary>
    public async Task<string?> UnitOfAsync(string? userName, CancellationToken cancellationToken) =>
        userName is null || await staff.FindAsync(userName, cancellationToken) is not { OrgUnits.Count: > 0 } member ? null : member.OrgUnits[0];

    /// <summary>
    /// The unit a task goes to and the units whose staff may take it: <paramref name="start"/>, raised by
    /// <see cref="TaskAssignment.LevelsUp"/>, with the units below it when included; when nobody there qualifies and
    /// <see cref="TaskAssignment.ClimbWhenEmpty"/> is set, the unit above, and so on. Null when the unit is unknown.
    /// </summary>
    public async Task<(string Unit, IReadOnlyList<string> Units)?> RouteAsync(
        string? start, TaskAssignment assignment, IReadOnlyCollection<string> excluded, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        var tree = (await staff.OrgUnitsAsync(cancellationToken)).ToDictionary(u => u.Code, StringComparer.Ordinal);
        if (start is null || !tree.TryGetValue(start, out var node))
        {
            return null;
        }

        for (var level = 0; level < assignment.LevelsUp && node.ParentCode is { } up && tree.TryGetValue(up, out var parent); level++)
        {
            node = parent;
        }

        while (true)
        {
            IReadOnlyList<string> units = assignment.IncludeSubUnits ? Below(tree, node.Code) : [node.Code];
            if (!assignment.ClimbWhenEmpty || node.ParentCode is not { } above || !tree.TryGetValue(above, out var next))
            {
                return (node.Code, units);
            }

            var found = await staff.SearchAsync(new StaffQuery(assignment.Role, Position: assignment.Position, OrgUnits: units), cancellationToken);
            if (found.Any(s => !excluded.Contains(s.UserName)))
            {
                return (node.Code, units);
            }

            node = next;
        }
    }

    private static List<string> Below(Dictionary<string, OrgUnitNode> tree, string root)
    {
        var result = new List<string> { root };
        for (var i = 0; i < result.Count; i++)
        {
            result.AddRange(tree.Values.Where(u => u.ParentCode == result[i] && !result.Contains(u.Code)).Select(u => u.Code));
        }

        return result;
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
