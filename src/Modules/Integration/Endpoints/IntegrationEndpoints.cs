using System.Text.Json.Nodes;
using DynamicCube.Modules.Integration.Changes;
using DynamicCube.Modules.Integration.Domain;
using DynamicCube.Modules.Integration.Persistence;
using DynamicCube.Modules.Integration.Runtime;
using DynamicCube.Platform.Integration;
using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Integration.Endpoints;

public sealed record TestOperationRequest(Dictionary<string, string?>? Inputs);

public sealed record TestOperationResponse(
    IntegrationOutcome Outcome,
    string? Code,
    string? MessageAr,
    string? MessageEn,
    IReadOnlyDictionary<string, JsonNode?> Outputs,
    JsonNode? Data,
    Guid CallId,
    IntegrationTrace Trace);

public sealed record CallLogSummary(
    Guid Id,
    string OperationCode,
    string ConnectionCode,
    DateTimeOffset StartedAt,
    int DurationMs,
    IntegrationOutcome Outcome,
    int? HttpStatus,
    string? Code,
    bool FromCache,
    string UserId,
    string AppCode);

public sealed record CallLogPage(IReadOnlyList<CallLogSummary> Items, int Total);

internal static class IntegrationEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/operations/{code}/test", async (string code, TestOperationRequest request, IntegrationGateway gateway, CancellationToken ct) =>
        {
            var (result, trace) = await gateway.TestAsync(code, request.Inputs ?? [], ct);
            return TypedResults.Ok(new TestOperationResponse(result.Outcome, result.Code, result.MessageAr, result.MessageEn, result.Outputs, result.Data, result.CallId, trace));
        }).RequirePrivilege(IntegrationResources.TestConsole, CatalogActions.Execute).WithName("TestIntegrationOperation");

        group.MapGet("/call-logs", async (
            string? operation,
            IntegrationOutcome? outcome,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int? skip,
            int? take,
            IntegrationDbContext db,
            CancellationToken ct) =>
        {
            var query = db.CallLogs.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(operation))
            {
                query = query.Where(l => l.OperationCode == operation);
            }

            if (outcome is not null)
            {
                query = query.Where(l => l.Outcome == outcome);
            }

            if (from is not null)
            {
                query = query.Where(l => l.StartedAt >= from);
            }

            if (to is not null)
            {
                query = query.Where(l => l.StartedAt < to);
            }

            var total = await query.CountAsync(ct);
            var items = await query.OrderByDescending(l => l.StartedAt)
                .Skip(Math.Max(0, skip ?? 0))
                .Take(Math.Clamp(take ?? 50, 1, 200))
                .Select(l => new CallLogSummary(l.Id, l.OperationCode, l.ConnectionCode, l.StartedAt, l.DurationMs, l.Outcome, l.HttpStatus, l.Code, l.FromCache, l.UserId, l.AppCode))
                .ToListAsync(ct);
            return TypedResults.Ok(new CallLogPage(items, total));
        }).RequirePrivilege(IntegrationResources.CallLog, CatalogActions.View).WithName("ListIntegrationCalls");

        group.MapGet("/call-logs/{id:guid}", async Task<Results<Ok<CallLog>, NotFound>> (Guid id, IntegrationDbContext db, CancellationToken ct) =>
            await db.CallLogs.AsNoTracking().SingleOrDefaultAsync(l => l.Id == id, ct) is { } entry ? TypedResults.Ok(entry) : TypedResults.NotFound())
            .RequirePrivilege(IntegrationResources.CallLog, CatalogActions.View).WithName("GetIntegrationCall");
    }

    /// <summary>Maps gateway exceptions to problem responses.</summary>
    public static async ValueTask<object?> Errors(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (IntegrationInputException ex)
        {
            return Results.ValidationProblem(ex.Errors.ToDictionary(e => e.Key, e => e.Value), title: "The inputs are invalid.");
        }
        catch (IntegrationOperationNotFoundException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
    }
}
