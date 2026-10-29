using DynamicCube.Platform.Persistence;

namespace DynamicCube.Modules.Workflow.Domain;

public enum InstanceStatus
{
    Running,

    /// <summary>A branch waits for the applicant's correction.</summary>
    WaitingCustomer,
    Completed,
    Cancelled,
}

public enum SlaState
{
    OnTrack,
    Warning,
    Breached,
}

/// <summary>One run of a workflow version for one subject (a service request).</summary>
public sealed class WorkflowInstance : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string WorkflowCode { get; set; } = string.Empty;

    public Guid VersionId { get; set; }

    public int VersionNumber { get; set; }

    public string SubjectType { get; set; } = string.Empty;

    public Guid SubjectId { get; set; }

    public string SubjectNumber { get; set; } = string.Empty;

    public string TitleAr { get; set; } = string.Empty;

    public string TitleEn { get; set; } = string.Empty;

    public string PartyNameAr { get; set; } = string.Empty;

    public string PartyNameEn { get; set; } = string.Empty;

    public string? ServiceCode { get; set; }

    public string? LevelCode { get; set; }

    public InstanceStatus Status { get; set; }

    public string? Outcome { get; set; }

    /// <summary>What automatic steps produced (JSON): <c>decisions</c> and <c>documents</c> by node, read by later rules.</summary>
    public string Facts { get; set; } = "{}";

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public int? SlaWorkingDays { get; set; }

    public DateTimeOffset? DueAt { get; set; }

    public SlaState Sla { get; set; }

    /// <summary>Since when the instance waits for the applicant (the SLA clock is paused meanwhile).</summary>
    public DateTimeOffset? PausedAt { get; set; }
}

public enum TokenStatus
{
    /// <summary>At a node (a human task, mostly).</summary>
    Active,

    /// <summary>Returned to the applicant; resumes at <see cref="WorkflowToken.ResumeAt"/>.</summary>
    WaitingCustomer,

    /// <summary>Arrived at a join that is not yet satisfied.</summary>
    AtJoin,
    Done,
    Cancelled,

    /// <summary>At a payment step: waits for its bill to be paid (or to expire).</summary>
    WaitingPayment,

    /// <summary>At a visit step: waits for the visit's result (the deadline keeps running: the visit is the entity's work).</summary>
    WaitingVisit,
}

/// <summary>An open split a token belongs to: its id, the split node and how many branches it started.</summary>
public sealed record ForkFrame(Guid Id, string Split, int Branches);

/// <summary>A point of execution; parallel branches have one token each.</summary>
public sealed class WorkflowToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid InstanceId { get; set; }

    public string NodeCode { get; set; } = string.Empty;

    public TokenStatus Status { get; set; }

    /// <summary>Open splits, innermost last.</summary>
    public List<ForkFrame> Forks { get; set; } = [];

    public string? ResumeAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public enum WorkTaskStatus
{
    Open,
    Completed,
    Cancelled,
}

/// <summary>
/// A human task: who may take it (candidate role, unit, position or user), who has it, its deadline and how it ended.
/// Carries the subject's inbox details so inboxes need no other module.
/// </summary>
public sealed class WorkTask : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid InstanceId { get; set; }

    public Guid TokenId { get; set; }

    public string NodeCode { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public WorkTaskStatus Status { get; set; }

    public string? CandidateRole { get; set; }

    /// <summary>The unit the task was routed to (shown in inboxes).</summary>
    public string? CandidateOrgUnit { get; set; }

    /// <summary>Units whose staff may take the task: <see cref="CandidateOrgUnit"/>, and the units below it when included.</summary>
    public List<string> CandidateOrgUnits { get; set; } = [];

    public string? CandidatePosition { get; set; }

    public string? CandidateUser { get; set; }

    public Definitions.AssignmentMode Mode { get; set; }

    public string? DistributorRole { get; set; }

    /// <summary>Usernames that may not take the task (they acted on its maker steps).</summary>
    public List<string> ExcludedUsers { get; set; } = [];

    public string? Assignee { get; set; }

    public DateTimeOffset? AssignedAt { get; set; }

    public string? AssignedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? DueAt { get; set; }

    public DateTimeOffset? WarnAt { get; set; }

    public SlaState Sla { get; set; }

    public string? EscalatedToRole { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string? CompletedBy { get; set; }

    public string? ActionCode { get; set; }

    public string? Comment { get; set; }

    public Guid SubjectId { get; set; }

    public string SubjectType { get; set; } = string.Empty;

    public string SubjectNumber { get; set; } = string.Empty;

    public string TitleAr { get; set; } = string.Empty;

    public string TitleEn { get; set; } = string.Empty;

    public string PartyNameAr { get; set; } = string.Empty;

    public string PartyNameEn { get; set; } = string.Empty;

    public string? ServiceCode { get; set; }

    public string? LevelCode { get; set; }
}

/// <summary>The instance's internal timeline: tasks, assignments, actions, notes, SLA alerts.</summary>
public sealed class WorkflowEvent
{
    /// <summary>Increases in the order events are recorded (events of one step share their time).</summary>
    public Guid Id { get; set; } = OrderedIds.Next();

    public Guid InstanceId { get; set; }

    public Guid? TaskId { get; set; }

    public DateTimeOffset At { get; set; }

    /// <summary>Username, or <c>system</c>.</summary>
    public string Actor { get; set; } = string.Empty;

    /// <summary>The user the actor stood in for (delegation), if any.</summary>
    public string? OnBehalfOf { get; set; }

    public string Type { get; set; } = string.Empty;

    public string? NodeCode { get; set; }

    public string? ActionCode { get; set; }

    /// <summary>Assignee, for assignment events.</summary>
    public string? Target { get; set; }

    public string? Comment { get; set; }

    public bool VisibleToApplicant { get; set; }
}

public static class WorkflowEventTypes
{
    public const string Started = "STARTED";
    public const string TaskCreated = "TASK_CREATED";
    public const string Assigned = "ASSIGNED";
    public const string Claimed = "CLAIMED";
    public const string Released = "RELEASED";
    public const string Action = "ACTION";
    public const string Returned = "RETURNED";
    public const string Resubmitted = "RESUBMITTED";
    public const string TaskCancelled = "TASK_CANCELLED";
    public const string Note = "NOTE";
    public const string SlaWarning = "SLA_WARNING";
    public const string SlaBreached = "SLA_BREACHED";
    public const string Completed = "COMPLETED";
    public const string Decided = "DECIDED";
    public const string DocumentIssued = "DOCUMENT_ISSUED";
    public const string DocumentAmended = "DOCUMENT_AMENDED";
    public const string BillIssued = "BILL_ISSUED";
    public const string PaymentReceived = "PAYMENT_RECEIVED";
    public const string PaymentNotReceived = "PAYMENT_NOT_RECEIVED";
    public const string VisitRequested = "VISIT_REQUESTED";
    public const string VisitCompleted = "VISIT_COMPLETED";
    public const string VisitCancelled = "VISIT_CANCELLED";
}

/// <summary>Out of office: the delegate works on the delegator's assigned tasks between the dates.</summary>
public sealed class Delegation : IAuditable, IHasAuditColumns
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Delegator { get; set; } = string.Empty;

    public string Delegate { get; set; } = string.Empty;

    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public string? Reason { get; set; }

    public bool IsCancelled { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}

/// <summary>Version 7 GUIDs that increase within the process, even when created in the same millisecond.</summary>
internal static class OrderedIds
{
    private static readonly Lock Gate = new();
    private static long _last;

    public static Guid Next()
    {
        lock (Gate)
        {
            _last = Math.Max(_last + 1, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            return Guid.CreateVersion7(DateTimeOffset.FromUnixTimeMilliseconds(_last));
        }
    }
}
