using System.Text.Json.Nodes;
using DynamicCube.Modules.Workflow.Domain;
using DynamicCube.Modules.Workflow.Persistence;
using DynamicCube.Platform.Expressions;
using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Workflow.Runtime;

/// <summary>What an automation rule's condition reads about an open task.</summary>
internal static class AutomationFacts
{
    public static JsonObject Sample { get; } = Build(new WorkTask { NodeCode = "REVIEW", CreatedAt = DateTimeOffset.UnixEpoch }, "WF", DateTimeOffset.UnixEpoch);

    /// <param name="now">Hours are counted to this time.</param>
    public static JsonObject Build(WorkTask task, string workflow, DateTimeOffset now)
    {
        static double Hours(TimeSpan span) => Math.Round(span.TotalHours, 2);
        return new JsonObject
        {
            ["task"] = new JsonObject
            {
                ["step"] = task.NodeCode,
                ["workflow"] = workflow,
                ["service"] = task.ServiceCode,
                ["company"] = task.CompanyCode,
                ["branch"] = task.BranchCode,
                ["level"] = task.LevelCode,
                ["role"] = task.CandidateRole,
                ["assignee"] = task.Assignee,
                ["assigned"] = task.Assignee is not null,
                ["ageHours"] = Hours(now - task.CreatedAt),
                ["unassignedHours"] = task.Assignee is null ? Hours(now - task.CreatedAt) : 0,
                ["assignedHours"] = task.AssignedAt is { } at ? Hours(now - at) : 0,
                ["dueInHours"] = task.DueAt is { } due ? Hours(due - now) : null,
                ["overdue"] = task.DueAt is { } d && d <= now,
                ["sla"] = task.Sla switch { SlaState.Warning => "WARNING", SlaState.Breached => "BREACHED", _ => "ON_TRACK" },
                ["escalated"] = task.EscalatedToRole is not null,
            },
        };
    }
}

/// <summary>A task an automation rule acts on (or would act on, in a test).</summary>
public sealed record AutomationMatch(Guid TaskId, string SubjectNumber, string Step, string NameAr, string NameEn, string? Assignee, string? Detail);

/// <summary>
/// Runs the active automation rules on open tasks (the worker, every minute): when a rule's condition holds on a task and
/// it has not acted on it yet (or its repeat time has passed), its action is taken under the instance lock and logged.
/// A test runs one rule (saved or not) without changing anything and lists the tasks it would act on.
/// </summary>
internal sealed class AutomationRunner(
    WorkflowDbContext context,
    Assignments assignments,
    JsonLogicEvaluator evaluator,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    private const int BatchSize = 500;
    public const string Actor = "automation";
    private static readonly TimeSpan RetryAfter = TimeSpan.FromHours(1);

    /// <summary>Returns how many actions were taken.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var rules = await context.AutomationRules.AsNoTracking().Where(r => r.IsActive).OrderBy(r => r.DisplayOrder).ThenBy(r => r.Code).ToListAsync(cancellationToken);
        var done = 0;
        foreach (var rule in rules)
        {
            foreach (var (task, _) in await MatchesAsync(rule, skipActed: true, cancellationToken))
            {
                done += await ActAsync(rule, task, cancellationToken) ? 1 : 0;
            }
        }

        return done;
    }

    /// <summary>The open tasks the rule would act on now (nothing changes).</summary>
    public async Task<IReadOnlyList<AutomationMatch>> TestAsync(AutomationRule rule, CancellationToken cancellationToken)
    {
        var matches = await MatchesAsync(rule, skipActed: false, cancellationToken);
        var result = new List<AutomationMatch>();
        foreach (var (task, acted) in matches)
        {
            var detail = rule.Action switch
            {
                AutomationAction.AssignLeastLoaded => await assignments.LeastLoadedAsync(task, cancellationToken) is { } user ? $"Assign to {user}" : "No candidate",
                AutomationAction.Remind => "Remind",
                _ => $"Escalate to {rule.EscalateToRole}",
            };
            result.Add(new AutomationMatch(task.Id, task.SubjectNumber, task.NodeCode, task.NameAr, task.NameEn, task.Assignee, acted ? $"{detail} (already done)" : detail));
        }

        return result;
    }

    private async Task<List<(WorkTask Task, bool Acted)>> MatchesAsync(AutomationRule rule, bool skipActed, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var condition = JsonNode.Parse(rule.Condition);
        var query = context.Tasks.AsNoTracking().Where(t => t.Status == WorkTaskStatus.Open);
        if (rule.Steps.Count > 0)
        {
            query = query.Where(t => rule.Steps.Contains(t.NodeCode));
        }

        if (rule.Action == AutomationAction.AssignLeastLoaded)
        {
            query = query.Where(t => t.Assignee == null);
        }

        var instances = context.Instances.AsNoTracking();
        if (rule.Workflows.Count > 0)
        {
            instances = instances.Where(i => rule.Workflows.Contains(i.WorkflowCode));
        }

        var rows = await query.Join(instances, t => t.InstanceId, i => i.Id, (t, i) => new { Task = t, i.WorkflowCode })
            .OrderBy(x => x.Task.CreatedAt).Take(BatchSize).ToListAsync(cancellationToken);
        var ids = rows.Select(r => r.Task.Id).ToList();
        // Done counts until the repeat time; a failed or skipped try is retried after an hour, not every minute.
        var since = rule.RepeatHours is { } hours ? now.AddHours(-hours) : DateTimeOffset.MinValue;
        var retry = now.Add(-RetryAfter);
        var acted = (await context.AutomationLogs.AsNoTracking()
            .Where(l => l.RuleCode == rule.Code && ids.Contains(l.TaskId) && (l.Outcome == AutomationOutcome.Done ? l.At > since : l.At > retry))
            .Select(l => l.TaskId).ToListAsync(cancellationToken)).ToHashSet();
        var matches = new List<(WorkTask, bool)>();
        foreach (var row in rows)
        {
            if (skipActed && acted.Contains(row.Task.Id))
            {
                continue;
            }

            bool holds;
            try
            {
                holds = JsonLogicEvaluator.IsTruthy(evaluator.Evaluate(condition, AutomationFacts.Build(row.Task, row.WorkflowCode, now)));
            }
            catch (JsonLogicException)
            {
                holds = false;
            }

            if (holds)
            {
                matches.Add((row.Task, acted.Contains(row.Task.Id)));
            }
        }

        return matches;
    }

    /// <summary>Takes the action under the instance lock (the task may have moved on since it was read) and logs it.</summary>
    private async Task<bool> ActAsync(AutomationRule rule, WorkTask read, CancellationToken cancellationToken)
    {
        await using var work = await unitOfWork.BeginAsync(cancellationToken);
        await context.LockAsync(read.InstanceId, cancellationToken);
        var task = await context.Tasks.SingleOrDefaultAsync(t => t.Id == read.Id && t.Status == WorkTaskStatus.Open, cancellationToken);
        if (task is null)
        {
            return false;
        }

        var now = clock.GetUtcNow();
        var (outcome, detail, eventType, target) = rule.Action switch
        {
            AutomationAction.AssignLeastLoaded => await AssignAsync(task, now, cancellationToken),
            AutomationAction.Remind => Remind(task),
            _ => Escalate(task, rule.EscalateToRole!),
        };
        if (eventType is not null)
        {
            context.Events.Add(new WorkflowEvent
            {
                InstanceId = task.InstanceId,
                TaskId = task.Id,
                At = now,
                Actor = Actor,
                Type = eventType,
                NodeCode = task.NodeCode,
                Target = target,
                Comment = rule.Code,
            });
        }

        context.AutomationLogs.Add(new AutomationLog
        {
            RuleCode = rule.Code,
            TaskId = task.Id,
            InstanceId = task.InstanceId,
            SubjectNumber = task.SubjectNumber,
            NodeCode = task.NodeCode,
            Action = rule.Action,
            Outcome = outcome,
            Detail = detail,
            At = now,
        });
        await context.SaveChangesAsync(cancellationToken);
        await work.CommitAsync(cancellationToken);
        return outcome == AutomationOutcome.Done;
    }

    private async Task<(AutomationOutcome, string?, string?, string?)> AssignAsync(WorkTask task, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (task.Assignee is not null)
        {
            return (AutomationOutcome.Skipped, "Already assigned", null, null);
        }

        if (await assignments.LeastLoadedAsync(task, cancellationToken) is not { } user)
        {
            return (AutomationOutcome.Failed, "No employee may take the task", null, null);
        }

        task.Assignee = user;
        task.AssignedAt = now;
        task.AssignedBy = Actor;
        TaskNotifications.Raise(context, clock, TaskNotifications.Assigned, task);
        return (AutomationOutcome.Done, $"Assigned to {user}", WorkflowEventTypes.Assigned, user);
    }

    private (AutomationOutcome, string?, string?, string?) Remind(WorkTask task)
    {
        TaskNotifications.Raise(context, clock, TaskNotifications.Reminder, task);
        return (AutomationOutcome.Done, task.Assignee is null ? $"Reminded role {task.CandidateRole}" : $"Reminded {task.Assignee}", WorkflowEventTypes.Reminded, task.Assignee);
    }

    private (AutomationOutcome, string?, string?, string?) Escalate(WorkTask task, string role)
    {
        task.EscalatedToRole = role;
        TaskNotifications.Raise(context, clock, TaskNotifications.Escalated, task);
        return (AutomationOutcome.Done, $"Escalated to {role}", WorkflowEventTypes.Escalated, role);
    }
}

/// <summary>One row of the automation log.</summary>
public sealed record AutomationLogEntry(Guid Id, string RuleCode, Guid TaskId, string SubjectNumber, string Step, AutomationAction Action, AutomationOutcome Outcome, string? Detail, DateTimeOffset At);

public sealed record AutomationLogPage(IReadOnlyList<AutomationLogEntry> Items, int Total);

/// <summary>The automation console: test a rule (saved or not) against the open tasks, and read what the rules did.</summary>
internal sealed class AutomationConsole(WorkflowDbContext context, AutomationRunner runner, JsonLogicEvaluator evaluator)
{
    public async Task<IReadOnlyList<AutomationMatch>> TestAsync(AutomationTestRequest request, CancellationToken cancellationToken)
    {
        if (request.When is null)
        {
            throw new WorkflowValidationException("when", "Say when the rule acts.");
        }

        try
        {
            evaluator.Evaluate(request.When, AutomationFacts.Sample);
        }
        catch (JsonLogicException ex)
        {
            throw new WorkflowValidationException("when", $"Invalid rule: {ex.Message}");
        }

        if (request.Action == AutomationAction.Escalate && string.IsNullOrWhiteSpace(request.EscalateToRole))
        {
            throw new WorkflowValidationException("escalateToRole", "Required.");
        }

        var rule = new AutomationRule
        {
            Code = string.IsNullOrWhiteSpace(request.Code) ? "TEST" : request.Code.Trim(),
            Workflows = request.Workflows ?? [],
            Steps = request.Steps ?? [],
            Condition = request.When.ToJsonString(),
            Action = request.Action,
            EscalateToRole = request.EscalateToRole,
            RepeatHours = request.RepeatHours,
        };
        return await runner.TestAsync(rule, cancellationToken);
    }

    public async Task<AutomationLogPage> LogAsync(string? rule, string? subject, int skip, int take, CancellationToken cancellationToken)
    {
        var query = context.AutomationLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(rule))
        {
            query = query.Where(l => l.RuleCode == rule);
        }

        if (!string.IsNullOrWhiteSpace(subject))
        {
            var term = subject.Trim();
            query = query.Where(l => l.SubjectNumber.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(l => l.At).Skip(Math.Max(skip, 0)).Take(Math.Clamp(take, 1, 200))
            .Select(l => new AutomationLogEntry(l.Id, l.RuleCode, l.TaskId, l.SubjectNumber, l.NodeCode, l.Action, l.Outcome, l.Detail, l.At))
            .ToListAsync(cancellationToken);
        return new AutomationLogPage(items, total);
    }
}

/// <summary>A rule to test: the same fields as the saved rule.</summary>
public sealed record AutomationTestRequest(string? Code, List<string>? Workflows, List<string>? Steps, JsonNode? When, AutomationAction Action, string? EscalateToRole, int? RepeatHours);
