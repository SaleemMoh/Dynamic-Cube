using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Integration.Changes;
using DynamicCube.Modules.Integration.Domain;
using DynamicCube.Modules.Integration.Persistence;
using DynamicCube.Modules.Integration.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.PublicApi;
using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Integration.Endpoints;

/// <summary>Where partners call the public API and get tokens, and whether sign-in clients are maintained from here.</summary>
public sealed record PublicApiInfo(string ApiUrl, string Audience, string? TokenUrl, bool Managed);

/// <summary>An endpoint as edited in the designer (not saved yet), the parameter values to try it with and whose limits apply.</summary>
/// <param name="Client">An API client whose companies and branches limit the try; none: no limits.</param>
public sealed record TryEndpointRequest(ApiEndpointPayload Endpoint, Dictionary<string, string?>? Inputs, string? Client, int? Limit, int? Offset);

/// <summary>What the endpoint answered, as a partner would get it, and the call as it would be made.</summary>
public sealed record TryEndpointResponse(int Status, JsonNode? Body, int? Rows, int DurationMs, string Url);

public sealed record ApiCallSummary(
    Guid Id,
    string ClientCode,
    string ClientId,
    string? EndpointCode,
    string Method,
    string Path,
    DateTimeOffset StartedAt,
    int DurationMs,
    int Status,
    int? Rows,
    string? Error,
    string? ClientIp,
    bool IsTry);

public sealed record ApiCallPage(IReadOnlyList<ApiCallSummary> Items, int Total);

/// <summary>
/// The public API (ADR-0042). On the external host: <c>GET /api/public/v1/{path}</c> for API clients' tokens only, and the
/// client's own OpenAPI document. On the internal host: the system functions, trying an unsaved endpoint, API clients'
/// sign-in and secrets, their OpenAPI documents, and the call log.
/// </summary>
internal static class PublicApiEndpoints
{
    public static void MapPublic(RouteGroupBuilder api)
    {
        api.MapGet("/openapi.json", async (HttpContext http, IntegrationDbContext db, PublicApiDocument document, ApiClientSignIns signIns, PublicApiSettings settings, CancellationToken ct) =>
        {
            var client = await ClientAsync(http, db, ct);
            if (client is not { IsActive: true })
            {
                return Answer(http, ApiAnswer.Problem(StatusCodes.Status403Forbidden, "This client may not call the API.", "The token's client is not an active API client."));
            }

            var endpoints = await EndpointsOfAsync(db, client, ct);
            var baseUrl = settings.BaseUrl ?? $"{http.Request.Scheme}://{http.Request.Host}";
            return Results.Json(await document.BuildAsync($"{client.NameEn} — public API", endpoints, baseUrl, await signIns.TokenUrlAsync(ct), ct));
        }).WithName("GetPublicApiDescription");

        api.MapGet("/{**path}", CallAsync).WithName("CallPublicEndpoint");
    }

    public static void MapAdmin(RouteGroupBuilder group)
    {
        var api = group.MapGroup("/public-api");
        api.MapGet("/info", async (ApiClientSignIns signIns, PublicApiSettings settings, IServiceClients clients, CancellationToken ct) =>
                TypedResults.Ok(new PublicApiInfo(signIns.ApiUrl, settings.Audience, await signIns.TokenUrlAsync(ct), clients.IsManaged)))
            .RequirePrivilege(IntegrationResources.Config, CatalogActions.View).WithName("GetPublicApiInfo");

        api.MapGet("/functions", (IEnumerable<IApiFunction> functions) => TypedResults.Ok(functions.Select(f => f.Info).OrderBy(i => i.Code, StringComparer.Ordinal).ToList()))
            .RequirePrivilege(IntegrationResources.Config, CatalogActions.View).WithName("ListPublicApiFunctions");

        // The endpoint designer's "Try it": an endpoint that is not saved yet, run as a partner would call it.
        api.MapPost("/endpoints/try", TryAsync).RequirePrivilege(IntegrationResources.TestConsole, CatalogActions.Execute).WithName("TryPublicEndpoint");

        api.MapGet("/clients/{code}/sign-in", (string code, ApiClientSignIns signIns, CancellationToken ct) => signIns.GetAsync(code, ct))
            .RequirePrivilege(IntegrationResources.Config, CatalogActions.View).WithName("GetApiClientSignIn");
        api.MapPost("/clients/{code}/sign-in", (string code, ApiClientSignIns signIns, CancellationToken ct) => signIns.SetUpAsync(code, ct))
            .RequirePrivilege(IntegrationResources.Config, CatalogActions.Edit).WithName("SetUpApiClientSignIn");
        api.MapPost("/clients/{code}/secret", (string code, ApiClientSignIns signIns, CancellationToken ct) => signIns.NewSecretAsync(code, ct))
            .RequirePrivilege(IntegrationResources.Config, CatalogActions.Edit).WithName("NewApiClientSecret");

        api.MapGet("/clients/{code}/openapi.json", async (string code, IntegrationDbContext db, PublicApiDocument document, ApiClientSignIns signIns, PublicApiSettings settings, CancellationToken ct) =>
        {
            var client = await db.ApiClients.AsNoTracking().SingleOrDefaultAsync(c => c.Code == code, ct)
                ?? throw new ApiClientNotFoundException($"There is no published API client {code}.");
            return Results.Json(await document.BuildAsync($"{client.NameEn} — public API", await EndpointsOfAsync(db, client, ct), settings.BaseUrl, await signIns.TokenUrlAsync(ct), ct));
        }).RequirePrivilege(IntegrationResources.Config, CatalogActions.View).WithName("GetApiClientDescription");

        api.MapGet("/calls", async (
            string? client,
            string? endpoint,
            int? status,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int? skip,
            int? take,
            IntegrationDbContext db,
            CancellationToken ct) =>
        {
            var query = db.ApiCalls.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(client))
            {
                query = query.Where(c => c.ClientCode == client);
            }

            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                query = query.Where(c => c.EndpointCode == endpoint);
            }

            // A status class (4 → 4xx) or an exact status.
            if (status is >= 1 and <= 5)
            {
                query = query.Where(c => c.Status >= status * 100 && c.Status < (status + 1) * 100);
            }
            else if (status is not null)
            {
                query = query.Where(c => c.Status == status);
            }

            if (from is not null)
            {
                query = query.Where(c => c.StartedAt >= from);
            }

            if (to is not null)
            {
                query = query.Where(c => c.StartedAt < to);
            }

            var total = await query.CountAsync(ct);
            var items = await query.OrderByDescending(c => c.StartedAt)
                .Skip(Math.Max(0, skip ?? 0))
                .Take(Math.Clamp(take ?? 50, 1, 200))
                .Select(c => new ApiCallSummary(c.Id, c.ClientCode, c.ClientId, c.EndpointCode, c.Method, c.Path, c.StartedAt, c.DurationMs, c.Status, c.Rows, c.Error, c.ClientIp, c.IsTry))
                .ToListAsync(ct);
            return TypedResults.Ok(new ApiCallPage(items, total));
        }).RequirePrivilege(IntegrationResources.ApiCalls, CatalogActions.View).WithName("ListPublicApiCalls");
    }

    /// <summary>Maps the admin endpoints' exceptions to problem responses.</summary>
    public static async ValueTask<object?> Errors(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (ApiClientNotFoundException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (ServiceClientException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "The identity provider refused.", detail: ex.Message);
        }
    }

    private static async Task<IResult> CallAsync(
        string? path,
        HttpContext http,
        IntegrationDbContext db,
        PublicApiRunner runner,
        PublicApiRateLimits limits,
        TimeProvider clock,
        CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        var call = new ApiCall
        {
            ClientId = http.User.FindFirst("azp")?.Value ?? string.Empty,
            StartedAt = clock.GetUtcNow(),
            Path = "/" + (path ?? string.Empty),
            CorrelationId = Activity.Current?.TraceId.ToString() ?? http.TraceIdentifier,
            ClientIp = http.Connection.RemoteIpAddress?.ToString(),
        };
        if (call.Path.Length > 300)
        {
            call.Path = call.Path[..300];
        }

        var answer = await AnswerAsync(http, db, runner, limits, call, ct);
        call.Status = answer.Status;
        call.Rows = answer.Rows;
        call.Error = answer.IsProblem ? Cut(answer.Error, 4000) : null;
        call.DurationMs = (int)watch.ElapsedMilliseconds;
        db.ApiCalls.Add(call);
        await db.SaveChangesAsync(CancellationToken.None);
        return Answer(http, answer);
    }

    private static async Task<ApiAnswer> AnswerAsync(HttpContext http, IntegrationDbContext db, PublicApiRunner runner, PublicApiRateLimits limits, ApiCall call, CancellationToken ct)
    {
        var client = await ClientAsync(http, db, ct);
        if (client is null)
        {
            return ApiAnswer.Problem(StatusCodes.Status403Forbidden, "This client may not call the API.", $"Sign-in client '{call.ClientId}' is not a registered API client.");
        }

        call.ClientCode = client.Code;
        if (!client.IsActive)
        {
            return ApiAnswer.Problem(StatusCodes.Status403Forbidden, "This client may not call the API.", $"API client {client.Code} is not active.");
        }

        var endpoints = await db.ApiEndpoints.AsNoTracking().Where(e => e.IsActive).ToListAsync(ct);
        if (PublicApiPaths.Match(endpoints, call.Path) is not var (endpoint, pathValues))
        {
            return ApiAnswer.Problem(StatusCodes.Status404NotFound, "No such endpoint.", $"Nothing answers {call.Path}.");
        }

        call.EndpointCode = endpoint.Code;
        call.Path = PublicApiPaths.Logged(endpoint, pathValues.ToDictionary(p => p.Key, p => (string?)p.Value, StringComparer.Ordinal), null, null);
        if (!client.Endpoints.Contains(endpoint.Code, StringComparer.Ordinal))
        {
            return ApiAnswer.Problem(StatusCodes.Status403Forbidden, "This client may not call this endpoint.", $"Endpoint {endpoint.Code} is not given to API client {client.Code}.");
        }

        if (!limits.TryAcquire(client.Code, client.RateLimitPerMinute, out var wait))
        {
            http.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            return ApiAnswer.Problem(StatusCodes.Status429TooManyRequests, "Too many calls.", $"At most {client.RateLimitPerMinute} calls a minute.");
        }

        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in http.Request.Query)
        {
            query.TryAdd(key, value.ToString());
        }

        var (answer, values, limit, offset) = await runner.RunAsync(endpoint, pathValues, query, Caller(client), false, ct);
        call.Path = PublicApiPaths.Logged(endpoint, values, limit, offset);
        return answer;
    }

    private static async Task<IResult> TryAsync(
        TryEndpointRequest request,
        HttpContext http,
        IntegrationDbContext db,
        IDataQueries queries,
        IEnumerable<IApiFunction> functions,
        PublicApiRunner runner,
        ICurrentUser user,
        TimeProvider clock,
        CancellationToken ct)
    {
        var payload = request.Endpoint ?? new ApiEndpointPayload();
        var problems = await ApiEndpointApplier.CheckAsync(payload, queries, functions, ct);
        if (problems.Count > 0)
        {
            return Results.ValidationProblem(problems.ToDictionary(p => p.Key, p => p.Value.ToArray()), title: "The endpoint is invalid.");
        }

        ApiClient? client = null;
        if (!string.IsNullOrWhiteSpace(request.Client))
        {
            client = await db.ApiClients.AsNoTracking().SingleOrDefaultAsync(c => c.Code == request.Client, ct)
                ?? throw new ApiClientNotFoundException($"There is no published API client {request.Client}.");
        }

        var endpoint = new ApiEndpoint { Code = payload.Code ?? "TRY", NameAr = payload.NameAr, NameEn = payload.NameEn };
        ApiEndpointApplier.Fill(endpoint, payload);
        var inputs = request.Inputs ?? [];
        var pathValues = endpoint.Parameters.Where(p => p.Location == ApiParameterLocation.Path && inputs.GetValueOrDefault(p.Name) is not null)
            .ToDictionary(p => p.Name, p => inputs[p.Name]!, StringComparer.Ordinal);
        var query = endpoint.Parameters.Where(p => p.Location == ApiParameterLocation.Query)
            .ToDictionary(p => p.Name, p => inputs.GetValueOrDefault(p.Name), StringComparer.Ordinal);
        query["limit"] = request.Limit?.ToString(CultureInfo.InvariantCulture);
        query["offset"] = request.Offset?.ToString(CultureInfo.InvariantCulture);

        var watch = Stopwatch.StartNew();
        var (answer, values, limit, offset) = await runner.RunAsync(endpoint, pathValues, query, client is null ? ApiCaller.Unlimited : Caller(client), true, ct);
        var duration = (int)watch.ElapsedMilliseconds;
        var path = PublicApiPaths.Logged(endpoint, values, limit, offset);
        db.ApiCalls.Add(new ApiCall
        {
            ClientId = "try:" + (user.UserName ?? user.UserId),
            ClientCode = client?.Code ?? string.Empty,
            EndpointCode = payload.Code,
            Path = path,
            StartedAt = clock.GetUtcNow(),
            DurationMs = duration,
            Status = answer.Status,
            Rows = answer.Rows,
            Error = answer.IsProblem ? Cut(answer.Error, 4000) : null,
            CorrelationId = Activity.Current?.TraceId.ToString() ?? http.TraceIdentifier,
            IsTry = true,
        });
        await db.SaveChangesAsync(CancellationToken.None);
        return TypedResults.Ok(new TryEndpointResponse(answer.Status, answer.Body, answer.Rows, duration, PublicApiSettings.Path + path));
    }

    private static Task<ApiClient?> ClientAsync(HttpContext http, IntegrationDbContext db, CancellationToken ct)
    {
        var clientId = http.User.FindFirst("azp")?.Value;
        return string.IsNullOrEmpty(clientId)
            ? Task.FromResult<ApiClient?>(null)
            : db.ApiClients.AsNoTracking().SingleOrDefaultAsync(c => c.ClientId == clientId, ct);
    }

    private static async Task<List<ApiEndpoint>> EndpointsOfAsync(IntegrationDbContext db, ApiClient client, CancellationToken ct) =>
        (await db.ApiEndpoints.AsNoTracking().Where(e => e.IsActive).ToListAsync(ct)).Where(e => client.Endpoints.Contains(e.Code, StringComparer.Ordinal)).ToList();

    /// <summary>The caller an API client is: its companies and branches (<c>*</c>: all of them).</summary>
    internal static ApiCaller Caller(ApiClient client) =>
        new(client.Code, client.Companies.Contains("*") ? null : client.Companies, client.Branches.Contains("*") ? null : client.Branches);

    private static IResult Answer(HttpContext http, ApiAnswer answer) =>
        Results.Json(answer.Body, statusCode: answer.Status, contentType: answer.IsProblem ? "application/problem+json" : "application/json");

    private static string? Cut(string? text, int max) => text is null || text.Length <= max ? text : text[..max];
}
