namespace DynamicCube.Modules.Integration.Domain;

/// <summary>What a public endpoint does: read rows built in the query builder, or call a read-only system function.</summary>
public enum ApiEndpointKind
{
    Query,
    Function,
}

/// <summary>Where a caller gives a parameter.</summary>
public enum ApiParameterLocation
{
    Path,
    Query,
}

/// <summary>The kind of value a parameter takes (checked before the endpoint runs, and described in the OpenAPI document).</summary>
public enum ApiParameterType
{
    Text,
    Number,
    Date,
    Boolean,
}

/// <summary>A parameter of a public endpoint: a <c>{placeholder}</c> of its path or a query string value.</summary>
public sealed class ApiEndpointParameter
{
    public string Name { get; set; } = string.Empty;

    public ApiParameterLocation Location { get; set; }

    public ApiParameterType Type { get; set; }

    /// <summary>Path parameters are always required.</summary>
    public bool IsRequired { get; set; }

    /// <summary>Personal data (e.g. a national ID): replaced by <c>***</c> in the call log.</summary>
    public bool IsMasked { get; set; }

    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    /// <summary>An example value (the designer's try and the OpenAPI document).</summary>
    public string? Sample { get; set; }
}

/// <summary>A field of a read-data endpoint's rows: its name in the response and the column (<c>alias.column</c>) it reads.</summary>
public sealed class ApiEndpointField
{
    public string Name { get; set; } = string.Empty;

    public string Column { get; set; } = string.Empty;

    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }
}

/// <summary>What a system function's input gets: one of the endpoint's parameters, or a fixed value.</summary>
public sealed class ApiFunctionBinding
{
    public string Input { get; set; } = string.Empty;

    public string? Parameter { get; set; }

    public string? Value { get; set; }
}

/// <summary>
/// A read-only endpoint of the public API (ADR-0042) on the external host, at <c>/api/public/v1{Path}</c>. It reads rows of
/// registered tables and classes (a query built in the query builder) or calls a system function such as a request's
/// status; API clients call it with their own token.
/// </summary>
public sealed class ApiEndpoint : ConfigEntity
{
    /// <summary>Relative path with <c>{name}</c> placeholders, e.g. <c>/requests/{number}/status</c>.</summary>
    public string Path { get; set; } = string.Empty;

    public ApiEndpointKind Kind { get; set; }

    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    public List<ApiEndpointParameter> Parameters { get; set; } = [];

    /// <summary>The query builder definition (JSON) of a read-data endpoint.</summary>
    public string? Query { get; set; }

    public List<ApiEndpointField> Fields { get; set; } = [];

    /// <summary>A read-data endpoint that returns one row (404 when there is none) instead of a page of rows.</summary>
    public bool SingleRow { get; set; }

    /// <summary>Rows per page when the caller gives no <c>limit</c>; also the most a caller may ask for.</summary>
    public int PageSize { get; set; } = 50;

    /// <summary>The system function of a function endpoint (e.g. <c>REQUEST_STATUS</c>).</summary>
    public string? Function { get; set; }

    public List<ApiFunctionBinding> FunctionInputs { get; set; } = [];
}

/// <summary>
/// A partner system allowed to call the public API: its sign-in client (OAuth2 client credentials in the realm the external
/// host trusts), the endpoints it may call, the companies and branches whose data it sees, and its rate limit. The
/// secret is the identity provider's and is never stored.
/// </summary>
public sealed class ApiClient : ConfigEntity
{
    /// <summary>The client id its tokens carry (<c>azp</c>).</summary>
    public string ClientId { get; set; } = string.Empty;

    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    /// <summary>Codes of the endpoints it may call.</summary>
    public List<string> Endpoints { get; set; } = [];

    /// <summary>Companies whose data it sees; <c>*</c> for all of them.</summary>
    public List<string> Companies { get; set; } = [];

    /// <summary>Branches whose data it sees; <c>*</c> for all of them.</summary>
    public List<string> Branches { get; set; } = [];

    public int RateLimitPerMinute { get; set; } = 60;

    /// <summary>Who to contact about the integration (shown to administrators).</summary>
    public string? ContactEmail { get; set; }
}

/// <summary>Every call of the public API, with masked parameters. Not configuration; written by the public API.</summary>
public sealed class ApiCall
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string ClientId { get; set; } = string.Empty;

    /// <summary>The API client's code (empty when the token's client is not registered).</summary>
    public string ClientCode { get; set; } = string.Empty;

    public string? EndpointCode { get; set; }

    public string Method { get; set; } = "GET";

    /// <summary>The path and query called, masked parameters replaced by <c>***</c>.</summary>
    public string Path { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }

    public int DurationMs { get; set; }

    public int Status { get; set; }

    /// <summary>Rows returned by a read-data endpoint.</summary>
    public int? Rows { get; set; }

    public string? Error { get; set; }

    public string? CorrelationId { get; set; }

    public string? ClientIp { get; set; }

    /// <summary>Tried by an administrator in the endpoint designer, not called by a partner.</summary>
    public bool IsTry { get; set; }
}
