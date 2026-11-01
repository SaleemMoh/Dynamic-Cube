using System.Text.Json.Serialization;
using DynamicCube.Platform.Integration;

namespace DynamicCube.Modules.Integration.Domain;

/// <summary>The external party, e.g. <c>MCI</c>, <c>NIC</c>; also the source of external code mappings.</summary>
public sealed class Provider : ConfigEntity
{
    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }
}

public enum AuthType
{
    None,

    /// <summary>Secret keys <c>username</c>, <c>password</c>.</summary>
    Basic,

    /// <summary>Secret key <c>apiKey</c>, sent in <see cref="Connection.ApiKeyHeader"/>.</summary>
    ApiKey,

    /// <summary>Secret keys <c>clientId</c>, <c>clientSecret</c>; the token is cached until it expires.</summary>
    [JsonStringEnumMemberName("OAUTH2_CLIENT_CREDENTIALS")]
    OAuth2ClientCredentials,

    /// <summary>
    /// SOAP only (ADR-0044): a WS-Security UsernameToken in the envelope's header, from secret keys <c>username</c> and
    /// <c>password</c> (sent as text, or as a digest with a nonce and the time when <see cref="Connection.PasswordDigest"/>).
    /// </summary>
    [JsonStringEnumMemberName("WS_SECURITY")]
    WsSecurity,
}

/// <summary>Base URL, credentials and resilience policy shared by the operations of one gateway or system.</summary>
public sealed class Connection : ConfigEntity
{
    public Guid ProviderId { get; set; }

    public Provider? Provider { get; set; }

    public string BaseUrl { get; set; } = string.Empty;

    public AuthType AuthType { get; set; }

    /// <summary>Name of the credentials in the secret store (<c>Secrets:{name}:*</c>); values are never stored in tables.</summary>
    public string? SecretName { get; set; }

    /// <summary>OAuth2 token URL, relative to <see cref="BaseUrl"/> or absolute.</summary>
    public string? TokenUrl { get; set; }

    public string? ApiKeyHeader { get; set; }

    /// <summary>WS-Security: the UsernameToken carries a digest of the password (with a nonce and the time) instead of the text.</summary>
    public bool PasswordDigest { get; set; }

    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Retries of idempotent operations after timeouts, network errors and 5xx responses.</summary>
    public int RetryCount { get; set; } = 2;

    /// <summary>Consecutive technical failures that open the circuit (calls fail fast while it is open).</summary>
    public int CircuitBreakerFailures { get; set; } = 5;

    public int CircuitBreakerSeconds { get; set; } = 60;

    public List<HeaderValue> Headers { get; set; } = [];
}

public sealed class HeaderValue
{
    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// How to read a provider family's response wrapper, e.g. MoMRAH gateway: <c>statusDetails.code</c> (200),
/// <c>data.responseCode</c> ("1"), message <c>data.responseMessage</c>, result <c>data.result</c>.
/// Paths are dot-separated; an array met on the way is read through its first element.
/// </summary>
public sealed class EnvelopeProfile : ConfigEntity
{
    public string? StatusCodePath { get; set; }

    public string? StatusMessagePath { get; set; }

    public List<string> SuccessStatusCodes { get; set; } = [];

    public string? BusinessCodePath { get; set; }

    public string? BusinessMessagePath { get; set; }

    public List<string> SuccessBusinessCodes { get; set; } = [];

    /// <summary>Where the payload sits; empty means the whole body.</summary>
    public string? ResultPath { get; set; }

    /// <summary>Codes shared by every operation of the family; an operation's own map wins.</summary>
    public List<ResponseCodeMapping> ResponseCodes { get; set; } = [];
}

/// <summary>Provider code → outcome and the message shown to users instead of the provider's text.</summary>
public sealed class ResponseCodeMapping
{
    public string Code { get; set; } = string.Empty;

    public IntegrationOutcome Outcome { get; set; }

    public string? MessageAr { get; set; }

    public string? MessageEn { get; set; }
}

public enum ParameterLocation
{
    Path,
    Query,
    Header,
    Body,
}

public sealed class OperationParameter
{
    public string Name { get; set; } = string.Empty;

    public ParameterLocation Location { get; set; }

    /// <summary>Name in the request when it differs from <see cref="Name"/>; for body parameters a dot path.</summary>
    public string? Target { get; set; }

    public bool IsRequired { get; set; } = true;

    /// <summary>Personal data: replaced by <c>***</c> in the call log.</summary>
    public bool IsMasked { get; set; }

    public string? SampleValue { get; set; }
}

/// <summary>A named value read from the result, e.g. <c>crNumbers</c> = <c>Licenses[*].Commercial_NO</c>.</summary>
public sealed class OperationOutput
{
    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;
}

/// <summary>How an operation talks to the provider.</summary>
public enum OperationProtocol
{
    /// <summary>HTTP with JSON (query, path, header and JSON body inputs).</summary>
    Rest,

    /// <summary>SOAP 1.1 or 1.2 (ADR-0044): an XML envelope built from the inputs, the XML response read as JSON.</summary>
    Soap,
}

/// <summary>
/// The SOAP call of an operation (ADR-0044): the envelope version, the <c>SOAPAction</c>, and the operation element of the
/// body with its namespace; body inputs become its child elements (dot paths nest them), in the order of the inputs.
/// </summary>
public sealed class SoapSettings
{
    /// <summary><c>1.1</c> (text/xml with a SOAPAction header) or <c>1.2</c> (application/soap+xml with the action in the content type).</summary>
    public string Version { get; set; } = "1.1";

    public string? Action { get; set; }

    /// <summary>The body's element, e.g. <c>GetPersonInfo</c>.</summary>
    public string OperationName { get; set; } = string.Empty;

    /// <summary>Its namespace (the WSDL's target namespace), e.g. <c>http://yakeen.example/person</c>.</summary>
    public string? Namespace { get; set; }

    /// <summary>Child elements in the same namespace (<c>elementFormDefault="qualified"</c>); otherwise without one.</summary>
    public bool QualifiedElements { get; set; } = true;
}

/// <summary>One call of an external API: request shape, response reading, cache and masking.</summary>
public sealed class Operation : ConfigEntity
{
    public Guid ConnectionId { get; set; }

    public Connection? Connection { get; set; }

    public Guid? EnvelopeProfileId { get; set; }

    public EnvelopeProfile? EnvelopeProfile { get; set; }

    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    public OperationProtocol Protocol { get; set; }

    /// <summary>The SOAP call; set for <see cref="OperationProtocol.Soap"/> operations only.</summary>
    public SoapSettings? Soap { get; set; }

    public string Method { get; set; } = "GET";

    /// <summary>Relative URL with <c>{name}</c> placeholders for path parameters.</summary>
    public string PathTemplate { get; set; } = string.Empty;

    public List<OperationParameter> Parameters { get; set; } = [];

    public List<OperationOutput> Outputs { get; set; } = [];

    public List<ResponseCodeMapping> ResponseCodes { get; set; } = [];

    /// <summary>Response fields replaced by <c>***</c> in the call log (matched by property name at any depth).</summary>
    public List<string> MaskFields { get; set; } = [];

    /// <summary>Successful and not-found results are reused for this long; 0 disables caching.</summary>
    public int CacheSeconds { get; set; }

    /// <summary>Safe to repeat after a failure; defaults to true for GET.</summary>
    public bool IsIdempotent { get; set; } = true;
}

/// <summary>Every integration call, with personal data masked. Not configuration; written by the gateway.</summary>
public sealed class CallLog
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string OperationCode { get; set; } = string.Empty;

    public string ConnectionCode { get; set; } = string.Empty;

    public string? CorrelationId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }

    public int DurationMs { get; set; }

    public IntegrationOutcome Outcome { get; set; }

    public int? HttpStatus { get; set; }

    public string? Code { get; set; }

    public string? Message { get; set; }

    public bool FromCache { get; set; }

    public int Attempts { get; set; }

    public string Method { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string? RequestBody { get; set; }

    public string? ResponseBody { get; set; }

    public string? Error { get; set; }
}
