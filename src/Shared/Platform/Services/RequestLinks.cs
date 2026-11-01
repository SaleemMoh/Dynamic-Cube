using System.Text.Json.Nodes;

namespace DynamicCube.Platform.Services;

/// <summary>
/// A request a workflow step asks for (3f): of <paramref name="ServiceCode"/>, for the same applicant as the parent
/// request, with the parent's answers its form shares and <paramref name="Data"/> over them. With
/// <paramref name="Submit"/> it is submitted at once when its answers are complete (its own workflow starts); otherwise,
/// or when something is missing, it waits as a draft for the applicant to complete and submit.
/// </summary>
/// <param name="RequestId">The new request's id, chosen by the caller so it can be awaited before it exists.</param>
/// <param name="Step">The parent workflow's step that asks for it.</param>
public sealed record SpawnRequest(Guid RequestId, string ParentType, Guid ParentId, string ServiceCode, JsonObject Data, bool Submit, string Step);

/// <summary>The request made: its number (once submitted) and status (<c>DRAFT</c>, <c>SUBMITTED</c>…).</summary>
public sealed record SpawnedRequest(Guid Id, string? Number, string Status);

/// <summary>Makes requests on behalf of a workflow step (implemented by Services).</summary>
public interface IRequestSpawner
{
    /// <summary>Whether a service exists that steps may make requests of (active).</summary>
    Task<bool> ServiceExistsAsync(string serviceCode, CancellationToken cancellationToken);

    /// <summary>Makes the request, in the caller's unit of work; throws <see cref="InvalidOperationException"/> when it cannot.</summary>
    Task<SpawnedRequest> SpawnAsync(SpawnRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// A request a step made has finished: approved, or rejected, closed, cancelled or expired (<paramref name="Status"/> says
/// which).
/// </summary>
public sealed record ChildRequestClosed(string ParentType, Guid ParentId, Guid RequestId, string? Number, bool Approved, string Status, DateTimeOffset At);

/// <summary>Told when a request made by a step finishes (implemented by Workflow, which moves the waiting step on).</summary>
public interface IChildRequestListener
{
    Task ChildClosedAsync(ChildRequestClosed closed, CancellationToken cancellationToken);
}
