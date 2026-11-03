using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Integration.Changes;
using DynamicCube.Modules.Integration.Domain;
using DynamicCube.Modules.Integration.Persistence;
using DynamicCube.Platform.Integration;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DynamicCube.Modules.Integration.Runtime;

/// <summary>What the test console shows besides the result: the request sent and the provider's raw response.</summary>
public sealed record IntegrationTrace(string Method, string Url, string? RequestBody, int? HttpStatus, string? ResponseBody, int Attempts, int DurationMs, string? Error);

/// <summary>
/// Runs configured operations: builds the request, adds credentials, applies timeout, retries (idempotent operations
/// only) and the circuit breaker, reads the provider envelope, maps codes to outcomes and bilingual messages, caches and
/// writes a masked call log entry.
/// </summary>
internal sealed partial class IntegrationGateway(
    IntegrationDbContext context,
    DefinitionCache definitions,
    CircuitBreakers breakers,
    TokenProvider tokens,
    IHttpClientFactory httpClients,
    IMemoryCache cache,
    IServiceScopeFactory scopes,
    ICurrentUser user,
    TimeProvider clock,
    ILogger<IntegrationGateway> logger) : IIntegrationGateway
{
    public const string HttpClientName = IntegrationModule.HttpClientName;

    private const string UnavailableAr = "الخدمة الخارجية غير متاحة حالياً، يرجى المحاولة لاحقاً.";
    private const string UnavailableEn = "The external service is not available right now. Please try again later.";
    private const string NotFoundAr = "لا توجد بيانات مطابقة لدى الجهة المختصة.";
    private const string NotFoundEn = "No matching record was found at the provider.";

    public async Task<IntegrationResult> InvokeAsync(string operationCode, IReadOnlyDictionary<string, string?> inputs, CancellationToken cancellationToken) =>
        (await RunAsync(operationCode, inputs, useCache: true, cancellationToken)).Result;

    /// <summary>Calls the operation and returns the trace as well (test console). The response cache is bypassed.</summary>
    public Task<(IntegrationResult Result, IntegrationTrace Trace)> TestAsync(string operationCode, IReadOnlyDictionary<string, string?> inputs, CancellationToken cancellationToken) =>
        RunAsync(operationCode, inputs, useCache: false, cancellationToken);

    /// <summary>
    /// Calls an operation that is not saved yet (the operation designer's <em>Try it</em>): its fields are checked as saving
    /// would check them, and it runs on the published connection and envelope it names, without the response cache.
    /// </summary>
    public async Task<(IntegrationResult Result, IntegrationTrace Trace)> TryAsync(OperationPayload payload, IReadOnlyDictionary<string, string?> inputs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var errors = new ValidationErrors();
        errors.Required("connectionCode", payload.ConnectionCode);
        OperationApplier.Check(payload, errors);
        var connection = string.IsNullOrWhiteSpace(payload.ConnectionCode)
            ? null
            : await context.Connections.AsNoTracking().Include(c => c.Provider).FirstOrDefaultAsync(c => c.Code == payload.ConnectionCode, cancellationToken);
        if (!string.IsNullOrWhiteSpace(payload.ConnectionCode) && connection is null)
        {
            errors.Add("connectionCode", $"Connection '{payload.ConnectionCode}' is not published yet.");
        }

        var envelope = string.IsNullOrWhiteSpace(payload.EnvelopeProfileCode)
            ? null
            : await context.EnvelopeProfiles.AsNoTracking().FirstOrDefaultAsync(e => e.Code == payload.EnvelopeProfileCode, cancellationToken);
        if (!string.IsNullOrWhiteSpace(payload.EnvelopeProfileCode) && envelope is null)
        {
            errors.Add("envelopeProfileCode", $"Response envelope '{payload.EnvelopeProfileCode}' is not published yet.");
        }

        errors.ThrowIfAny();
        var operation = new Operation
        {
            Code = string.IsNullOrWhiteSpace(payload.Code) ? "DRAFT" : payload.Code.Trim(),
            NameAr = payload.NameAr,
            NameEn = payload.NameEn,
            Connection = connection,
            ConnectionId = connection!.Id,
            EnvelopeProfile = envelope,
            EnvelopeProfileId = envelope?.Id,
        };
        OperationApplier.Fill(operation, payload);
        operation.CacheSeconds = 0;
        return await RunAsync(operation, inputs, useCache: false, cancellationToken);
    }

    private async Task<(IntegrationResult Result, IntegrationTrace Trace)> RunAsync(string operationCode, IReadOnlyDictionary<string, string?> inputs, bool useCache, CancellationToken cancellationToken)
    {
        var operation = await definitions.GetAsync(context, operationCode, cancellationToken) ?? throw new IntegrationOperationNotFoundException(operationCode);
        return await RunAsync(operation, inputs, useCache, cancellationToken);
    }

    private async Task<(IntegrationResult Result, IntegrationTrace Trace)> RunAsync(Operation operation, IReadOnlyDictionary<string, string?> inputs, bool useCache, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var connection = operation.Connection!;
        ValidateInputs(operation, inputs);

        var started = clock.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();
        var request = BuildRequest(operation, inputs);
        var cacheKey = operation.CacheSeconds > 0 ? CacheKey(operation, inputs) : null;
        if (useCache && cacheKey is not null && cache.TryGetValue(cacheKey, out IntegrationResult? cached) && cached is not null)
        {
            var hit = cached with { CallId = Guid.CreateVersion7(), FromCache = true };
            // The cached result is logged as the response: it is what the caller used (evidence), masked like any response.
            await LogAsync(operation, request, hit, started, 0, null, hit.Data?.ToJsonString(), 0, null);
            return (hit, new IntegrationTrace(operation.Method, request.MaskedUrl, request.MaskedBody, null, null, 0, 0, null));
        }

        if (breakers.IsOpen(connection.Code))
        {
            var open = Technical(Guid.CreateVersion7(), "CIRCUIT_OPEN", null);
            await LogAsync(operation, request, open, started, 0, null, null, 0, "Circuit open: recent calls failed.");
            return (open, new IntegrationTrace(operation.Method, request.MaskedUrl, request.MaskedBody, null, null, 0, 0, "Circuit open: recent calls failed."));
        }

        var (status, body, attempts, error, errorCode) = await SendAsync(operation, connection, request, cancellationToken);
        stopwatch.Stop();

        var callId = Guid.CreateVersion7();
        var result = status is null ? Technical(callId, errorCode, null) : Interpret(callId, operation, status.Value, body);
        breakers.Record(connection.Code, result.Outcome == IntegrationOutcome.TechnicalError, connection.CircuitBreakerFailures, connection.CircuitBreakerSeconds);
        if (cacheKey is not null && result.Outcome is IntegrationOutcome.Success or IntegrationOutcome.NotFound)
        {
            cache.Set(cacheKey, result, TimeSpan.FromSeconds(operation.CacheSeconds));
        }

        var duration = (int)stopwatch.ElapsedMilliseconds;
        await LogAsync(operation, request, result, started, duration, status, body, attempts, error);
        return (result, new IntegrationTrace(operation.Method, request.MaskedUrl, request.MaskedBody, status, body, attempts, duration, error));
    }

    private async Task<(int? Status, string? Body, int Attempts, string? Error, string ErrorCode)> SendAsync(Operation operation, Connection connection, BuiltRequest built, CancellationToken cancellationToken)
    {
        var maxAttempts = 1 + (operation.IsIdempotent ? connection.RetryCount : 0);
        var client = httpClients.CreateClient(HttpClientName);
        var refreshedToken = false;
        var attempts = 0;
        string? error = null;
        var errorCode = "UNREACHABLE";
        while (attempts < maxAttempts)
        {
            attempts++;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(connection.TimeoutSeconds));
            try
            {
                // WS-Security travels in the envelope, with a fresh nonce and time for every attempt.
                using var request = built.Create(connection.AuthType == AuthType.WsSecurity ? tokens.SoapSecurityHeader(connection, built.SoapVersion) : null);
                await tokens.AuthorizeAsync(request, connection, timeout.Token);
                using var response = await client.SendAsync(request, timeout.Token);
                var body = await response.Content.ReadAsStringAsync(timeout.Token);
                var status = (int)response.StatusCode;
                if (response.StatusCode == HttpStatusCode.Unauthorized && connection.AuthType == AuthType.OAuth2ClientCredentials && !refreshedToken)
                {
                    // The cached token was revoked or expired early: get a new one once, without spending a retry.
                    tokens.Forget(connection);
                    refreshedToken = true;
                    attempts--;
                    continue;
                }

                if (status >= 500 && attempts < maxAttempts && !(built.SoapVersion is not null && SoapXml.IsFault(body)))
                {
                    await DelayAsync(attempts, cancellationToken);
                    continue;
                }

                return (status, body, attempts, null, string.Empty);
            }
            catch (IntegrationAuthException ex)
            {
                return (null, null, attempts, ex.Message, "AUTH");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                error = ex is TaskCanceledException ? $"Timed out after {connection.TimeoutSeconds} s." : ex.Message;
                errorCode = ex is TaskCanceledException ? "TIMEOUT" : "UNREACHABLE";
                if (attempts < maxAttempts)
                {
                    await DelayAsync(attempts, cancellationToken);
                }
            }
        }

        return (null, null, attempts, error, errorCode);
    }

    private static Task DelayAsync(int attempt, CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1)), cancellationToken);

    internal static IntegrationResult Interpret(Guid callId, Operation operation, int httpStatus, string? body)
    {
        JsonNode? json = null;
        SoapFault? fault = null;
        var readable = string.IsNullOrWhiteSpace(body);
        if (operation.Protocol == OperationProtocol.Soap)
        {
            // The XML response read as JSON (ADR-0044): the content of the body's element, or the fault.
            if (SoapXml.Read(body) is { } soap)
            {
                json = soap.Data;
                fault = soap.Fault;
                readable = true;
            }
        }
        else
        {
            try
            {
                json = string.IsNullOrWhiteSpace(body) ? null : JsonNode.Parse(body);
                readable = true;
            }
            catch (JsonException)
            {
                // Not JSON; decided by the HTTP status below.
            }
        }

        var envelope = operation.EnvelopeProfile;
        var statusCode = JsonPaths.ReadText(json, envelope?.StatusCodePath);
        var businessCode = JsonPaths.ReadText(json, envelope?.BusinessCodePath);
        var providerMessage = JsonPaths.ReadText(json, envelope?.BusinessMessagePath) ?? JsonPaths.ReadText(json, envelope?.StatusMessagePath) ?? fault?.Message;
        var code = businessCode ?? statusCode ?? fault?.Code ?? httpStatus.ToString(CultureInfo.InvariantCulture);
        var mapping = Find(operation.ResponseCodes, code) ?? Find(envelope?.ResponseCodes, code)
            ?? Find(operation.ResponseCodes, $"HTTP_{httpStatus}") ?? Find(envelope?.ResponseCodes, $"HTTP_{httpStatus}");

        IntegrationOutcome outcome;
        if (mapping is not null)
        {
            outcome = mapping.Outcome;
        }
        else if (fault is not null)
        {
            // A fault the provider blames on itself (Server / Receiver) is technical; one about the request is a business error.
            outcome = fault.IsServer ? IntegrationOutcome.TechnicalError : IntegrationOutcome.BusinessError;
        }
        else if (httpStatus >= 500 || httpStatus is 401 or 403 or 407 or 408 or 429)
        {
            outcome = IntegrationOutcome.TechnicalError;
        }
        else if (envelope?.StatusCodePath is not null && statusCode is not null && !envelope.SuccessStatusCodes.Contains(statusCode))
        {
            outcome = IntegrationOutcome.TechnicalError;
        }
        else if (httpStatus == 404 && businessCode is null)
        {
            outcome = IntegrationOutcome.NotFound;
        }
        else if (httpStatus >= 400)
        {
            outcome = IntegrationOutcome.BusinessError;
        }
        else if (!readable)
        {
            outcome = IntegrationOutcome.TechnicalError;
        }
        else if (envelope?.BusinessCodePath is not null)
        {
            outcome = businessCode is null ? IntegrationOutcome.TechnicalError
                : envelope.SuccessBusinessCodes.Contains(businessCode) ? IntegrationOutcome.Success
                : IntegrationOutcome.BusinessError;
        }
        else
        {
            outcome = IntegrationOutcome.Success;
        }

        var data = JsonPaths.Read(json, envelope?.ResultPath);
        var outputs = outcome == IntegrationOutcome.Success
            ? operation.Outputs.ToDictionary(o => o.Name, o => JsonPaths.Read(data, o.Path))
            : new Dictionary<string, JsonNode?>();
        var (messageAr, messageEn) = mapping is not null && (mapping.MessageAr is not null || mapping.MessageEn is not null)
            ? (mapping.MessageAr ?? mapping.MessageEn, mapping.MessageEn ?? mapping.MessageAr)
            : outcome switch
            {
                IntegrationOutcome.TechnicalError => (UnavailableAr, UnavailableEn),
                IntegrationOutcome.NotFound when providerMessage is null => (NotFoundAr, NotFoundEn),
                IntegrationOutcome.Success => (null, null),
                _ => (providerMessage, providerMessage),
            };
        return new IntegrationResult(outcome, code, messageAr, messageEn, outputs, outcome == IntegrationOutcome.TechnicalError ? null : data, callId, false);
    }

    private static ResponseCodeMapping? Find(List<ResponseCodeMapping>? codes, string code) =>
        codes?.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));

    private static IntegrationResult Technical(Guid callId, string code, string? message) =>
        new(IntegrationOutcome.TechnicalError, code, message ?? UnavailableAr, message ?? UnavailableEn, new Dictionary<string, JsonNode?>(), null, callId, false);

    private static void ValidateInputs(Operation operation, IReadOnlyDictionary<string, string?> inputs)
    {
        var errors = new Dictionary<string, string[]>();
        foreach (var parameter in operation.Parameters.Where(p => p.IsRequired && string.IsNullOrWhiteSpace(inputs.GetValueOrDefault(p.Name))))
        {
            errors[parameter.Name] = ["Required."];
        }

        foreach (var name in inputs.Keys.Where(k => operation.Parameters.All(p => p.Name != k)))
        {
            errors[name] = ["Not a parameter of this operation."];
        }

        if (errors.Count > 0)
        {
            throw new IntegrationInputException(errors);
        }
    }

    internal static BuiltRequest BuildRequest(Operation operation, IReadOnlyDictionary<string, string?> inputs)
    {
        var connection = operation.Connection!;
        var path = new StringBuilder(operation.PathTemplate);
        var maskedPath = new StringBuilder(operation.PathTemplate);
        var query = new List<string>();
        var maskedQuery = new List<string>();
        var headers = new List<(string Name, string Value)>(connection.Headers.Select(h => (h.Name, h.Value)));
        JsonObject? body = null;
        JsonObject? maskedBody = null;
        var soap = operation.Protocol == OperationProtocol.Soap ? operation.Soap : null;
        var xmlValues = new List<(string Target, string Value)>();
        var maskedXmlValues = new List<(string Target, string Value)>();
        foreach (var parameter in operation.Parameters)
        {
            var value = inputs.GetValueOrDefault(parameter.Name);
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            var shown = parameter.IsMasked ? Masking.Mask : value;
            var target = string.IsNullOrWhiteSpace(parameter.Target) ? parameter.Name : parameter.Target;
            switch (parameter.Location)
            {
                case ParameterLocation.Path:
                    path.Replace($"{{{parameter.Name}}}", Uri.EscapeDataString(value));
                    maskedPath.Replace($"{{{parameter.Name}}}", shown);
                    break;
                case ParameterLocation.Query:
                    query.Add($"{Uri.EscapeDataString(target)}={Uri.EscapeDataString(value)}");
                    maskedQuery.Add($"{Uri.EscapeDataString(target)}={shown}");
                    break;
                case ParameterLocation.Header:
                    headers.Add((target, value));
                    break;
                case ParameterLocation.Body when soap is not null:
                    xmlValues.Add((target, value));
                    maskedXmlValues.Add((target, shown));
                    break;
                case ParameterLocation.Body:
                    body ??= [];
                    maskedBody ??= [];
                    JsonPaths.Write(body, target, value);
                    JsonPaths.Write(maskedBody, target, shown);
                    break;
            }
        }

        if (soap is not null)
        {
            var soapUrl = connection.BaseUrl + path + (query.Count > 0 ? "?" + string.Join('&', query) : string.Empty);
            var soapMaskedUrl = connection.BaseUrl + maskedPath + (maskedQuery.Count > 0 ? "?" + string.Join('&', maskedQuery) : string.Empty);
            if (soap.Version != "1.2")
            {
                headers.Add(("SOAPAction", $"\"{soap.Action}\""));
            }

            return new BuiltRequest(
                HttpMethod.Post,
                new Uri(soapUrl),
                soapMaskedUrl,
                headers,
                SoapXml.Envelope(soap, xmlValues),
                SoapXml.WithHeader(SoapXml.Envelope(soap, maskedXmlValues), null),
                SoapXml.ContentType(soap),
                "application/soap+xml, text/xml",
                soap.Version);
        }

        var url = connection.BaseUrl + path + (query.Count > 0 ? "?" + string.Join('&', query) : string.Empty);
        var maskedUrl = connection.BaseUrl + maskedPath + (maskedQuery.Count > 0 ? "?" + string.Join('&', maskedQuery) : string.Empty);
        var bodyText = body?.ToJsonString() ?? (operation.Method is "POST" or "PUT" or "PATCH" ? "{}" : null);
        return new BuiltRequest(new HttpMethod(operation.Method), new Uri(url), maskedUrl, headers, bodyText, maskedBody?.ToJsonString() ?? (bodyText is null ? null : "{}"));
    }

    private string CacheKey(Operation operation, IReadOnlyDictionary<string, string?> inputs)
    {
        var canonical = string.Join('\n', inputs.OrderBy(i => i.Key, StringComparer.Ordinal).Select(i => $"{i.Key}={i.Value}"));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return $"integ:response:{definitions.Version}:{operation.Code}:{hash}";
    }

    private async Task LogAsync(Operation operation, BuiltRequest request, IntegrationResult result, DateTimeOffset started, int durationMs, int? httpStatus, string? responseBody, int attempts, string? error)
    {
        var fields = new HashSet<string>(operation.MaskFields, StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in operation.Parameters.Where(p => p.IsMasked))
        {
            fields.Add(parameter.Name);
            if (!string.IsNullOrWhiteSpace(parameter.Target))
            {
                fields.Add(parameter.Target.Split('.')[^1]);
            }
        }

        var entry = new CallLog
        {
            Id = result.CallId,
            OperationCode = operation.Code,
            ConnectionCode = operation.Connection!.Code,
            CorrelationId = Activity.Current?.TraceId.ToString(),
            UserId = user.UserId,
            AppCode = user.AppCode,
            StartedAt = started,
            DurationMs = durationMs,
            Outcome = result.Outcome,
            HttpStatus = httpStatus,
            Code = Truncate(result.Code, 100),
            Message = Truncate(result.MessageEn, 2000),
            FromCache = result.FromCache,
            Attempts = attempts,
            Method = operation.Method,
            Url = Truncate(request.MaskedUrl, 2000)!,
            RequestBody = Masking.Body(request.MaskedBody, fields),
            ResponseBody = Masking.Body(responseBody, fields),
            Error = Truncate(error, 4000),
            SubjectType = IntegrationSubject.Current?.Type,
            SubjectId = IntegrationSubject.Current?.Id,
        };

        try
        {
            // Own scope: the log is written even when the caller's unit of work is rolled back.
            await using var scope = scopes.CreateAsyncScope();
            var logContext = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
            logContext.CallLogs.Add(entry);
            await logContext.SaveChangesAsync(CancellationToken.None);
        }
#pragma warning disable CA1031 // A failing log must not fail the business call.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            CallLogFailed(logger, ex, operation.Code);
        }
    }

    private static string? Truncate(string? value, int max) => value is null || value.Length <= max ? value : value[..max];

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not write the call log of {Operation}")]
    private static partial void CallLogFailed(ILogger logger, Exception exception, string operation);
}

/// <summary>
/// A request that can be re-created for each attempt, plus its masked form for the log. A SOAP request
/// (<see cref="SoapVersion"/> set) carries its envelope; a header block (WS-Security) is added at each attempt.
/// </summary>
internal sealed record BuiltRequest(
    HttpMethod Method,
    Uri Url,
    string MaskedUrl,
    IReadOnlyList<(string Name, string Value)> Headers,
    string? Body,
    string? MaskedBody,
    string ContentType = "application/json",
    string Accept = "application/json",
    string? SoapVersion = null)
{
    public HttpRequestMessage Create(string? soapHeader = null)
    {
        var request = new HttpRequestMessage(Method, Url);
        request.Headers.Accept.ParseAdd(Accept);
        foreach (var (name, value) in Headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        var body = SoapVersion is null ? Body : SoapXml.WithHeader(Body ?? string.Empty, soapHeader);
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8);
            request.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(ContentType);
        }

        return request;
    }
}
