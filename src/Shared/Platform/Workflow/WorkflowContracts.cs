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

    /// <summary>A bill was issued; processing continues once it is paid.</summary>
    PendingPayment,
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

/// <summary>
/// Every service has its own workflow (implemented by the Workflow module). A new service starts with a default one
/// that needs no approval or review; its versions are what requests of the service run on.
/// </summary>
public interface IServiceWorkflows
{
    /// <summary>The code of the service's workflow, or null when it has none.</summary>
    Task<string?> WorkflowOfAsync(string serviceCode, CancellationToken cancellationToken);

    /// <summary>Gives the service its default workflow (start → completed) unless it already has one; runs in the caller's unit of work.</summary>
    Task EnsureAsync(string serviceCode, string nameAr, string nameEn, CancellationToken cancellationToken);

    /// <summary>Whether a workflow of that code exists (owned by a service or not).</summary>
    Task<bool> ExistsAsync(string workflowCode, CancellationToken cancellationToken);

    /// <summary>
    /// Makes a named workflow the one the service runs, for versions that still name one (packages from before services
    /// owned their workflow): a service without a workflow takes over a workflow no other service owns; otherwise the named
    /// workflow's latest published version is added to the service's own workflow, unless that already runs the same
    /// definition. Runs in the caller's unit of work.
    /// </summary>
    Task AdoptAsync(string serviceCode, string nameAr, string nameEn, string workflowCode, CancellationToken cancellationToken);

    /// <summary>Puts the service's workflow and its versions at the service's level (entity and business app); runs in the caller's unit of work.</summary>
    Task ScopeAsync(string serviceCode, string? entityCode, string? businessApp, CancellationToken cancellationToken, IReadOnlyList<string>? sharedApps = null);
}

/// <summary>
/// The workflow cannot run as it is configured (e.g. a decision step whose rule set has no published version): the
/// request is not taken, and the message says what to fix.
/// </summary>
public sealed class WorkflowConfigurationException(string message, Exception? inner = null) : Exception(message, inner);
