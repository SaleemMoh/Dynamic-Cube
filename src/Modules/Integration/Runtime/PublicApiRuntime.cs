using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.RateLimiting;
using DynamicCube.Modules.Integration.Domain;
using DynamicCube.Platform.PublicApi;
using Microsoft.Extensions.Logging;

namespace DynamicCube.Modules.Integration.Runtime;

/// <summary>
/// The public API's settings (ADR-0042): the audience its tokens carry (<c>PublicApi:Audience</c>), the external host's
/// address partners call (<c>PublicApi:BaseUrl</c>) and where they get tokens (<c>PublicApi:TokenUrl</c>; else the
/// identity provider is asked), used in OpenAPI documents and on the API client screens.
/// </summary>
public sealed record PublicApiSettings(string Audience, string? BaseUrl, string? TokenUrl = null)
{
    public const string Path = "/api/public/v1";
}

/// <summary>What a public endpoint answered: the HTTP status and body, the rows returned and, for the log, what went wrong.</summary>
public sealed record ApiAnswer(int Status, JsonNode? Body, int? Rows = null, string? Error = null)
{
    public bool IsProblem => Status >= 400;

    public static ApiAnswer Problem(int status, string title, string? detail = null, IReadOnlyDictionary<string, string[]>? errors = null, string? error = null)
    {
        var body = new JsonObject { ["title"] = title, ["status"] = status };
        if (detail is not null)
        {
            body["detail"] = detail;
        }

        if (errors is { Count: > 0 })
        {
            body["errors"] = new JsonObject(errors.Select(e => KeyValuePair.Create(e.Key, (JsonNode?)new JsonArray([.. e.Value.Select(m => (JsonNode?)JsonValue.Create(m))]))));
        }

        return new ApiAnswer(status, body, null, error ?? detail ?? title);
    }
}

/// <summary>Finds the endpoint a path calls and reads its placeholders (literal parts compare without case).</summary>
public static class PublicApiPaths
{
    public static (ApiEndpoint Endpoint, Dictionary<string, string> Values)? Match(IEnumerable<ApiEndpoint> endpoints, string path)
    {
        var parts = Split(path);
        foreach (var endpoint in endpoints)
        {
            var template = Split(endpoint.Path);
            if (template.Length != parts.Length)
            {
                continue;
            }

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            var matched = true;
            for (var i = 0; i < template.Length && matched; i++)
            {
                if (template[i].StartsWith('{') && template[i].EndsWith('}'))
                {
                    values[template[i][1..^1]] = Uri.UnescapeDataString(parts[i]);
                }
                else
                {
                    matched = string.Equals(template[i], parts[i], StringComparison.OrdinalIgnoreCase);
                }
            }

            if (matched)
            {
                return (endpoint, values);
            }
        }

        return null;
    }

    /// <summary>The path and query as logged: masked parameters written as <c>***</c>.</summary>
    public static string Logged(ApiEndpoint endpoint, IReadOnlyDictionary<string, string?> values, int? limit, int? offset)
    {
        var path = new StringBuilder(endpoint.Path);
        foreach (var parameter in endpoint.Parameters.Where(p => p.Location == ApiParameterLocation.Path))
        {
            var value = values.GetValueOrDefault(parameter.Name) ?? string.Empty;
            path.Replace($"{{{parameter.Name}}}", parameter.IsMasked ? "***" : Uri.EscapeDataString(value));
        }

        var query = endpoint.Parameters
            .Where(p => p.Location == ApiParameterLocation.Query && values.GetValueOrDefault(p.Name) is not null)
            .Select(p => $"{p.Name}={(p.IsMasked ? "***" : Uri.EscapeDataString(values[p.Name]!))}")
            .Concat(limit is null ? [] : [$"limit={limit.Value.ToString(CultureInfo.InvariantCulture)}"])
            .Concat(offset is null or 0 ? [] : [$"offset={offset.Value.ToString(CultureInfo.InvariantCulture)}"])
            .ToList();
        var logged = query.Count == 0 ? path.ToString() : $"{path}?{string.Join('&', query)}";
        return logged.Length > 2000 ? logged[..2000] : logged;
    }

    private static string[] Split(string path) => path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
}

/// <summary>
/// Runs a public endpoint for a caller: checks the parameters, then reads a page of rows (or one row) of its query, or
/// calls its system function. Problems come back as problem details; the endpoint's internals are shown only when
/// <c>forAdministrator</c> (the designer's try).
/// </summary>
public sealed partial class PublicApiRunner(IDataQueries queries, IEnumerable<IApiFunction> functions, ILogger<PublicApiRunner> logger)
{
    public const int MaxOffset = 1_000_000;

    public async Task<(ApiAnswer Answer, IReadOnlyDictionary<string, string?> Values, int? Limit, int? Offset)> RunAsync(
        ApiEndpoint endpoint,
        IReadOnlyDictionary<string, string> pathValues,
        IReadOnlyDictionary<string, string?> query,
        ApiCaller caller,
        bool forAdministrator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(pathValues);
        ArgumentNullException.ThrowIfNull(query);
        var (values, errors) = Bind(endpoint, pathValues, query);
        int? limit = null;
        int? offset = null;
        if (endpoint.Kind == ApiEndpointKind.Query && !endpoint.SingleRow)
        {
            limit = Whole(query.GetValueOrDefault("limit"), endpoint.PageSize, 1, endpoint.PageSize, "limit", errors);
            offset = Whole(query.GetValueOrDefault("offset"), 0, 0, MaxOffset, "offset", errors);
        }

        if (errors.Count > 0)
        {
            return (ApiAnswer.Problem(400, "The parameters are invalid.", errors: errors.ToDictionary(e => e.Key, e => e.Value.ToArray(), StringComparer.Ordinal)), values, limit, offset);
        }

        try
        {
            var answer = endpoint.Kind == ApiEndpointKind.Query
                ? await RowsAsync(endpoint, values, caller, limit ?? 1, offset ?? 0, cancellationToken)
                : await FunctionAsync(endpoint, values, caller, cancellationToken);
            return (answer, values, limit, offset);
        }
        catch (ApiInputException ex)
        {
            return (ApiAnswer.Problem(400, "The parameters are invalid.", errors: ToParameters(endpoint, ex.Errors)), values, limit, offset);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogFailed(logger, ex, endpoint.Code);
            return (ApiAnswer.Problem(500, "The endpoint cannot answer now.", forAdministrator ? ex.Message : "Try again later; the call is logged.", error: ex.Message), values, limit, offset);
        }
    }

    private async Task<ApiAnswer> RowsAsync(ApiEndpoint endpoint, IReadOnlyDictionary<string, string?> values, ApiCaller caller, int limit, int offset, CancellationToken cancellationToken)
    {
        var fields = endpoint.Fields.Select(f => new DataField(f.Name, f.Column)).ToList();
        var definition = endpoint.Query is null ? null : JsonNode.Parse(endpoint.Query);
        if (endpoint.SingleRow)
        {
            var one = await queries.RunAsync(definition, fields, values, caller, 1, 0, cancellationToken);
            return one.Rows.Count == 0 ? ApiAnswer.Problem(404, "Not found.", "Nothing matches the parameters.") : new ApiAnswer(200, one.Rows[0], 1);
        }

        var page = await queries.RunAsync(definition, fields, values, caller, limit, offset, cancellationToken);
        return new ApiAnswer(200, new JsonObject
        {
            ["items"] = new JsonArray([.. page.Rows.Select(r => (JsonNode?)r)]),
            ["limit"] = limit,
            ["offset"] = offset,
            ["hasMore"] = page.HasMore,
        }, page.Rows.Count);
    }

    private async Task<ApiAnswer> FunctionAsync(ApiEndpoint endpoint, IReadOnlyDictionary<string, string?> values, ApiCaller caller, CancellationToken cancellationToken)
    {
        var function = functions.FirstOrDefault(f => f.Info.Code == endpoint.Function)
            ?? throw new InvalidOperationException($"The system function {endpoint.Function} is not available.");
        var inputs = endpoint.FunctionInputs.ToDictionary(
            b => b.Input,
            b => string.IsNullOrWhiteSpace(b.Parameter) ? b.Value : values.GetValueOrDefault(b.Parameter),
            StringComparer.Ordinal);
        var result = await function.InvokeAsync(inputs, caller, cancellationToken);
        return result.Found
            ? new ApiAnswer(200, result.Data, result.Data is JsonArray list ? list.Count : 1)
            : ApiAnswer.Problem(404, "Not found.", "Nothing matches the parameters.");
    }

    private static (Dictionary<string, string?> Values, Dictionary<string, List<string>> Errors) Bind(
        ApiEndpoint endpoint, IReadOnlyDictionary<string, string> pathValues, IReadOnlyDictionary<string, string?> query)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var parameter in endpoint.Parameters)
        {
            var raw = parameter.Location == ApiParameterLocation.Path ? pathValues.GetValueOrDefault(parameter.Name) : query.GetValueOrDefault(parameter.Name);
            raw = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
            if (raw is null)
            {
                if (parameter.IsRequired)
                {
                    errors[parameter.Name] = ["Required."];
                }
            }
            else if (raw.Length > 500)
            {
                errors[parameter.Name] = ["At most 500 characters."];
            }
            else if (Typed(raw, parameter.Type) is { } typed)
            {
                raw = typed;
            }
            else
            {
                errors[parameter.Name] = [parameter.Type switch
                {
                    ApiParameterType.Number => "Give a number.",
                    ApiParameterType.Date => "Give a date as YYYY-MM-DD.",
                    _ => "Give true or false.",
                }];
            }

            values[parameter.Name] = raw;
        }

        return (values, errors);
    }

    /// <summary>The value as the endpoint uses it, or null when it is not of the parameter's type.</summary>
    private static string? Typed(string raw, ApiParameterType type) => type switch
    {
        ApiParameterType.Number => decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number.ToString(CultureInfo.InvariantCulture) : null,
        ApiParameterType.Date => DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ? raw : null,
        ApiParameterType.Boolean => bool.TryParse(raw, out var flag) ? (flag ? "true" : "false") : null,
        _ => raw,
    };

    private static int Whole(string? raw, int fallback, int min, int max, string name, Dictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        if (int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= min && value <= max)
        {
            return value;
        }

        errors[name] = [$"Give a whole number from {min} to {max}."];
        return fallback;
    }

    /// <summary>A function's input problems, named after the endpoint's parameters that feed them.</summary>
    private static Dictionary<string, string[]> ToParameters(ApiEndpoint endpoint, IReadOnlyDictionary<string, string[]> errors) =>
        errors.GroupBy(e => endpoint.FunctionInputs.FirstOrDefault(b => b.Input == e.Key)?.Parameter is { Length: > 0 } parameter ? parameter : e.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.SelectMany(e => e.Value).ToArray(), StringComparer.Ordinal);

    [LoggerMessage(Level = LogLevel.Error, Message = "Public endpoint {Endpoint} failed")]
    private static partial void LogFailed(ILogger logger, Exception exception, string endpoint);
}

/// <summary>Each API client's calls per minute (per instance of the external host).</summary>
public sealed class PublicApiRateLimits : IDisposable
{
    private readonly ConcurrentDictionary<string, (int Limit, FixedWindowRateLimiter Limiter)> _limiters = new(StringComparer.Ordinal);

    /// <summary>Whether the client may call now; when not, how long until it may.</summary>
    public bool TryAcquire(string clientCode, int perMinute, out TimeSpan retryAfter)
    {
        var entry = _limiters.GetOrAdd(clientCode, _ => (perMinute, Create(perMinute)));
        if (entry.Limit != perMinute)
        {
            var replaced = (perMinute, Create(perMinute));
            if (_limiters.TryUpdate(clientCode, replaced, entry))
            {
                entry.Limiter.Dispose();
                entry = replaced;
            }
            else
            {
                replaced.Item2.Dispose();
                entry = _limiters[clientCode];
            }
        }

        using var lease = entry.Limiter.AttemptAcquire();
        if (lease.IsAcquired)
        {
            retryAfter = TimeSpan.Zero;
            return true;
        }

        retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var wait) ? wait : TimeSpan.FromMinutes(1);
        return false;
    }

    public void Dispose()
    {
        foreach (var entry in _limiters.Values)
        {
            entry.Limiter.Dispose();
        }

        _limiters.Clear();
    }

    private static FixedWindowRateLimiter Create(int perMinute) => new(new FixedWindowRateLimiterOptions
    {
        PermitLimit = Math.Max(1, perMinute),
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
        AutoReplenishment = true,
    });
}
