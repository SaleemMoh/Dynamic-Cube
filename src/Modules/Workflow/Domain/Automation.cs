namespace DynamicCube.Modules.Workflow.Domain;

public enum AutomationAction
{
    /// <summary>An unassigned task goes to the candidate with the fewest open tasks.</summary>
    AssignLeastLoaded,

    /// <summary>A reminder is raised for the task (staff notification rules send it).</summary>
    Remind,

    /// <summary>The task is escalated to a role (it shows in their escalations) and a notification is raised.</summary>
    Escalate,
}

/// <summary>
/// A rule run by the worker on open tasks: when its condition holds on a task (age, hours unassigned, hours to its due
/// time, its SLA state…), the action is taken once (or again every <see cref="RepeatHours"/>) and logged.
/// </summary>
public sealed class AutomationRule : ConfigEntity
{
    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    /// <summary>Workflows it applies to (empty: all).</summary>
    public List<string> Workflows { get; set; } = [];

    /// <summary>Task steps it applies to (empty: all).</summary>
    public List<string> Steps { get; set; } = [];

    /// <summary>JSONLogic over <c>task</c> (see <c>AutomationFacts</c>), as JSON.</summary>
    public string Condition { get; set; } = "true";

    public AutomationAction Action { get; set; }

    /// <summary>Escalate: the role the task is escalated to.</summary>
    public string? EscalateToRole { get; set; }

    /// <summary>Take the action again after this many hours while the condition still holds (once when empty).</summary>
    public int? RepeatHours { get; set; }

    public int DisplayOrder { get; set; }
}

public enum AutomationOutcome
{
    Done,
    Skipped,
    Failed,
}

/// <summary>One action an automation rule took (or tried) on a task.</summary>
public sealed class AutomationLog
{
    public Guid Id { get; set; } = OrderedIds.Next();

    public string RuleCode { get; set; } = string.Empty;

    public Guid TaskId { get; set; }

    public Guid InstanceId { get; set; }

    public string SubjectNumber { get; set; } = string.Empty;

    public string NodeCode { get; set; } = string.Empty;

    public AutomationAction Action { get; set; }

    public AutomationOutcome Outcome { get; set; }

    public string? Detail { get; set; }

    public DateTimeOffset At { get; set; }
}
