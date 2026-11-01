using DynamicCube.Modules.Services.Domain;
using DynamicCube.Platform.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Services.Runtime;

/// <summary>Requests made by a workflow step (3f): when one finishes, the parent's workflow is told, in the same unit of work.</summary>
internal static class RequestLinks
{
    private static readonly StatusCategory[] Finished = [StatusCategory.Approved, StatusCategory.Rejected, StatusCategory.Closed, StatusCategory.Cancelled];

    public static async Task TellParentAsync(IServiceProvider services, ServiceRequest request, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (request is not { LinkKind: RequestLinkKind.Spawned, ParentRequestId: { } parent } || !Finished.Contains(request.StatusCategory))
        {
            return;
        }

        var closed = new ChildRequestClosed(
            RequestSubject.Type,
            parent,
            request.Id,
            request.Number,
            request.StatusCategory == StatusCategory.Approved,
            request.StatusCategory == StatusCategory.Cancelled && request.DisplayStatus == "EXPIRED" ? "EXPIRED" : System.Text.Json.JsonNamingPolicy.SnakeCaseUpper.ConvertName(request.StatusCategory.ToString()),
            clock.GetUtcNow());
        foreach (var listener in services.GetServices<IChildRequestListener>())
        {
            await listener.ChildClosedAsync(closed, cancellationToken);
        }
    }
}

/// <summary>Requests a workflow step asks for (3f), made by the applicant's side of services.</summary>
internal sealed class RequestSpawner(RequestService requests) : IRequestSpawner
{
    public Task<bool> ServiceExistsAsync(string serviceCode, CancellationToken cancellationToken) => requests.ServiceExistsAsync(serviceCode, cancellationToken);

    public async Task<SpawnedRequest> SpawnAsync(SpawnRequest request, CancellationToken cancellationToken)
    {
        var made = await requests.SpawnAsync(request, cancellationToken);
        return new SpawnedRequest(made.Id, made.Number, System.Text.Json.JsonNamingPolicy.SnakeCaseUpper.ConvertName(made.StatusCategory.ToString()));
    }
}
