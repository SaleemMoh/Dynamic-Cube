using System.Text.Json.Nodes;
using DynamicCube.Modules.Workflow.Changes;
using DynamicCube.Modules.Workflow.Definitions;
using DynamicCube.Modules.Workflow.Domain;
using DynamicCube.Modules.Workflow.Persistence;
using DynamicCube.Platform.Persistence;
using DynamicCube.Platform.Security;
using DynamicCube.Platform.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Workflow.Runtime;

public enum InboxBox
{
    /// <summary>Assigned to me (or to someone I stand in for).</summary>
    Mine,

    /// <summary>Unassigned tasks I may claim.</summary>
    Queue,

    /// <summary>Unassigned tasks I distribute to my team.</summary>
    Distribute,

    /// <summary>Overdue tasks escalated to my role.</summary>
    Escalated,
}

public sealed record TaskSummary(
    Guid Id,
    Guid InstanceId,
    string NodeCode,
    string NameAr,
    string NameEn,
    WorkTaskStatus Status,
    Guid SubjectId,
    string SubjectType,
    string SubjectNumber,
    string TitleAr,
    string TitleEn,
    string PartyNameAr,
    string PartyNameEn,
    string? ServiceCode,
    string? LevelCode,
    string? Assignee,
    string? AssigneeNameAr,
    string? AssigneeNameEn,
    AssignmentMode Mode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt,
    SlaState Sla,
    string? CompletedBy,
    string? ActionCode,
    string? BranchCode = null);

public sealed record TaskPage(IReadOnlyList<TaskSummary> Items, int Total);

public sealed record InboxCounts(int Mine, int Queue, int Distribute, int Escalated);

public sealed record ActionView(string Code, ActionKind Kind, string NameAr, string NameEn, bool CommentRequired, bool VisibleToApplicant);

public sealed record TaskPermissions(bool CanClaim, bool CanRelease, bool CanAssign, bool CanAct, bool CanNote);

public sealed record TimelineItem(
    DateTimeOffset At,
    string Type,
    string Actor,
    string ActorNameAr,
    string ActorNameEn,
    string? OnBehalfOf,
    string? NodeCode,
    string? NodeNameAr,
    string? NodeNameEn,
    string? ActionCode,
    string? ActionNameAr,
    string? ActionNameEn,
    string? Target,
    string? TargetNameAr,
    string? TargetNameEn,
    string? Comment,
    bool VisibleToApplicant);

public sealed record InstanceView(
    Guid Id,
    string WorkflowCode,
    int VersionNumber,
    InstanceStatus Status,
    string? Outcome,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? DueAt,
    SlaState Sla,
    IReadOnlyList<TaskSummary> Tasks,
    IReadOnlyList<TimelineItem> Timeline,
    JsonObject Facts);

public sealed record TaskDetail(
    TaskSummary Task,
    string? CandidateRole,
    string? CandidateOrgUnit,
    string? CandidatePosition,
    string? CandidateUser,
    string? DistributorRole,
    string? Comment,
    DateTimeOffset? CompletedAt,
    string? OnBehalfOf,
    TaskPermissions Permissions,
    IReadOnlyList<ActionView> Actions,
    InstanceView Instance);

public sealed record ActRequest(string Action, string? Comment);

public sealed record AssignRequest(string UserName);

public sealed record NoteRequest(string Text);

public sealed record DelegationRequest(string Delegate, DateOnly FromDate, DateOnly ToDate, string? Reason);

public sealed record DelegationView(
    Guid Id,
    string Delegator,
    string DelegatorNameAr,
    string DelegatorNameEn,
    string Delegate,
    string DelegateNameAr,
    string DelegateNameEn,
    DateOnly FromDate, DateOnly ToDate, string? Reason, bool IsCurrent);

public sealed record StaffOption(string UserName, string NameAr, string NameEn, int OpenTasks);

/// <summary>
/// The employee's side of workflows: inboxes, the task view and what the current employee may do on a task (claim,
/// release, assign, act, note), out-of-office delegations, and supervision of all open tasks.
/// </summary>
internal sealed class ActionCenter(
    WorkflowDbContext context,
    WorkflowEngine engine,
    DefinitionCache definitions,
    Assignments assignments,
    IStaffDirectory staff,
    IAccessChecker access,
    IUnitOfWork unitOfWork,
    IServiceProvider services,
    TimeProvider clock)
{
    private readonly Dictionary<string, StaffMember?> _names = new(StringComparer.Ordinal);

    /// <param name="app">Only tasks of requests for the services of this business app.</param>
    public async Task<TaskPage> InboxAsync(InboxBox box, string? search, int skip, int take, CancellationToken cancellationToken, string? app = null)
    {
        var me = await MeAsync(cancellationToken);
        var query = Search(await BoxAsync(me, box, cancellationToken), search);
        if (!string.IsNullOrWhiteSpace(app))
        {
            var codes = services.GetService<IServiceCatalog>() is { } catalog ? await catalog.ServicesOfAppAsync(app, cancellationToken) : [];
            query = query.Where(t => t.ServiceCode != null && codes.Contains(t.ServiceCode));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(t => t.DueAt == null).ThenBy(t => t.DueAt).ThenBy(t => t.CreatedAt)
            .Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200)).ToListAsync(cancellationToken);
        return new TaskPage(await SummariesAsync(rows, cancellationToken), total);
    }

    public async Task<InboxCounts> CountsAsync(CancellationToken cancellationToken)
    {
        var me = await MeAsync(cancellationToken);
        return new InboxCounts(
            await (await BoxAsync(me, InboxBox.Mine, cancellationToken)).CountAsync(cancellationToken),
            await (await BoxAsync(me, InboxBox.Queue, cancellationToken)).CountAsync(cancellationToken),
            await (await BoxAsync(me, InboxBox.Distribute, cancellationToken)).CountAsync(cancellationToken),
            await (await BoxAsync(me, InboxBox.Escalated, cancellationToken)).CountAsync(cancellationToken));
    }

    /// <summary>Every open task, for supervisors.</summary>
    public async Task<TaskPage> SupervisionAsync(string? search, string? role, SlaState? sla, bool? unassigned, int skip, int take, CancellationToken cancellationToken)
    {
        var query = Search(InScope(context.Tasks.AsNoTracking().Where(t => t.Status == WorkTaskStatus.Open), await ScopeAsync(cancellationToken)), search);
        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(t => t.CandidateRole == role);
        }

        if (sla is not null)
        {
            query = query.Where(t => t.Sla == sla);
        }

        if (unassigned is not null)
        {
            query = unassigned.Value ? query.Where(t => t.Assignee == null) : query.Where(t => t.Assignee != null);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(t => t.DueAt == null).ThenBy(t => t.DueAt).ThenBy(t => t.CreatedAt)
            .Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200)).ToListAsync(cancellationToken);
        return new TaskPage(await SummariesAsync(rows, cancellationToken), total);
    }

    public async Task<TaskDetail> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var me = await MeAsync(cancellationToken);
        var task = await context.Tasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, cancellationToken) ?? throw NotFound();
        var who = await RightsAsync(me, task, cancellationToken);
        if (!who.CanViewTask(task))
        {
            throw new WorkflowForbiddenException("This task is not in your inboxes.");
        }

        var instance = await context.Instances.AsNoTracking().SingleAsync(i => i.Id == task.InstanceId, cancellationToken);
        var version = await definitions.GetAsync(context, instance.VersionId, cancellationToken);
        var node = version.Node(task.NodeCode);
        var permissions = who.Permissions(task);
        var actions = permissions.CanAct
            ? node.Actions.Select(a => new ActionView(a.Code, a.Kind, a.NameAr, a.NameEn, a.RequiresComment, a.IsVisibleToApplicant)).ToList()
            : [];
        return new TaskDetail(
            (await SummariesAsync([task], cancellationToken))[0],
            task.CandidateRole,
            task.CandidateOrgUnit,
            task.CandidatePosition,
            task.CandidateUser,
            task.DistributorRole,
            task.Comment,
            task.CompletedAt,
            task.Assignee is not null && task.Assignee != me.UserName && who.Delegators.Contains(task.Assignee) ? task.Assignee : null,
            permissions,
            actions,
            await InstanceViewAsync(instance, version, cancellationToken));
    }

    /// <summary>The workflow of a subject (e.g. on the request page), for employees of the Action Center.</summary>
    public async Task<InstanceView> InstanceAsync(string subjectType, Guid subjectId, CancellationToken cancellationToken)
    {
        await MeAsync(cancellationToken);
        var instance = await context.Instances.AsNoTracking().SingleOrDefaultAsync(i => i.SubjectType == subjectType && i.SubjectId == subjectId, cancellationToken)
            ?? throw new WorkflowNotFoundException("The request has no workflow.");
        return await InstanceViewAsync(instance, await definitions.GetAsync(context, instance.VersionId, cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<StaffOption>> CandidatesAsync(Guid id, CancellationToken cancellationToken)
    {
        var me = await MeAsync(cancellationToken);
        var task = await context.Tasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, cancellationToken) ?? throw NotFound();
        if (!(await RightsAsync(me, task, cancellationToken)).Permissions(task).CanAssign)
        {
            throw new WorkflowForbiddenException("You may not assign this task.");
        }

        var candidates = await assignments.CandidatesAsync(task, cancellationToken);
        var names = candidates.Select(c => c.UserName).ToList();
        var loads = await context.Tasks.AsNoTracking()
            .Where(t => t.Status == WorkTaskStatus.Open && t.Assignee != null && names.Contains(t.Assignee))
            .GroupBy(t => t.Assignee!)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        return [.. candidates.Select(c => new StaffOption(c.UserName, c.NameAr, c.NameEn, loads.GetValueOrDefault(c.UserName))).OrderBy(c => c.OpenTasks).ThenBy(c => c.UserName, StringComparer.Ordinal)];
    }

    public Task ClaimAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, (me, task, rights) =>
        {
            if (!rights.Permissions(task).CanClaim)
            {
                throw new WorkflowForbiddenException("You may not claim this task.");
            }

            Assign(task, me.UserName, me.UserName);
            return Event(task, me.UserName, WorkflowEventTypes.Claimed, target: me.UserName);
        }, cancellationToken);

    public Task ReleaseAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, (me, task, rights) =>
        {
            if (!rights.Permissions(task).CanRelease)
            {
                throw new WorkflowForbiddenException("You may not release this task.");
            }

            var previous = task.Assignee;
            task.Assignee = null;
            task.AssignedAt = null;
            task.AssignedBy = null;
            return Event(task, me.UserName, WorkflowEventTypes.Released, target: previous);
        }, cancellationToken);

    public async Task AssignAsync(Guid id, AssignRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var target = await staff.FindAsync(request.UserName, cancellationToken) ?? throw new WorkflowValidationException("userName", "No active employee has this user name.");
        await ChangeAsync(id, (me, task, rights) =>
        {
            if (!rights.Permissions(task).CanAssign)
            {
                throw new WorkflowForbiddenException("You may not assign this task.");
            }

            if (!Assignments.IsCandidate(task, target))
            {
                throw new WorkflowValidationException("userName", "This employee may not take the task.");
            }

            Assign(task, target.UserName, me.UserName);
            return Event(task, me.UserName, WorkflowEventTypes.Assigned, target: target.UserName);
        }, cancellationToken);
    }

    public async Task ActAsync(Guid id, ActRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var me = await MeAsync(cancellationToken);
        var task = await context.Tasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, cancellationToken) ?? throw NotFound();
        var rights = await RightsAsync(me, task, cancellationToken);
        if (!rights.Permissions(task).CanAct)
        {
            throw new WorkflowForbiddenException(task.Assignee is null ? "Claim the task first." : "The task is assigned to someone else.");
        }

        await engine.ActAsync(id, request.Action, request.Comment, me.UserName, task.Assignee == me.UserName ? null : task.Assignee, cancellationToken);
    }

    public async Task AddNoteAsync(Guid id, NoteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var text = request.Text?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > 4000)
        {
            throw new WorkflowValidationException("text", "Write a note of up to 4000 characters.");
        }

        var me = await MeAsync(cancellationToken);
        var task = await context.Tasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, cancellationToken) ?? throw NotFound();
        if (!(await RightsAsync(me, task, cancellationToken)).CanViewTask(task))
        {
            throw new WorkflowForbiddenException("This task is not in your inboxes.");
        }

        context.Events.Add(Event(task, me.UserName, WorkflowEventTypes.Note, comment: text));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DelegationView>> DelegationsAsync(CancellationToken cancellationToken)
    {
        var me = await MeAsync(cancellationToken);
        var rows = await context.Delegations.AsNoTracking()
            .Where(d => !d.IsCancelled && (d.Delegator == me.UserName || d.Delegate == me.UserName) && d.ToDate >= Today())
            .OrderBy(d => d.FromDate)
            .ToListAsync(cancellationToken);
        var views = new List<DelegationView>();
        foreach (var d in rows)
        {
            var (delegatorAr, delegatorEn) = await NameAsync(d.Delegator, cancellationToken);
            var (delegateAr, delegateEn) = await NameAsync(d.Delegate, cancellationToken);
            views.Add(new DelegationView(d.Id, d.Delegator, delegatorAr, delegatorEn, d.Delegate, delegateAr, delegateEn, d.FromDate, d.ToDate, d.Reason, d.FromDate <= Today()));
        }

        return views;
    }

    public async Task<DelegationView> DelegateAsync(DelegationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var me = await MeAsync(cancellationToken);
        if (request.Delegate == me.UserName)
        {
            throw new WorkflowValidationException("delegate", "Choose someone else.");
        }

        _ = await staff.FindAsync(request.Delegate, cancellationToken) ?? throw new WorkflowValidationException("delegate", "No active employee has this user name.");
        if (request.ToDate < request.FromDate || request.FromDate < Today())
        {
            throw new WorkflowValidationException("toDate", "The period must start today or later and end on or after its start.");
        }

        if (request.ToDate.DayNumber - request.FromDate.DayNumber > 180)
        {
            throw new WorkflowValidationException("toDate", "A delegation lasts at most 180 days.");
        }

        var delegation = new Delegation { Delegator = me.UserName, Delegate = request.Delegate, FromDate = request.FromDate, ToDate = request.ToDate, Reason = request.Reason?.Trim() };
        context.Delegations.Add(delegation);
        await context.SaveChangesAsync(cancellationToken);
        return (await DelegationsAsync(cancellationToken)).Single(d => d.Id == delegation.Id);
    }

    public async Task CancelDelegationAsync(Guid id, CancellationToken cancellationToken)
    {
        var me = await MeAsync(cancellationToken);
        var delegation = await context.Delegations.SingleOrDefaultAsync(d => d.Id == id && d.Delegator == me.UserName, cancellationToken)
            ?? throw new WorkflowNotFoundException("The delegation does not exist.");
        delegation.IsCancelled = true;
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Active employees, e.g. to choose a delegate.</summary>
    public async Task<IReadOnlyList<StaffOption>> StaffAsync(CancellationToken cancellationToken)
    {
        await MeAsync(cancellationToken);
        return [.. (await staff.SearchAsync(new StaffQuery(), cancellationToken)).Select(s => new StaffOption(s.UserName, s.NameAr, s.NameEn, 0))];
    }

    private async Task<StaffMember> MeAsync(CancellationToken cancellationToken) =>
        await staff.CurrentAsync(cancellationToken) ?? throw new WorkflowForbiddenException("Only employees use the Action Center.");

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    private async Task<List<string>> DelegatorsAsync(string me, CancellationToken cancellationToken)
    {
        var today = Today();
        return await context.Delegations.AsNoTracking()
            .Where(d => d.Delegate == me && !d.IsCancelled && d.FromDate <= today && d.ToDate >= today)
            .Select(d => d.Delegator)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private async Task<IQueryable<WorkTask>> BoxAsync(StaffMember me, InboxBox box, CancellationToken cancellationToken)
    {
        // Tasks of the company the employee works in (ADR-0036 step 6).
        var scope = await ScopeAsync(cancellationToken);
        var company = scope.Company;
        var open = context.Tasks.AsNoTracking().Where(t => t.Status == WorkTaskStatus.Open && (!scope.Restricted || t.CompanyCode == null || t.CompanyCode == company));
        var roles = me.Roles.ToList();

        // A role counts for a task's branch when held in every branch, or in that branch ("ROLE|BRANCH").
        var everyBranch = me.Holdings is null ? roles : me.Holdings.Where(h => h.Branches.Contains(RoleHolding.All)).Select(h => h.Role).Distinct().ToList();
        var inBranch = me.Holdings is null ? [] : me.Holdings.SelectMany(h => h.Branches.Where(b => b != RoleHolding.All).Select(b => h.Role + "|" + b)).Distinct().ToList();
        var units = me.OrgUnits.ToList();
        var positions = me.Positions.ToList();
        var user = me.UserName;
        switch (box)
        {
            case InboxBox.Mine:
                var assignees = await DelegatorsAsync(user, cancellationToken);
                assignees.Add(user);
                return open.Where(t => t.Assignee != null && assignees.Contains(t.Assignee));
            case InboxBox.Queue:
                return open.Where(t => t.Assignee == null && t.Mode != AssignmentMode.Distribute
                    && (t.CandidateUser != null || t.CandidateRole != null || t.CandidateOrgUnits.Count > 0 || t.CandidatePosition != null)
                    && (t.CandidateUser == null || t.CandidateUser == user)
                    && (t.CandidateRole == null || everyBranch.Contains(t.CandidateRole)
                        || (t.BranchCode == null ? roles.Contains(t.CandidateRole) : inBranch.Contains(t.CandidateRole + "|" + t.BranchCode)))
                    && (t.CandidateOrgUnits.Count == 0 || t.CandidateOrgUnits.Any(u => units.Contains(u)))
                    && (t.CandidatePosition == null || positions.Contains(t.CandidatePosition))
                    && !t.ExcludedUsers.Contains(user));
            case InboxBox.Distribute:
                return open.Where(t => t.Assignee == null && t.Mode == AssignmentMode.Distribute && t.DistributorRole != null
                    && (everyBranch.Contains(t.DistributorRole) || (t.BranchCode == null ? roles.Contains(t.DistributorRole) : inBranch.Contains(t.DistributorRole + "|" + t.BranchCode))));
            default:
                return open.Where(t => t.EscalatedToRole != null
                    && (everyBranch.Contains(t.EscalatedToRole) || (t.BranchCode == null ? roles.Contains(t.EscalatedToRole) : inBranch.Contains(t.EscalatedToRole + "|" + t.BranchCode))));
        }
    }

    private async Task<WorkScope> ScopeAsync(CancellationToken cancellationToken) =>
        services.GetService<IWorkScope>() is { } work ? await work.CurrentAsync(cancellationToken) : WorkScope.Unrestricted;

    /// <summary>Tasks of the company the employee works in and the branches their roles cover (tasks without a company stay visible).</summary>
    private static IQueryable<WorkTask> InScope(IQueryable<WorkTask> query, WorkScope scope)
    {
        if (!scope.Restricted)
        {
            return query;
        }

        var company = scope.Company;
        var branches = scope.Branches?.ToList();
        return query.Where(t => t.CompanyCode == null || (t.CompanyCode == company && (branches == null || t.BranchCode == null || branches.Contains(t.BranchCode))));
    }

    private static IQueryable<WorkTask> Search(IQueryable<WorkTask> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var pattern = "%" + search.Trim().Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
        return query.Where(t => EF.Functions.ILike(t.SubjectNumber, pattern) || EF.Functions.ILike(t.PartyNameAr, pattern) || EF.Functions.ILike(t.PartyNameEn, pattern));
    }

    private async Task<Rights> RightsAsync(StaffMember me, WorkTask task, CancellationToken cancellationToken)
    {
        var delegators = await DelegatorsAsync(me.UserName, cancellationToken);
        // Supervisors act on the tasks of their company and branches only.
        var inScope = (await ScopeAsync(cancellationToken)).Allows(task.CompanyCode, task.BranchCode);
        var supervisor = inScope && await access.CanAsync(WorkflowResources.Supervision, CatalogActions.View, cancellationToken);
        var reassigner = inScope && await access.CanAsync(WorkflowResources.Supervision, CatalogActions.Edit, cancellationToken);
        var involved = await context.Tasks.AsNoTracking().AnyAsync(t => t.InstanceId == task.InstanceId && (t.CompletedBy == me.UserName || t.Assignee == me.UserName), cancellationToken)
            || await context.Events.AsNoTracking().AnyAsync(e => e.InstanceId == task.InstanceId && e.Actor == me.UserName, cancellationToken);
        return new Rights(me, delegators, supervisor, reassigner, involved);
    }

    private async Task ChangeAsync(Guid id, Func<StaffMember, WorkTask, Rights, WorkflowEvent> change, CancellationToken cancellationToken)
    {
        var me = await MeAsync(cancellationToken);
        var instanceId = await context.Tasks.Where(t => t.Id == id).Select(t => (Guid?)t.InstanceId).SingleOrDefaultAsync(cancellationToken) ?? throw NotFound();
        await using var work = await unitOfWork.BeginAsync(cancellationToken);
        await context.LockAsync(instanceId, cancellationToken);
        var task = await context.Tasks.SingleAsync(t => t.Id == id, cancellationToken);
        context.Events.Add(change(me, task, await RightsAsync(me, task, cancellationToken)));
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await work.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new WorkflowConflictException("Someone else changed the task; reload it.");
        }
    }

    private void Assign(WorkTask task, string assignee, string by)
    {
        task.Assignee = assignee;
        task.AssignedAt = clock.GetUtcNow();
        task.AssignedBy = by;
        if (assignee != by)
        {
            TaskNotifications.Raise(context, clock, TaskNotifications.Assigned, task);
        }
    }

    private WorkflowEvent Event(WorkTask task, string actor, string type, string? target = null, string? comment = null) => new()
    {
        InstanceId = task.InstanceId,
        TaskId = task.Id,
        At = clock.GetUtcNow(),
        Actor = actor,
        Type = type,
        NodeCode = task.NodeCode,
        Target = target,
        Comment = comment,
    };

    private async Task<InstanceView> InstanceViewAsync(WorkflowInstance instance, ParsedVersion version, CancellationToken cancellationToken)
    {
        var tasks = await context.Tasks.AsNoTracking().Where(t => t.InstanceId == instance.Id).OrderBy(t => t.CreatedAt).ToListAsync(cancellationToken);
        var events = await context.Events.AsNoTracking().Where(e => e.InstanceId == instance.Id).OrderBy(e => e.At).ThenBy(e => e.Id).ToListAsync(cancellationToken);
        var timeline = new List<TimelineItem>();
        foreach (var e in events)
        {
            version.Nodes.TryGetValue(e.NodeCode ?? string.Empty, out var node);
            var action = node?.Actions.FirstOrDefault(a => a.Code == e.ActionCode);
            var (actorAr, actorEn) = await NameAsync(e.Actor, cancellationToken);
            var (targetAr, targetEn) = e.Target is null ? (null, null) : await NameAsync(e.Target, cancellationToken);
            timeline.Add(new TimelineItem(
                e.At, e.Type, e.Actor, actorAr, actorEn, e.OnBehalfOf, e.NodeCode, node?.NameAr, node?.NameEn,
                e.ActionCode, action?.NameAr, action?.NameEn, e.Target, targetAr, targetEn, e.Comment, e.VisibleToApplicant));
        }

        return new InstanceView(instance.Id, instance.WorkflowCode, instance.VersionNumber, instance.Status, instance.Outcome, instance.StartedAt, instance.CompletedAt,
            instance.DueAt, instance.Sla, await SummariesAsync(tasks, cancellationToken), timeline, JsonNode.Parse(instance.Facts)?.AsObject() ?? []);
    }

    private async Task<IReadOnlyList<TaskSummary>> SummariesAsync(List<WorkTask> tasks, CancellationToken cancellationToken)
    {
        var list = new List<TaskSummary>(tasks.Count);
        foreach (var t in tasks)
        {
            var (assigneeAr, assigneeEn) = t.Assignee is null ? (null, null) : await NameAsync(t.Assignee, cancellationToken);
            list.Add(new TaskSummary(t.Id, t.InstanceId, t.NodeCode, t.NameAr, t.NameEn, t.Status, t.SubjectId, t.SubjectType, t.SubjectNumber, t.TitleAr, t.TitleEn,
                t.PartyNameAr, t.PartyNameEn, t.ServiceCode, t.LevelCode, t.Assignee, assigneeAr, assigneeEn,
                t.Mode, t.CreatedAt, t.DueAt, t.Sla, t.CompletedBy, t.ActionCode, t.BranchCode));
        }

        return list;
    }

    /// <summary>Arabic and English display names of a user name (the user name itself for <c>system</c>, the applicant or a former employee), cached for the request.</summary>
    private async Task<(string Ar, string En)> NameAsync(string userName, CancellationToken cancellationToken)
    {
        if (userName is WorkflowEngine.System or WorkflowEngine.Applicant)
        {
            return (userName, userName);
        }

        if (!_names.TryGetValue(userName, out var member))
        {
            member = await staff.FindAsync(userName, cancellationToken);
            _names[userName] = member;
        }

        return member is null ? (userName, userName) : (member.NameAr, member.NameEn);
    }

    private static WorkflowNotFoundException NotFound() => new("The task does not exist.");

    /// <summary>What the current employee may do on a task.</summary>
    private sealed record Rights(StaffMember Me, List<string> Delegators, bool Supervisor, bool Reassigner, bool Involved)
    {
        public bool IsAssignee(WorkTask task) => task.Assignee is not null && (task.Assignee == Me.UserName || Delegators.Contains(task.Assignee));

        public bool IsDistributor(WorkTask task) => task.DistributorRole is not null && Me.Holds(task.DistributorRole, task.CompanyCode, task.BranchCode);

        public bool CanViewTask(WorkTask task) =>
            Supervisor || Involved || IsAssignee(task) || Assignments.IsCandidate(task, Me) || IsDistributor(task)
            || (task.EscalatedToRole is not null && Me.Holds(task.EscalatedToRole, task.CompanyCode, task.BranchCode));

        public TaskPermissions Permissions(WorkTask task)
        {
            var open = task.Status == WorkTaskStatus.Open;
            return new TaskPermissions(
                CanClaim: open && task.Assignee is null && task.Mode != AssignmentMode.Distribute && Assignments.IsCandidate(task, Me),
                CanRelease: open && task.Assignee is not null && (IsAssignee(task) || Reassigner),
                CanAssign: open && ((task.Mode == AssignmentMode.Distribute && IsDistributor(task)) || Reassigner),
                CanAct: open && IsAssignee(task),
                CanNote: CanViewTask(task));
        }
    }
}
