using System.Text.Json.Nodes;

namespace DynamicCube.Platform.PublicApi;

/// <summary>
/// A system calling the platform's public API (ADR-0042): the API client it signed in as and the data it may see — the
/// companies and branches given to it (null: all of them).
/// </summary>
public sealed record ApiCaller(string ClientCode, IReadOnlyList<string>? Companies, IReadOnlyList<string>? Branches)
{
    /// <summary>A caller limited to nothing (the endpoint designer trying an endpoint as the administrator).</summary>
    public static ApiCaller Unlimited { get; } = new("ADMIN", null, null);

    /// <summary>Whether a row of that company and branch is visible to the caller.</summary>
    public bool Sees(string? company, string? branch) =>
        (Companies is null || (company is not null && Companies.Contains(company, StringComparer.Ordinal)))
        && (Branches is null || (branch is not null && Branches.Contains(branch, StringComparer.Ordinal)));
}

/// <summary>An input of a system function, as the endpoint designer offers it.</summary>
public sealed record ApiFunctionInput(string Name, bool Required, string DescriptionAr, string DescriptionEn);

/// <summary>A read-only system function a public endpoint may call (e.g. a request's status with its place in the workflow).</summary>
/// <param name="Sample">An example of what it returns, shown in the designer and described in the OpenAPI document.</param>
public sealed record ApiFunctionInfo(
    string Code,
    string NameAr,
    string NameEn,
    string DescriptionAr,
    string DescriptionEn,
    IReadOnlyList<ApiFunctionInput> Inputs,
    JsonNode Sample);

/// <summary>What a function returns: the data, or nothing found (the caller gets 404).</summary>
public sealed record ApiFunctionResult(JsonNode? Data)
{
    public static ApiFunctionResult NotFound { get; } = new((JsonNode?)null);

    public bool Found => Data is not null;
}

/// <summary>
/// A read-only system function contributed by a module (class values, request status, document check). It must apply the
/// caller's company and branch limits itself and never return what a limited caller may not see.
/// </summary>
public interface IApiFunction
{
    ApiFunctionInfo Info { get; }

    /// <summary>Runs the function; throws <see cref="ApiInputException"/> for inputs it cannot use.</summary>
    Task<ApiFunctionResult> InvokeAsync(IReadOnlyDictionary<string, string?> inputs, ApiCaller caller, CancellationToken cancellationToken);
}

/// <summary>A field of a data endpoint: its name in the response and the column (<c>alias.column</c>) it reads.</summary>
public sealed record DataField(string Name, string Column);

/// <summary>The kinds of values a data endpoint's field returns.</summary>
public static class DataTypes
{
    public const string Text = "TEXT";
    public const string Number = "NUMBER";

    /// <summary>A date (<c>YYYY-MM-DD</c>) or a date and time (ISO 8601).</summary>
    public const string Date = "DATE";
    public const string Boolean = "BOOLEAN";

    /// <summary>A list or a JSON value.</summary>
    public const string Json = "JSON";
}

/// <summary>Rows of a data endpoint (one page) and whether more follow.</summary>
public sealed record DataRows(IReadOnlyList<JsonObject> Rows, bool HasMore);

/// <summary>
/// Read-data queries of public endpoints (implemented by Apps Control): definitions built in the visual query builder over
/// registered tables and classes, returning the chosen columns. Conditions may compare with the endpoint's inputs and with
/// the caller's companies and branches. Queries run read-only with a time limit.
/// </summary>
public interface IDataQueries
{
    /// <summary>What is wrong with the definition (field → messages); empty when it can run.</summary>
    Task<IReadOnlyDictionary<string, string[]>> CheckAsync(JsonNode? query, IReadOnlyList<DataField> fields, IReadOnlyCollection<string> inputs, CancellationToken cancellationToken);

    /// <summary>The kind of value each field returns (<see cref="DataTypes"/>), for the OpenAPI document; empty when the definition has problems.</summary>
    Task<IReadOnlyDictionary<string, string>> TypesAsync(JsonNode? query, IReadOnlyList<DataField> fields, IReadOnlyCollection<string> inputs, CancellationToken cancellationToken);

    /// <summary>
    /// One page of rows for the caller; throws <see cref="ApiInputException"/> when an input does not fit its column.
    /// <paramref name="inputs"/> holds every input of the endpoint, null when the caller left it out (its conditions are skipped).
    /// </summary>
    Task<DataRows> RunAsync(
        JsonNode? query,
        IReadOnlyList<DataField> fields,
        IReadOnlyDictionary<string, string?> inputs,
        ApiCaller caller,
        int limit,
        int offset,
        CancellationToken cancellationToken);
}

/// <summary>Inputs a public endpoint cannot use (the caller gets 400 with these messages).</summary>
public sealed class ApiInputException : Exception
{
    public ApiInputException()
        : this(new Dictionary<string, string[]>())
    {
    }

    public ApiInputException(string message)
        : this(new Dictionary<string, string[]> { ["request"] = [message] })
    {
    }

    public ApiInputException(string message, Exception innerException)
        : base(message, innerException)
    {
        Errors = new Dictionary<string, string[]> { ["request"] = [message] };
    }

    public ApiInputException(IReadOnlyDictionary<string, string[]> errors)
        : base("The inputs are invalid.")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

/// <summary>A partner system's sign-in client in the identity provider, as it stands.</summary>
public sealed record ServiceClient(string ClientId, bool Enabled, bool ServiceAccount, bool HasAudience);

/// <summary>
/// Sign-in clients of partner systems (OAuth2 client credentials) in the realm the public API trusts, maintained from the
/// platform. The secret is the identity provider's: it is shown once and never stored by the platform.
/// </summary>
public interface IServiceClients
{
    /// <summary>False when clients are maintained in the identity provider directly.</summary>
    bool IsManaged { get; }

    /// <summary>The token address callers use (the realm's token endpoint), when it can be read.</summary>
    Task<string?> TokenUrlAsync(CancellationToken cancellationToken);

    Task<ServiceClient?> FindAsync(string clientId, string audience, CancellationToken cancellationToken);

    /// <summary>Creates the client (client credentials only, with the audience) or brings it in line; true when created.</summary>
    Task<bool> SaveAsync(string clientId, string name, string audience, CancellationToken cancellationToken);

    Task<string> SecretAsync(string clientId, CancellationToken cancellationToken);

    Task<string> NewSecretAsync(string clientId, CancellationToken cancellationToken);
}

/// <summary>The identity provider could not do what was asked (unreachable, refused, or the client is missing).</summary>
public sealed class ServiceClientException : Exception
{
    public ServiceClientException()
    {
    }

    public ServiceClientException(string message)
        : base(message)
    {
    }

    public ServiceClientException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The public API's token audience and the policy its endpoints require (ADR-0042).</summary>
public static class PublicApiAuthorization
{
    public const string DefaultAudience = "dynamic-cube-public-api";

    public const string Policy = "public-api";

    /// <summary>Whether a token was issued for the public API only (it must not reach any other endpoint).</summary>
    public static bool IsPublicOnly(System.Security.Claims.ClaimsPrincipal user, string? platformAudience, string publicAudience)
    {
        ArgumentNullException.ThrowIfNull(user);
        var audiences = user.FindAll("aud").Select(c => c.Value).ToList();
        return audiences.Contains(publicAudience, StringComparer.Ordinal)
            && (platformAudience is null || !audiences.Contains(platformAudience, StringComparer.Ordinal));
    }
}
