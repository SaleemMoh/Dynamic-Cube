using DynamicCube.Modules.Integration.Domain;
using DynamicCube.Modules.Integration.Persistence;
using DynamicCube.Modules.Integration.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Integration;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Integration.Changes;

public sealed class ProviderPayload : ConfigPayload
{
    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }
}

internal sealed class ProviderApplier(IntegrationDbContext db) : IntegrationApplier<Provider, ProviderPayload>(db)
{
    public override string EntityType => IntegrationChangeTypes.Provider;

    protected override IQueryable<Provider> Query(IntegrationDbContext context) => context.Providers;

    protected override Task ApplyAsync(Provider entity, ProviderPayload payload, IntegrationRefs refs, ValidationErrors errors, Provider? existing, CancellationToken cancellationToken)
    {
        errors.MaxLength("descriptionAr", payload.DescriptionAr, 2000);
        errors.MaxLength("descriptionEn", payload.DescriptionEn, 2000);
        entity.DescriptionAr = payload.DescriptionAr;
        entity.DescriptionEn = payload.DescriptionEn;
        return Task.CompletedTask;
    }

    protected override Task<ProviderPayload> ToPayloadAsync(IntegrationDbContext context, Provider entity, CancellationToken cancellationToken) =>
        Task.FromResult(new ProviderPayload { DescriptionAr = entity.DescriptionAr, DescriptionEn = entity.DescriptionEn });

    protected override async Task BeforeDeleteAsync(IntegrationDbContext context, Provider entity, CancellationToken cancellationToken)
    {
        if (await context.Connections.AnyAsync(c => c.ProviderId == entity.Id, cancellationToken))
        {
            throw new ChangeSetException($"Provider '{entity.Code}' still has connections.");
        }
    }
}

public sealed class ConnectionPayload : ConfigPayload
{
    public string? ProviderCode { get; set; }

    public string BaseUrl { get; set; } = string.Empty;

    public AuthType AuthType { get; set; }

    public string? SecretName { get; set; }

    public string? TokenUrl { get; set; }

    public string? ApiKeyHeader { get; set; }

    public int TimeoutSeconds { get; set; } = 30;

    public int RetryCount { get; set; } = 2;

    public int CircuitBreakerFailures { get; set; } = 5;

    public int CircuitBreakerSeconds { get; set; } = 60;

    public List<HeaderValue> Headers { get; set; } = [];
}

internal sealed class ConnectionApplier(IntegrationDbContext db) : IntegrationApplier<Connection, ConnectionPayload>(db)
{
    public override string EntityType => IntegrationChangeTypes.Connection;

    protected override IQueryable<Connection> Query(IntegrationDbContext context) => context.Connections.Include(c => c.Provider);

    protected override async Task ApplyAsync(Connection entity, ConnectionPayload payload, IntegrationRefs refs, ValidationErrors errors, Connection? existing, CancellationToken cancellationToken)
    {
        errors.Required("providerCode", payload.ProviderCode);
        entity.ProviderId = await refs.ProviderAsync(payload.ProviderCode, "providerCode", errors, cancellationToken) ?? Guid.Empty;

        if (!Uri.TryCreate(payload.BaseUrl, UriKind.Absolute, out var baseUrl) || (baseUrl.Scheme != Uri.UriSchemeHttps && baseUrl.Scheme != Uri.UriSchemeHttp))
        {
            errors.Add("baseUrl", "An absolute http(s) URL is required.");
        }

        if (payload.AuthType != AuthType.None)
        {
            errors.Code("secretName", payload.SecretName, ValidationErrors.IdentifierCode());
        }

        if (payload.AuthType == AuthType.OAuth2ClientCredentials)
        {
            errors.Required("tokenUrl", payload.TokenUrl);
        }

        Range(errors, "timeoutSeconds", payload.TimeoutSeconds, 1, 300);
        Range(errors, "retryCount", payload.RetryCount, 0, 5);
        Range(errors, "circuitBreakerFailures", payload.CircuitBreakerFailures, 1, 100);
        Range(errors, "circuitBreakerSeconds", payload.CircuitBreakerSeconds, 1, 3600);
        ChildSync.RequireUnique(payload.Headers, h => h.Name.ToUpperInvariant(), "headers", errors);
        foreach (var (header, i) in payload.Headers.Select((h, i) => (h, i)))
        {
            errors.Required($"headers[{i}].name", header.Name);
            if (string.Equals(header.Name, "Authorization", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"headers[{i}].name", "Credentials belong in the secret store, not in headers.");
            }
        }

        entity.BaseUrl = payload.BaseUrl.Trim().TrimEnd('/');
        entity.AuthType = payload.AuthType;
        entity.SecretName = payload.AuthType == AuthType.None ? null : payload.SecretName;
        entity.TokenUrl = payload.AuthType == AuthType.OAuth2ClientCredentials ? payload.TokenUrl?.Trim() : null;
        entity.ApiKeyHeader = payload.AuthType == AuthType.ApiKey ? (string.IsNullOrWhiteSpace(payload.ApiKeyHeader) ? "X-API-Key" : payload.ApiKeyHeader.Trim()) : null;
        entity.TimeoutSeconds = payload.TimeoutSeconds;
        entity.RetryCount = payload.RetryCount;
        entity.CircuitBreakerFailures = payload.CircuitBreakerFailures;
        entity.CircuitBreakerSeconds = payload.CircuitBreakerSeconds;
        entity.Headers = payload.Headers;
    }

    protected override Task<ConnectionPayload> ToPayloadAsync(IntegrationDbContext context, Connection entity, CancellationToken cancellationToken) =>
        Task.FromResult(new ConnectionPayload
        {
            ProviderCode = entity.Provider?.Code,
            BaseUrl = entity.BaseUrl,
            AuthType = entity.AuthType,
            SecretName = entity.SecretName,
            TokenUrl = entity.TokenUrl,
            ApiKeyHeader = entity.ApiKeyHeader,
            TimeoutSeconds = entity.TimeoutSeconds,
            RetryCount = entity.RetryCount,
            CircuitBreakerFailures = entity.CircuitBreakerFailures,
            CircuitBreakerSeconds = entity.CircuitBreakerSeconds,
            Headers = entity.Headers,
        });

    protected override async Task BeforeDeleteAsync(IntegrationDbContext context, Connection entity, CancellationToken cancellationToken)
    {
        if (await context.Operations.AnyAsync(o => o.ConnectionId == entity.Id, cancellationToken))
        {
            throw new ChangeSetException($"Connection '{entity.Code}' still has operations.");
        }
    }

    internal static void Range(ValidationErrors errors, string field, int value, int min, int max)
    {
        if (value < min || value > max)
        {
            errors.Add(field, $"Must be between {min} and {max}.");
        }
    }
}

public sealed class EnvelopeProfilePayload : ConfigPayload
{
    public string? StatusCodePath { get; set; }

    public string? StatusMessagePath { get; set; }

    public List<string> SuccessStatusCodes { get; set; } = [];

    public string? BusinessCodePath { get; set; }

    public string? BusinessMessagePath { get; set; }

    public List<string> SuccessBusinessCodes { get; set; } = [];

    public string? ResultPath { get; set; }

    public List<ResponseCodeMapping> ResponseCodes { get; set; } = [];
}

internal sealed class EnvelopeProfileApplier(IntegrationDbContext db) : IntegrationApplier<EnvelopeProfile, EnvelopeProfilePayload>(db)
{
    public override string EntityType => IntegrationChangeTypes.EnvelopeProfile;

    protected override IQueryable<EnvelopeProfile> Query(IntegrationDbContext context) => context.EnvelopeProfiles;

    protected override Task ApplyAsync(EnvelopeProfile entity, EnvelopeProfilePayload payload, IntegrationRefs refs, ValidationErrors errors, EnvelopeProfile? existing, CancellationToken cancellationToken)
    {
        OptionalPath(errors, "statusCodePath", payload.StatusCodePath);
        OptionalPath(errors, "statusMessagePath", payload.StatusMessagePath);
        OptionalPath(errors, "businessCodePath", payload.BusinessCodePath);
        OptionalPath(errors, "businessMessagePath", payload.BusinessMessagePath);
        OptionalPath(errors, "resultPath", payload.ResultPath);
        if (payload.StatusCodePath is not null && payload.SuccessStatusCodes.Count == 0)
        {
            errors.Add("successStatusCodes", "List the status codes that mean success.");
        }

        if (payload.BusinessCodePath is not null && payload.SuccessBusinessCodes.Count == 0)
        {
            errors.Add("successBusinessCodes", "List the business codes that mean success.");
        }

        ResponseCodes(errors, payload.ResponseCodes);
        entity.StatusCodePath = Blank(payload.StatusCodePath);
        entity.StatusMessagePath = Blank(payload.StatusMessagePath);
        entity.SuccessStatusCodes = payload.SuccessStatusCodes;
        entity.BusinessCodePath = Blank(payload.BusinessCodePath);
        entity.BusinessMessagePath = Blank(payload.BusinessMessagePath);
        entity.SuccessBusinessCodes = payload.SuccessBusinessCodes;
        entity.ResultPath = Blank(payload.ResultPath);
        entity.ResponseCodes = payload.ResponseCodes;

        return Task.CompletedTask;
    }

    protected override Task<EnvelopeProfilePayload> ToPayloadAsync(IntegrationDbContext context, EnvelopeProfile entity, CancellationToken cancellationToken) =>
        Task.FromResult(new EnvelopeProfilePayload
        {
            StatusCodePath = entity.StatusCodePath,
            StatusMessagePath = entity.StatusMessagePath,
            SuccessStatusCodes = entity.SuccessStatusCodes,
            BusinessCodePath = entity.BusinessCodePath,
            BusinessMessagePath = entity.BusinessMessagePath,
            SuccessBusinessCodes = entity.SuccessBusinessCodes,
            ResultPath = entity.ResultPath,
            ResponseCodes = entity.ResponseCodes,
        });

    protected override async Task BeforeDeleteAsync(IntegrationDbContext context, EnvelopeProfile entity, CancellationToken cancellationToken)
    {
        if (await context.Operations.AnyAsync(o => o.EnvelopeProfileId == entity.Id, cancellationToken))
        {
            throw new ChangeSetException($"Envelope profile '{entity.Code}' is used by operations.");
        }
    }

    internal static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static void OptionalPath(ValidationErrors errors, string field, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && !JsonPaths.IsValid(path.Trim()))
        {
            errors.Add(field, "Use a dot path such as data.result or items[*].code.");
        }
    }

    internal static void ResponseCodes(ValidationErrors errors, List<ResponseCodeMapping> codes)
    {
        ChildSync.RequireUnique(codes, c => c.Code, "responseCodes", errors);
        foreach (var (code, i) in codes.Select((c, i) => (c, i)))
        {
            errors.Required($"responseCodes[{i}].code", code.Code);
            errors.MaxLength($"responseCodes[{i}].messageAr", code.MessageAr, 1000);
            errors.MaxLength($"responseCodes[{i}].messageEn", code.MessageEn, 1000);
        }
    }
}

public sealed class OperationPayload : ConfigPayload
{
    public string? ConnectionCode { get; set; }

    public string? EnvelopeProfileCode { get; set; }

    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    public string Method { get; set; } = "GET";

    public string PathTemplate { get; set; } = string.Empty;

    public List<OperationParameter> Parameters { get; set; } = [];

    public List<OperationOutput> Outputs { get; set; } = [];

    public List<ResponseCodeMapping> ResponseCodes { get; set; } = [];

    public List<string> MaskFields { get; set; } = [];

    public int CacheSeconds { get; set; }

    public bool? IsIdempotent { get; set; }
}

internal sealed partial class OperationApplier(IntegrationDbContext db) : IntegrationApplier<Operation, OperationPayload>(db)
{
    private static readonly string[] Methods = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    public override string EntityType => IntegrationChangeTypes.Operation;

    protected override IQueryable<Operation> Query(IntegrationDbContext context) =>
        context.Operations.Include(o => o.Connection).Include(o => o.EnvelopeProfile);

    protected override IQueryable<Operation> ListQuery(IntegrationDbContext context, string? parent) =>
        (parent is null ? Query(context) : Query(context).Where(o => o.Connection!.Code == parent)).OrderBy(o => o.Code);

    protected override async Task ApplyAsync(Operation entity, OperationPayload payload, IntegrationRefs refs, ValidationErrors errors, Operation? existing, CancellationToken cancellationToken)
    {
        errors.Required("connectionCode", payload.ConnectionCode);
        entity.ConnectionId = await refs.ConnectionAsync(payload.ConnectionCode, "connectionCode", errors, cancellationToken) ?? Guid.Empty;
        entity.EnvelopeProfileId = await refs.EnvelopeProfileAsync(payload.EnvelopeProfileCode, "envelopeProfileCode", errors, cancellationToken);
        errors.MaxLength("descriptionAr", payload.DescriptionAr, 2000);
        errors.MaxLength("descriptionEn", payload.DescriptionEn, 2000);

        var method = payload.Method?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!Methods.Contains(method))
        {
            errors.Add("method", $"Use one of {string.Join(", ", Methods)}.");
        }

        var template = payload.PathTemplate?.Trim() ?? string.Empty;
        if (!template.StartsWith('/') || template.Contains("://", StringComparison.Ordinal))
        {
            errors.Add("pathTemplate", "A relative URL starting with '/' is required.");
        }

        ChildSync.RequireUnique(payload.Parameters, p => p.Name, "parameters", errors);
        foreach (var (parameter, i) in payload.Parameters.Select((p, i) => (p, i)))
        {
            var field = $"parameters[{i}]";
            errors.Code(field + ".name", parameter.Name, ParameterName());
            if (parameter.Location == ParameterLocation.Path && !template.Contains($"{{{parameter.Name}}}", StringComparison.Ordinal))
            {
                errors.Add(field + ".name", $"The path template has no {{{parameter.Name}}} placeholder.");
            }

            if (parameter.Location == ParameterLocation.Body && method == "GET")
            {
                errors.Add(field + ".location", "GET requests have no body.");
            }

            if (parameter.Location == ParameterLocation.Body && !JsonPaths.IsValidTarget(parameter.Target ?? parameter.Name))
            {
                errors.Add(field + ".target", "Use a dot path such as request.idNumber.");
            }
        }

        foreach (System.Text.RegularExpressions.Match placeholder in Placeholder().Matches(template))
        {
            var name = placeholder.Groups[1].Value;
            if (!payload.Parameters.Any(p => p.Name == name && p.Location == ParameterLocation.Path))
            {
                errors.Add("pathTemplate", $"Placeholder {{{name}}} has no path parameter.");
            }
        }

        ChildSync.RequireUnique(payload.Outputs, o => o.Name, "outputs", errors);
        foreach (var (output, i) in payload.Outputs.Select((o, i) => (o, i)))
        {
            errors.Code($"outputs[{i}].name", output.Name, ParameterName());
            if (!JsonPaths.IsValid(output.Path))
            {
                errors.Add($"outputs[{i}].path", "Use a dot path such as Licenses[*].Commercial_NO.");
            }
        }

        EnvelopeProfileApplier.ResponseCodes(errors, payload.ResponseCodes);
        ConnectionApplier.Range(errors, "cacheSeconds", payload.CacheSeconds, 0, 7 * 24 * 3600);

        entity.DescriptionAr = payload.DescriptionAr;
        entity.DescriptionEn = payload.DescriptionEn;
        entity.Method = method;
        entity.PathTemplate = template;
        entity.Parameters = payload.Parameters;
        entity.Outputs = payload.Outputs;
        entity.ResponseCodes = payload.ResponseCodes;
        entity.MaskFields = [.. payload.MaskFields.Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)];
        entity.CacheSeconds = payload.CacheSeconds;
        entity.IsIdempotent = payload.IsIdempotent ?? method is "GET" or "PUT" or "DELETE";
    }

    protected override Task<OperationPayload> ToPayloadAsync(IntegrationDbContext context, Operation entity, CancellationToken cancellationToken) =>
        Task.FromResult(new OperationPayload
        {
            ConnectionCode = entity.Connection?.Code,
            EnvelopeProfileCode = entity.EnvelopeProfile?.Code,
            DescriptionAr = entity.DescriptionAr,
            DescriptionEn = entity.DescriptionEn,
            Method = entity.Method,
            PathTemplate = entity.PathTemplate,
            Parameters = entity.Parameters,
            Outputs = entity.Outputs,
            ResponseCodes = entity.ResponseCodes,
            MaskFields = entity.MaskFields,
            CacheSeconds = entity.CacheSeconds,
            IsIdempotent = entity.IsIdempotent,
        });

    [System.Text.RegularExpressions.GeneratedRegex("^[A-Za-z][A-Za-z0-9_\\-]{0,63}$")]
    private static partial System.Text.RegularExpressions.Regex ParameterName();

    [System.Text.RegularExpressions.GeneratedRegex(@"\{([^}]*)\}")]
    private static partial System.Text.RegularExpressions.Regex Placeholder();
}
