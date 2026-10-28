using System.Text.Json.Nodes;
using DynamicCube.Modules.Workflow.Definitions;
using DynamicCube.Modules.Workflow.Domain;
using DynamicCube.Modules.Workflow.Persistence;
using DynamicCube.Platform.Calendars;
using DynamicCube.Platform.Decisions;
using DynamicCube.Platform.Documents;
using DynamicCube.Platform.Expressions;
using DynamicCube.Platform.Persistence;
using DynamicCube.Platform.Security;
using DynamicCube.Platform.Workflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Workflow.Runtime;

/// <summary>
/// Runs workflow instances: tokens move through the graph until they reach human tasks, which wait for an action.
/// Each operation locks its instance, saves its changes and sets the subject's status in one unit of work.
/// </summary>
internal sealed class WorkflowEngine(
    WorkflowDbContext context,
    DefinitionCache definitions,
    Assignments assignments,
    IEnumerable<IWorkflowSubject> subjects,
    IWorkingCalendarProvider calendars,
    IUnitOfWork unitOfWork,
    JsonLogicEvaluator evaluator,
    IServiceProvider services,
    TimeProvider clock) : IWorkflowRuntime
{
    public const string System = "system";
    public const string Applicant = "applicant";
    private const int MaxSteps = 500;

    public async Task<bool> CanStartAsync(string workflowCode, CancellationToken cancellationToken) =>
        await CurrentVersionIdAsync(workflowCode, cancellationToken) is not null;

    public async Task StartAsync(WorkflowStart start, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(start);
        var versionId = await CurrentVersionIdAsync(start.WorkflowCode, cancellationToken)
            ?? throw new WorkflowConflictException($"Workflow '{start.WorkflowCode}' has no published version.");
        await using var work = await unitOfWork.BeginAsync(cancellationToken);
        if (await context.Instances.AnyAsync(i => i.SubjectType == start.SubjectType && i.SubjectId == start.SubjectId, cancellationToken))
        {
            throw new WorkflowConflictException($"{start.SubjectNumber} already has a workflow.");
        }

        var version = await definitions.GetAsync(context, versionId, cancellationToken);
        var calendar = await calendars.GetAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var sla = start.SlaWorkingDays ?? version.Definition.SlaWorkingDays;
        var instance = new WorkflowInstance
        {
            WorkflowCode = version.WorkflowCode,
            VersionId = version.Id,
            VersionNumber = version.Number,
            SubjectType = start.SubjectType,
            SubjectId = start.SubjectId,
            SubjectNumber = start.SubjectNumber,
            TitleAr = start.TitleAr,
            TitleEn = start.TitleEn,
            PartyNameAr = start.PartyNameAr,
            PartyNameEn = start.PartyNameEn,
            ServiceCode = start.ServiceCode,
            LevelCode = start.LevelCode,
            Status = InstanceStatus.Running,
            StartedAt = now,
            SlaWorkingDays = sla,
            DueAt = sla is { } days ? calendar.AddWorkingDays(now, days) : null,
        };
        context.Instances.Add(instance);
        var run = new Run(instance, version, calendar, now, System, null, [], []);
        run.Event(WorkflowEventTypes.Started, null, null);
        var token = run.NewToken(version.Definition.Nodes.Single(n => n.Type == NodeType.Start).Code, []);
        await AdvanceAsync(run, token, cancellationToken);
        await FinishAsync(run, cancellationToken);
        await work.CommitAsync(cancellationToken);
    }

    public async Task ResumeAsync(string subjectType, Guid subjectId, CancellationToken cancellationToken)
    {
        await using var work = await unitOfWork.BeginAsync(cancellationToken);
        var id = await context.Instances.Where(i => i.SubjectType == subjectType && i.SubjectId == subjectId).Select(i => (Guid?)i.Id).SingleOrDefaultAsync(cancellationToken)
            ?? throw new WorkflowNotFoundException("The request has no workflow.");
        var run = await LoadAsync(id, Applicant, null, cancellationToken);
        var waiting = run.Tokens.Where(t => t.Status == TokenStatus.WaitingCustomer).ToList();
        if (waiting.Count == 0)
        {
            throw new WorkflowConflictException("The request is not waiting for the applicant.");
        }

        var instance = run.Instance;
        if (instance.PausedAt is { } paused && instance.DueAt is { } due)
        {
            // The SLA clock stood still while the applicant had the request.
            instance.DueAt = run.Calendar.AddWorkingDays(due, run.Calendar.WorkingDaysBetween(paused, run.Now));
        }

        instance.PausedAt = null;
        run.Event(WorkflowEventTypes.Resubmitted, null, null, visible: true);
        run.Update(SubjectStatus.InProgress, "UNDER_REVIEW", "RESUBMITTED", null);
        foreach (var token in waiting)
        {
            token.Status = TokenStatus.Active;
            token.NodeCode = token.ResumeAt ?? token.NodeCode;
            token.ResumeAt = null;
            run.Resuming.Add(token.Id);
            await AdvanceAsync(run, token, cancellationToken);
        }

        await FinishAsync(run, cancellationToken);
        await work.CommitAsync(cancellationToken);
    }

    /// <summary>Completes an open task with one of its actions and moves its branch on. The caller checked who may act.</summary>
    public async Task ActAsync(Guid taskId, string actionCode, string? comment, string actor, string? onBehalfOf, CancellationToken cancellationToken)
    {
        await using var work = await unitOfWork.BeginAsync(cancellationToken);
        var instanceId = await context.Tasks.Where(t => t.Id == taskId).Select(t => (Guid?)t.InstanceId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new WorkflowNotFoundException("The task does not exist.");
        var run = await LoadAsync(instanceId, actor, onBehalfOf, cancellationToken);
        var task = run.Tasks.Single(t => t.Id == taskId);
        if (task.Status != WorkTaskStatus.Open)
        {
            throw new WorkflowConflictException("The task is already closed.");
        }

        var node = run.Version.Node(task.NodeCode);
        var action = node.Actions.SingleOrDefault(a => a.Code == actionCode)
            ?? throw new WorkflowConflictException($"The task has no action '{actionCode}'.");
        comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (action.RequiresComment && comment is null)
        {
            throw new WorkflowValidationException("comment", "A comment is required for this action.");
        }

        task.Status = WorkTaskStatus.Completed;
        task.CompletedAt = run.Now;
        task.CompletedBy = actor;
        task.ActionCode = action.Code;
        task.Comment = comment;
        run.Event(WorkflowEventTypes.Action, task, action.Code, comment, visible: action.IsVisibleToApplicant && comment is not null);

        var token = run.Tokens.Single(t => t.Id == task.TokenId);
        if (action.Kind == ActionKind.Return)
        {
            token.Status = TokenStatus.WaitingCustomer;
            token.ResumeAt = action.ResumeAt ?? node.Code;
            run.Instance.PausedAt ??= run.Now;
            run.Event(WorkflowEventTypes.Returned, task, action.Code, comment, visible: true);
            run.Update(SubjectStatus.PendingCustomer, action.DisplayStatus ?? "RETURNED", "RETURNED", comment);
        }
        else
        {
            if (action.IsVisibleToApplicant && comment is not null)
            {
                run.Reason = comment;
            }

            if (action.DisplayStatus is not null)
            {
                run.Update(SubjectStatus.InProgress, action.DisplayStatus, "STATUS_CHANGED", null);
            }

            token.NodeCode = action.To!;
            await AdvanceAsync(run, token, cancellationToken);
        }

        await FinishAsync(run, cancellationToken);
        await work.CommitAsync(cancellationToken);
    }

    private async Task AdvanceAsync(Run run, WorkflowToken token, CancellationToken cancellationToken)
    {
        if (++run.Steps > MaxSteps)
        {
            throw new WorkflowConflictException($"Workflow {run.Version.WorkflowCode}/{run.Version.Number} loops without reaching a task.");
        }

        var node = run.Version.Node(token.NodeCode);
        switch (node.Type)
        {
            case NodeType.Start:
                token.NodeCode = node.Next!;
                await AdvanceAsync(run, token, cancellationToken);
                break;

            case NodeType.HumanTask:
                await CreateTaskAsync(run, token, node, cancellationToken);
                break;

            case NodeType.ParallelSplit:
                token.Status = TokenStatus.Done;
                var fork = new ForkFrame(Guid.CreateVersion7(), node.Code, node.Branches.Count);
                var branches = node.Branches.Select(b => run.NewToken(b, [.. token.Forks, fork])).ToList();
                foreach (var branch in branches)
                {
                    await AdvanceAsync(run, branch, cancellationToken);
                }

                break;

            case NodeType.Join:
                await JoinAsync(run, token, node, cancellationToken);
                break;

            case NodeType.Gateway:
                var facts = await RuleContextAsync(run, cancellationToken);
                var route = node.Routes.FirstOrDefault(r => JsonLogicEvaluator.IsTruthy(evaluator.Evaluate(r.When, facts)));
                token.NodeCode = route?.To ?? node.Default!;
                await AdvanceAsync(run, token, cancellationToken);
                break;

            case NodeType.Decision:
                await DecideAsync(run, node, cancellationToken);
                token.NodeCode = node.Next!;
                await AdvanceAsync(run, token, cancellationToken);
                break;

            case NodeType.Document:
                await IssueAsync(run, node, cancellationToken);
                token.NodeCode = node.Next!;
                await AdvanceAsync(run, token, cancellationToken);
                break;

            case NodeType.End:
                token.Status = TokenStatus.Done;
                Complete(run, node);
                break;
        }
    }

    /// <summary>Evaluates the node's rule set (Decisions module) and keeps the results as <c>decisions.&lt;result&gt;</c>.</summary>
    private async Task DecideAsync(Run run, NodeDefinition node, CancellationToken cancellationToken)
    {
        var decisions = services.GetService<IDecisionService>() ?? throw new WorkflowConflictException("Decision rules are not installed.");
        var facts = await RuleContextAsync(run, cancellationToken);
        var input = node.Input.Count == 0 ? facts : Map(node.Input, facts);
        DecisionResult result;
        try
        {
            result = await decisions.EvaluateAsync(new DecisionRequest(node.RuleSet!, input, run.Instance.SubjectType, run.Instance.SubjectId, run.Instance.SubjectNumber), cancellationToken);
        }
        catch (DecisionException ex)
        {
            throw new WorkflowConflictException($"Step '{node.Code}': {ex.Message}");
        }

        var output = (JsonObject)result.Output.DeepClone();
        output["evaluationId"] = result.EvaluationId.ToString();
        output["version"] = result.Version;
        SetFact(run, "decisions", node.Result ?? node.Code, output);
        run.Event(WorkflowEventTypes.Decided, null, null, $"{result.RuleSetCode}/{result.Version}", node: node.Code);
    }

    /// <summary>Issues the node's document (Documents module) for the holder and keeps its number as <c>documents.&lt;node&gt;</c>.</summary>
    private async Task IssueAsync(Run run, NodeDefinition node, CancellationToken cancellationToken)
    {
        var issuer = services.GetService<IDocumentIssuer>() ?? throw new WorkflowConflictException("Documents are not installed.");
        var facts = await RuleContextAsync(run, cancellationToken);
        var holderValue = evaluator.Evaluate(node.Holder ?? new JsonObject { ["var"] = "party.id" }, facts);
        if (holderValue is not JsonValue holderText || !Guid.TryParse(holderText.ToString(), out var holder))
        {
            throw new WorkflowConflictException($"Step '{node.Code}': the holder is not a party id.");
        }

        var lines = new JsonArray();
        if (node.Lines is { } definition)
        {
            var name = definition.As ?? "item";
            foreach (var element in evaluator.Evaluate(definition.Source, facts) as JsonArray ?? [])
            {
                var scope = (JsonObject)facts.DeepClone();
                scope[name] = element?.DeepClone();
                lines.Add(Map(definition.Fields, scope));
            }
        }

        IssuedDocumentInfo issued;
        try
        {
            issued = await issuer.IssueAsync(
                new DocumentIssue(node.DocumentType!, holder, Map(node.Data, facts), lines, run.Instance.SubjectType, run.Instance.SubjectId, run.Instance.SubjectNumber, node.Reason),
                cancellationToken);
        }
        catch (DocumentIssueException ex)
        {
            throw new WorkflowConflictException($"Step '{node.Code}': {ex.Message}");
        }

        SetFact(run, "documents", node.Code, new JsonObject
        {
            ["id"] = issued.Id.ToString(),
            ["number"] = issued.Number,
            ["version"] = issued.Version,
            ["amended"] = issued.Amended,
        });
        run.Event(issued.Amended ? WorkflowEventTypes.DocumentAmended : WorkflowEventTypes.DocumentIssued, null, null, $"{issued.Number} v{issued.Version}", node: node.Code);
    }

    private JsonObject Map(Dictionary<string, JsonNode?> fields, JsonObject facts) =>
        new(fields.Select(f => KeyValuePair.Create(f.Key, evaluator.Evaluate(f.Value, facts)?.DeepClone())));

    private static void SetFact(Run run, string group, string key, JsonNode value)
    {
        var facts = JsonNode.Parse(run.Instance.Facts)?.AsObject() ?? [];
        if (facts[group] is not JsonObject entries)
        {
            entries = [];
            facts[group] = entries;
        }

        entries[key] = value;
        run.Instance.Facts = facts.ToJsonString();
    }

    private async Task JoinAsync(Run run, WorkflowToken token, NodeDefinition node, CancellationToken cancellationToken)
    {
        if (token.Forks.Count == 0)
        {
            // Not inside a split: the join simply passes on.
            token.NodeCode = node.Next!;
            await AdvanceAsync(run, token, cancellationToken);
            return;
        }

        token.Status = TokenStatus.AtJoin;
        var fork = token.Forks[^1];
        var arrived = run.Tokens.Where(t => t.Status == TokenStatus.AtJoin && t.NodeCode == node.Code && t.Forks.Count > 0 && t.Forks[^1].Id == fork.Id).ToList();
        var needed = node.Mode switch
        {
            JoinMode.Any => 1,
            JoinMode.Count => Math.Min(node.Count ?? fork.Branches, fork.Branches),
            _ => fork.Branches,
        };
        if (arrived.Count < needed)
        {
            return;
        }

        foreach (var done in arrived)
        {
            done.Status = TokenStatus.Done;
        }

        // Branches still running (join any / count) are no longer needed.
        foreach (var other in run.Tokens.Where(t => (t.Status is TokenStatus.Active or TokenStatus.WaitingCustomer or TokenStatus.AtJoin) && t.Forks.Any(f => f.Id == fork.Id)).ToList())
        {
            CancelToken(run, other);
        }

        var next = run.NewToken(node.Next!, [.. token.Forks.Take(token.Forks.Count - 1)]);
        await AdvanceAsync(run, next, cancellationToken);
    }

    private async Task CreateTaskAsync(Run run, WorkflowToken token, NodeDefinition node, CancellationToken cancellationToken)
    {
        var instance = run.Instance;
        var assignment = node.Assignment!;
        var task = new WorkTask
        {
            InstanceId = instance.Id,
            TokenId = token.Id,
            NodeCode = node.Code,
            NameAr = node.NameAr,
            NameEn = node.NameEn,
            Status = WorkTaskStatus.Open,
            Mode = assignment.Mode,
            DistributorRole = assignment.Mode == AssignmentMode.Distribute ? assignment.DistributorRole : null,
            ExcludedUsers = [.. run.Tasks.Where(t => t.Status == WorkTaskStatus.Completed && node.ExcludeActorsOf.Contains(t.NodeCode) && t.CompletedBy is not null).Select(t => t.CompletedBy!).Distinct()],
            CreatedAt = run.Now,
            SubjectId = instance.SubjectId,
            SubjectType = instance.SubjectType,
            SubjectNumber = instance.SubjectNumber,
            TitleAr = instance.TitleAr,
            TitleEn = instance.TitleEn,
            PartyNameAr = instance.PartyNameAr,
            PartyNameEn = instance.PartyNameEn,
            ServiceCode = instance.ServiceCode,
            LevelCode = instance.LevelCode,
        };

        string? direct = null;
        switch (assignment.Target)
        {
            case TargetKind.User:
                task.CandidateUser = assignment.User;
                direct = assignment.User;
                break;
            case TargetKind.PreviousActor:
                direct = LastActor(run, assignment.Step!);
                if (direct is not null && !task.ExcludedUsers.Contains(direct))
                {
                    task.CandidateUser = direct;
                }
                else
                {
                    direct = null;
                    SetRole(task, assignment);
                }

                break;
            default:
                SetRole(task, assignment);
                break;
        }

        // A task resumed after the applicant's correction goes back to whoever returned it.
        if (run.Resuming.Contains(token.Id) && LastActor(run, node.Code) is { } previous && !task.ExcludedUsers.Contains(previous))
        {
            direct = previous;
        }

        direct ??= assignment.Mode == AssignmentMode.Auto ? await assignments.LeastLoadedAsync(task, cancellationToken) : null;
        if (direct is not null && !task.ExcludedUsers.Contains(direct))
        {
            task.Assignee = direct;
            task.AssignedAt = run.Now;
            task.AssignedBy = System;
        }

        var days = node.Sla is { } sla ? (instance.LevelCode is { } level && sla.ByLevel.TryGetValue(level, out var byLevel) ? byLevel : sla.WorkingDays) : null;
        if (days is { } d)
        {
            task.DueAt = run.Calendar.AddWorkingDays(run.Now, d);
            task.WarnAt = run.Now + ((task.DueAt.Value - run.Now) * (node.Sla!.WarnPercent / 100.0));
        }

        context.Tasks.Add(task);
        run.Tasks.Add(task);
        run.Event(WorkflowEventTypes.TaskCreated, task, null, null);
        if (task.Assignee is not null)
        {
            run.Event(WorkflowEventTypes.Assigned, task, null, null, target: task.Assignee);
        }

        run.Update(SubjectStatus.InProgress, node.DisplayStatus ?? "UNDER_REVIEW", "STATUS_CHANGED", null);
    }

    private static void SetRole(WorkTask task, TaskAssignment assignment)
    {
        task.CandidateRole = assignment.Role;
        task.CandidateOrgUnit = assignment.OrgUnit;
        task.CandidatePosition = assignment.Position;
    }

    private static string? LastActor(Run run, string nodeCode) =>
        run.Tasks.Where(t => t.NodeCode == nodeCode && t.Status == WorkTaskStatus.Completed).OrderByDescending(t => t.CompletedAt).Select(t => t.CompletedBy).FirstOrDefault();

    private static void Complete(Run run, NodeDefinition node)
    {
        foreach (var token in run.Tokens.Where(t => t.Status is TokenStatus.Active or TokenStatus.WaitingCustomer or TokenStatus.AtJoin).ToList())
        {
            CancelToken(run, token);
        }

        var outcome = node.Outcome ?? EndOutcome.Closed;
        var name = outcome.ToString().ToUpperInvariant();
        run.Instance.Status = InstanceStatus.Completed;
        run.Instance.Outcome = name;
        run.Instance.CompletedAt = run.Now;
        run.Instance.PausedAt = null;
        run.Event(WorkflowEventTypes.Completed, null, null, run.Reason, visible: true, target: name);
        var status = outcome switch
        {
            EndOutcome.Approved => SubjectStatus.Approved,
            EndOutcome.Rejected => SubjectStatus.Rejected,
            _ => SubjectStatus.Closed,
        };
        run.Update(status, node.DisplayStatus ?? name, name, run.Reason);
    }

    private static void CancelToken(Run run, WorkflowToken token)
    {
        token.Status = TokenStatus.Cancelled;
        foreach (var task in run.Tasks.Where(t => t.TokenId == token.Id && t.Status == WorkTaskStatus.Open))
        {
            task.Status = WorkTaskStatus.Cancelled;
            task.CompletedAt = run.Now;
            run.Event(WorkflowEventTypes.TaskCancelled, task, null, null);
        }
    }

    /// <summary>What rules see: the subject's facts, the last action taken on each task, and decisions and documents so far.</summary>
    private async Task<JsonObject> RuleContextAsync(Run run, CancellationToken cancellationToken)
    {
        var facts = await Subject(run.Instance.SubjectType).GetRuleContextAsync(run.Instance.SubjectId, cancellationToken);
        var actions = new JsonObject();
        foreach (var task in run.Tasks.Where(t => t.Status == WorkTaskStatus.Completed).OrderBy(t => t.CompletedAt))
        {
            actions[task.NodeCode] = task.ActionCode;
        }

        facts["actions"] = actions;
        facts["level"] ??= run.Instance.LevelCode;
        var produced = JsonNode.Parse(run.Instance.Facts)?.AsObject() ?? [];
        facts["decisions"] = produced["decisions"]?.DeepClone() ?? new JsonObject();
        facts["documents"] = produced["documents"]?.DeepClone() ?? new JsonObject();
        return facts;
    }

    private async Task<Run> LoadAsync(Guid instanceId, string actor, string? onBehalfOf, CancellationToken cancellationToken)
    {
        // One operation at a time per instance, so parallel branches arriving at a join see each other.
        await context.LockAsync(instanceId, cancellationToken);
        var instance = await context.Instances.SingleAsync(i => i.Id == instanceId, cancellationToken);
        if (instance.Status is InstanceStatus.Completed or InstanceStatus.Cancelled)
        {
            throw new WorkflowConflictException("The workflow has finished.");
        }

        var version = await definitions.GetAsync(context, instance.VersionId, cancellationToken);
        var tokens = await context.Tokens.Where(t => t.InstanceId == instanceId).ToListAsync(cancellationToken);
        var tasks = await context.Tasks.Where(t => t.InstanceId == instanceId).ToListAsync(cancellationToken);
        return new Run(instance, version, await calendars.GetAsync(cancellationToken), clock.GetUtcNow(), actor, onBehalfOf, tokens, tasks);
    }

    private async Task FinishAsync(Run run, CancellationToken cancellationToken)
    {
        var instance = run.Instance;
        if (instance.Status != InstanceStatus.Completed)
        {
            instance.Status = run.Tokens.Any(t => t.Status == TokenStatus.WaitingCustomer) ? InstanceStatus.WaitingCustomer : InstanceStatus.Running;
        }

        foreach (var token in run.NewTokens)
        {
            context.Tokens.Add(token);
        }

        context.Events.AddRange(run.Events);
        await context.SaveChangesAsync(cancellationToken);

        // While a branch waits for the applicant, the request shows that, whatever the other branches do.
        if (run.Pending is { } update && !(instance.Status == InstanceStatus.WaitingCustomer && update.Status == SubjectStatus.InProgress))
        {
            await Subject(instance.SubjectType).UpdateAsync(instance.SubjectId, update, cancellationToken);
        }
    }

    private IWorkflowSubject Subject(string type) =>
        subjects.FirstOrDefault(s => s.SubjectType == type) ?? throw new InvalidOperationException($"No workflow subject of type '{type}' is registered.");

    private Task<Guid?> CurrentVersionIdAsync(string workflowCode, CancellationToken cancellationToken) =>
        context.Versions.AsNoTracking()
            .Where(v => v.Workflow!.Code == workflowCode && v.Workflow.IsActive && v.IsActive && !v.IsRetired && v.PublishedAt != null)
            .OrderByDescending(v => v.Number)
            .Select(v => (Guid?)v.Id)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>State of one engine operation on one instance.</summary>
    private sealed class Run(
        WorkflowInstance instance,
        ParsedVersion version,
        WorkingCalendar calendar,
        DateTimeOffset now,
        string actor,
        string? onBehalfOf,
        List<WorkflowToken> tokens,
        List<WorkTask> tasks)
    {
        public WorkflowInstance Instance { get; } = instance;

        public ParsedVersion Version { get; } = version;

        public WorkingCalendar Calendar { get; } = calendar;

        public DateTimeOffset Now { get; } = now;

        public List<WorkflowToken> Tokens { get; } = tokens;

        public List<WorkflowToken> NewTokens { get; } = [];

        public List<WorkTask> Tasks { get; } = tasks;

        public List<WorkflowEvent> Events { get; } = [];

        /// <summary>Tokens resumed after the applicant's correction in this operation.</summary>
        public HashSet<Guid> Resuming { get; } = [];

        /// <summary>The applicant-visible reason of the last action (e.g. a rejection), for the final status.</summary>
        public string? Reason { get; set; }

        public SubjectUpdate? Pending { get; private set; }

        public int Steps { get; set; }

        public WorkflowToken NewToken(string node, List<ForkFrame> forks)
        {
            var token = new WorkflowToken { InstanceId = Instance.Id, NodeCode = node, Status = TokenStatus.Active, Forks = forks, CreatedAt = Now };
            Tokens.Add(token);
            NewTokens.Add(token);
            return token;
        }

        public void Update(SubjectStatus status, string displayStatus, string eventType, string? comment) =>
            Pending = new SubjectUpdate(status, displayStatus, eventType, comment);

        public void Event(string type, WorkTask? task, string? action, string? comment = null, bool visible = false, string? target = null, string? node = null) =>
            Events.Add(new WorkflowEvent
            {
                InstanceId = Instance.Id,
                TaskId = task?.Id,
                At = Now,
                Actor = actor,
                OnBehalfOf = onBehalfOf,
                Type = type,
                NodeCode = task?.NodeCode ?? node,
                ActionCode = action,
                Target = target,
                Comment = comment,
                VisibleToApplicant = visible,
            });
    }
}

public sealed class WorkflowNotFoundException(string message) : Exception(message);

public sealed class WorkflowConflictException(string message) : Exception(message);

/// <summary>The current user may not do this on the task.</summary>
public sealed class WorkflowForbiddenException(string message) : Exception(message);

public sealed class WorkflowValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
