using System.Text.Json.Nodes;

namespace DynamicCube.Modules.Workflow.Definitions;

/// <summary>
/// A workflow version's graph: nodes connected by their <c>next</c>, branch, action and route targets. Authored in a
/// change set; immutable once published, and running instances stay on the version they started on.
/// </summary>
public sealed class WorkflowDefinition
{
    /// <summary>Overall target in working days when the subject brings none (the service level's target wins).</summary>
    public int? SlaWorkingDays { get; set; }

    public List<NodeDefinition> Nodes { get; set; } = [];
}

public enum NodeType
{
    Start,
    HumanTask,

    /// <summary>Starts every branch at once.</summary>
    ParallelSplit,

    /// <summary>Waits for the branches of the nearest open split (all, any or a count).</summary>
    Join,

    /// <summary>Routes by the first rule that holds, else the default.</summary>
    Gateway,

    /// <summary>Evaluates a decision rule set (e.g. grading); the results are kept for later rules and documents.</summary>
    Decision,

    /// <summary>Issues (or amends) a document for the holder, e.g. the certificate.</summary>
    Document,
    End,
}

public sealed class NodeDefinition
{
    public string Code { get; set; } = string.Empty;

    public NodeType Type { get; set; }

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    /// <summary>Start and join: the node that follows.</summary>
    public string? Next { get; set; }

    /// <summary>Human task: who works on it.</summary>
    public TaskAssignment? Assignment { get; set; }

    /// <summary>Human task: what the assignee can do.</summary>
    public List<TaskAction> Actions { get; set; } = [];

    /// <summary>Human task: status shown to the applicant while it is open (default <c>UNDER_REVIEW</c>).</summary>
    public string? DisplayStatus { get; set; }

    public TaskSla? Sla { get; set; }

    /// <summary>Human task (maker-checker): whoever completed one of these steps in this instance may not take this one.</summary>
    public List<string> ExcludeActorsOf { get; set; } = [];

    /// <summary>Parallel split: the first node of each branch.</summary>
    public List<string> Branches { get; set; } = [];

    public JoinMode Mode { get; set; }

    /// <summary>Join with <see cref="JoinMode.Count"/>: how many branches must arrive.</summary>
    public int? Count { get; set; }

    public List<GatewayRoute> Routes { get; set; } = [];

    /// <summary>Gateway: where to go when no route holds.</summary>
    public string? Default { get; set; }

    /// <summary>End: the result for the applicant.</summary>
    public EndOutcome? Outcome { get; set; }

    /// <summary>Decision: the rule set to evaluate (its current published version).</summary>
    public string? RuleSet { get; set; }

    /// <summary>Decision: input field → JSONLogic over the workflow facts; empty passes all the facts.</summary>
    public Dictionary<string, JsonNode?> Input { get; set; } = [];

    /// <summary>Decision: where the results go, as <c>decisions.&lt;Result&gt;</c> (default: the node code).</summary>
    public string? Result { get; set; }

    /// <summary>Document: the document type to issue.</summary>
    public string? DocumentType { get; set; }

    /// <summary>Document: JSONLogic giving the holder's party id (default: the subject's party).</summary>
    public JsonNode? Holder { get; set; }

    /// <summary>Document: data field → JSONLogic over the workflow facts.</summary>
    public Dictionary<string, JsonNode?> Data { get; set; } = [];

    /// <summary>Document: the line items (e.g. one per sector with its grade).</summary>
    public DocumentLines? Lines { get; set; }

    /// <summary>Document: why it is issued (kept in the document's history).</summary>
    public string? Reason { get; set; }
}

/// <summary>Line items built from a list: one line per element, each field a JSONLogic rule.</summary>
public sealed class DocumentLines
{
    /// <summary>JSONLogic giving the list, e.g. <c>{"var": "decisions.GRADING.items"}</c>.</summary>
    public JsonNode? Source { get; set; }

    /// <summary>Name the element is read by in the field rules (default <c>item</c>).</summary>
    public string? As { get; set; }

    public Dictionary<string, JsonNode?> Fields { get; set; } = [];
}

public enum TargetKind
{
    /// <summary>Employees holding a role (optionally within an org unit and/or position).</summary>
    Role,

    /// <summary>One named employee.</summary>
    User,

    /// <summary>Whoever completed <see cref="TaskAssignment.Step"/> in this instance (falls back to the role, if any).</summary>
    PreviousActor,
}

public enum AssignmentMode
{
    /// <summary>Candidates claim the task from a shared queue.</summary>
    Queue,

    /// <summary>A distributor (e.g. the request manager) assigns it to a candidate.</summary>
    Distribute,

    /// <summary>Assigned at once to the candidate with the fewest open tasks.</summary>
    Auto,
}

public sealed class TaskAssignment
{
    public TargetKind Target { get; set; }

    public string? Role { get; set; }

    public string? OrgUnit { get; set; }

    public string? Position { get; set; }

    public string? User { get; set; }

    public string? Step { get; set; }

    public AssignmentMode Mode { get; set; }

    /// <summary>Distribute mode: the role whose holders assign the task.</summary>
    public string? DistributorRole { get; set; }
}

public enum ActionKind
{
    /// <summary>Moves on (e.g. "send to auditor").</summary>
    Complete,
    Approve,

    /// <summary>Needs a reason, which the applicant sees.</summary>
    Reject,

    /// <summary>Back to the applicant for correction (needs a reason the applicant sees); resumes after resubmission.</summary>
    Return,
}

public sealed class TaskAction
{
    public string Code { get; set; } = string.Empty;

    public ActionKind Kind { get; set; }

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    /// <summary>Next node (not used by returns).</summary>
    public string? To { get; set; }

    public bool CommentRequired { get; set; }

    /// <summary>The comment is shown to the applicant (always for rejections and returns).</summary>
    public bool CommentToApplicant { get; set; }

    /// <summary>Status shown to the applicant after the action (returns default to <c>RETURNED</c>).</summary>
    public string? DisplayStatus { get; set; }

    /// <summary>Return: where processing resumes after resubmission (default: this task, for the same employee).</summary>
    public string? ResumeAt { get; set; }

    public bool RequiresComment => CommentRequired || Kind is ActionKind.Reject or ActionKind.Return;

    public bool IsVisibleToApplicant => CommentToApplicant || Kind is ActionKind.Reject or ActionKind.Return;
}

public sealed class TaskSla
{
    public int? WorkingDays { get; set; }

    /// <summary>Working days per service level, overriding <see cref="WorkingDays"/>.</summary>
    public Dictionary<string, int> ByLevel { get; set; } = [];

    /// <summary>When the task turns amber, as a percentage of its time (default 75).</summary>
    public int WarnPercent { get; set; } = 75;

    /// <summary>Holders of this role see the task in their escalations once it is overdue.</summary>
    public string? EscalateToRole { get; set; }
}

public sealed class GatewayRoute
{
    /// <summary>JSONLogic over <c>data</c>, <c>level</c> and <c>actions</c> (node code → last action code).</summary>
    public JsonNode? When { get; set; }

    public string To { get; set; } = string.Empty;
}

public enum JoinMode
{
    All,
    Any,
    Count,
}

public enum EndOutcome
{
    Approved,
    Rejected,
    Closed,
}
