using System.Text.Json.Nodes;

namespace DynamicCube.Platform.Workflow;

/// <summary>What a workflow is processing (a service request) and how its inbox rows describe it.</summary>
/// <param name="SubjectType">Which <see cref="IWorkflowSubject"/> owns the subject, e.g. <c>SERVICE_REQUEST</c>.</param>
/// <param name="SlaWorkingDays">Overall target from the subject (e.g. the service level); the workflow's own target otherwise.</param>
public sealed record WorkflowStart(
    string WorkflowCode,
    string SubjectType,
    Guid SubjectId,
    string SubjectNumber,
    string TitleAr,
    string TitleEn,
    string PartyNameAr,
    string PartyNameEn,
    string? ServiceCode,
    string? LevelCode,
    int? SlaWorkingDays);

/// <summary>Starts and resumes workflows (implemented by the Workflow module). Callers run it inside their unit of work.</summary>
public interface IWorkflowRuntime
{
    /// <summary>Whether a workflow has a published version that new subjects can start on.</summary>
    Task<bool> CanStartAsync(string workflowCode, CancellationToken cancellationToken);

    Task StartAsync(WorkflowStart start, CancellationToken cancellationToken);

    /// <summary>The applicant answered a return for correction: the waiting branches continue.</summary>
    Task ResumeAsync(string subjectType, Guid subjectId, CancellationToken cancellationToken);
}

/// <summary>Where a subject stands for the applicant, as set by the workflow.</summary>
public enum SubjectStatus
{
    InProgress,
    PendingCustomer,
    Approved,
    Rejected,
    Closed,
}

/// <param name="EventType">Timeline entry for the applicant, e.g. <c>RETURNED</c>, <c>APPROVED</c>.</param>
/// <param name="Comment">A reason the applicant may read (returns and rejections), else null.</param>
public sealed record SubjectUpdate(SubjectStatus Status, string DisplayStatus, string EventType, string? Comment);

/// <summary>A kind of subject workflows run on (implemented by the module that owns it, e.g. Services for requests).</summary>
public interface IWorkflowSubject
{
    string SubjectType { get; }

    /// <summary>Facts gateway rules read, e.g. <c>{"data": {...answers}, "level": "PREMIUM"}</c>.</summary>
    Task<JsonObject> GetRuleContextAsync(Guid subjectId, CancellationToken cancellationToken);

    Task UpdateAsync(Guid subjectId, SubjectUpdate update, CancellationToken cancellationToken);
}
