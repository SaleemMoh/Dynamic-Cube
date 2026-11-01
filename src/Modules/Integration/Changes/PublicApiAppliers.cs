using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DynamicCube.Modules.Integration.Domain;
using DynamicCube.Modules.Integration.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.PublicApi;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Integration.Changes;

public sealed class ApiEndpointPayload : ConfigPayload
{
    public string? Path { get; set; }

    public ApiEndpointKind Kind { get; set; }

    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    public List<ApiEndpointParameter> Parameters { get; set; } = [];

    public JsonNode? Query { get; set; }

    public List<ApiEndpointField> Fields { get; set; } = [];

    public bool SingleRow { get; set; }

    public int PageSize { get; set; } = 50;

    public string? Function { get; set; }

    public List<ApiFunctionBinding> FunctionInputs { get; set; } = [];

    /// <summary>Whether a client may name a registered person (<c>X-Acting-User</c>) whose access then applies.</summary>
    public bool ActsForUser { get; set; }
}

/// <summary>
/// Public endpoints (ADR-0042): a path unique in its shape, parameters that match its placeholders, and either a read-data
/// query over registered tables and classes with the columns it returns, or a system function with its inputs given.
/// </summary>
internal sealed partial class ApiEndpointApplier(IntegrationDbContext db, IDataQueries queries, IEnumerable<IApiFunction> functions)
    : IntegrationApplier<ApiEndpoint, ApiEndpointPayload>(db)
{
    public const int MaxPageSize = 1000;

    /// <summary>Query string names the runtime keeps for paging.</summary>
    public static readonly string[] Reserved = ["limit", "offset"];

    public override string EntityType => IntegrationChangeTypes.ApiEndpoint;

    protected override IQueryable<ApiEndpoint> Query(IntegrationDbContext context) => context.ApiEndpoints;

    protected override IQueryable<ApiEndpoint> ListQuery(IntegrationDbContext context, string? parent) => context.ApiEndpoints.OrderBy(e => e.Path);

    protected override async Task ApplyAsync(ApiEndpoint entity, ApiEndpointPayload payload, IntegrationRefs refs, ValidationErrors errors, ApiEndpoint? existing, CancellationToken cancellationToken)
    {
        var problems = await CheckAsync(payload, queries, functions, cancellationToken);
        foreach (var (field, messages) in problems)
        {
            foreach (var message in messages)
            {
                errors.Add(field, message);
            }
        }

        if (!problems.ContainsKey("path"))
        {
            await UniquePathAsync(refs, entity.Code.Length > 0 ? entity.Code : existing?.Code, payload.Path!, errors, cancellationToken);
        }

        Fill(entity, payload);
    }

    /// <summary>The checks of an endpoint's own fields (also made before an unsaved definition is tried).</summary>
    internal static async Task<Dictionary<string, List<string>>> CheckAsync(
        ApiEndpointPayload payload, IDataQueries queries, IEnumerable<IApiFunction> functions, CancellationToken cancellationToken)
    {
        var problems = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        void Add(string field, string message)
        {
            if (!problems.TryGetValue(field, out var list))
            {
                problems[field] = list = [];
            }

            list.Add(message);
        }

        var path = payload.Path?.Trim() ?? string.Empty;
        if (path.Length > 300 || !PathPattern().IsMatch(path))
        {
            Add("path", "Use a path such as /regions or /requests/{number}/status: words of letters, digits, - and _, and {placeholders}.");
        }
        else if (path.Equals("/openapi.json", StringComparison.OrdinalIgnoreCase))
        {
            Add("path", "/openapi.json is the API's own description.");
        }

        if (payload.DescriptionAr?.Length > 2000)
        {
            Add("descriptionAr", "At most 2000 characters.");
        }

        if (payload.DescriptionEn?.Length > 2000)
        {
            Add("descriptionEn", "At most 2000 characters.");
        }

        var placeholders = Placeholders(path);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (parameter, i) in payload.Parameters.Select((p, i) => (p, i)))
        {
            var field = $"parameters[{i}]";
            var name = parameter.Name ?? string.Empty;
            if (!ParameterName().IsMatch(name))
            {
                Add(field + ".name", "Use a name of English letters, digits and _, starting with a letter.");
                continue;
            }

            if (!names.Add(name))
            {
                Add(field + ".name", $"'{parameter.Name}' is listed more than once.");
            }

            if (Reserved.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                Add(field + ".name", "limit and offset are kept for paging.");
            }

            if (parameter.Location == ApiParameterLocation.Path && !placeholders.Contains(name))
            {
                Add(field + ".name", $"The path has no {{{parameter.Name}}} placeholder.");
            }

            if (parameter.Location == ApiParameterLocation.Query && placeholders.Contains(name))
            {
                Add(field + ".location", $"{{{parameter.Name}}} is in the path: it is a path parameter.");
            }

            if (parameter.Sample?.Length > 200 || parameter.DescriptionAr?.Length > 500 || parameter.DescriptionEn?.Length > 500)
            {
                Add(field + ".sample", "Keep the sample under 200 characters and descriptions under 500.");
            }
        }

        foreach (var placeholder in placeholders.Where(p => !payload.Parameters.Any(x => x.Name == p && x.Location == ApiParameterLocation.Path)))
        {
            Add("path", $"Placeholder {{{placeholder}}} has no path parameter.");
        }

        if (payload.Kind == ApiEndpointKind.Query)
        {
            if (payload.PageSize is < 1 or > MaxPageSize)
            {
                Add("pageSize", $"Must be between 1 and {MaxPageSize}.");
            }

            var fields = payload.Fields.Select(f => new DataField(f.Name, f.Column)).ToList();
            foreach (var (field, messages) in await queries.CheckAsync(payload.Query, fields, [.. names], cancellationToken))
            {
                foreach (var message in messages)
                {
                    Add(field, message);
                }
            }
        }
        else
        {
            CheckFunction(payload, names, functions, Add);
        }

        return problems;
    }

    private static void CheckFunction(ApiEndpointPayload payload, HashSet<string> parameters, IEnumerable<IApiFunction> functions, Action<string, string> add)
    {
        var function = functions.FirstOrDefault(f => f.Info.Code == payload.Function);
        if (function is null)
        {
            add("function", "Choose the system function the endpoint calls.");
            return;
        }

        var inputs = function.Info.Inputs;
        var bound = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (binding, i) in payload.FunctionInputs.Select((b, i) => (b, i)))
        {
            var field = $"functionInputs[{i}]";
            if (!inputs.Any(x => x.Name == binding.Input))
            {
                add(field + ".input", $"{function.Info.Code} has no input '{binding.Input}'.");
                continue;
            }

            if (!bound.Add(binding.Input))
            {
                add(field + ".input", $"'{binding.Input}' is given more than once.");
            }

            var fromParameter = !string.IsNullOrWhiteSpace(binding.Parameter);
            var fixedValue = !string.IsNullOrWhiteSpace(binding.Value);
            if (!fromParameter && !fixedValue)
            {
                // Left empty: the input is not given (an optional input then means "all"; a required one is reported below).
                bound.Remove(binding.Input);
                continue;
            }

            if (fromParameter && fixedValue)
            {
                add(field + ".parameter", "Give the input one of the endpoint's parameters or a fixed value, not both.");
            }
            else if (fromParameter && !parameters.Contains(binding.Parameter!))
            {
                add(field + ".parameter", $"The endpoint has no parameter '{binding.Parameter}'.");
            }
            else if (binding.Value?.Length > 500)
            {
                add(field + ".value", "At most 500 characters.");
            }
        }

        foreach (var input in inputs.Where(x => x.Required && !bound.Contains(x.Name)))
        {
            add("functionInputs", $"Give {function.Info.Code} its input '{input.Name}'.");
        }
    }

    /// <summary>Copies a checked payload onto the endpoint (path parameters are always required).</summary>
    internal static void Fill(ApiEndpoint entity, ApiEndpointPayload payload)
    {
        entity.Path = payload.Path?.Trim() ?? string.Empty;
        entity.Kind = payload.Kind;
        entity.DescriptionAr = payload.DescriptionAr;
        entity.DescriptionEn = payload.DescriptionEn;
        entity.Parameters = [.. payload.Parameters.Select(p =>
        {
            p.IsRequired = p.IsRequired || p.Location == ApiParameterLocation.Path;
            return p;
        })];
        var isQuery = payload.Kind == ApiEndpointKind.Query;
        entity.Query = isQuery ? payload.Query?.ToJsonString() : null;
        entity.Fields = isQuery ? payload.Fields : [];
        entity.SingleRow = isQuery && payload.SingleRow;
        entity.PageSize = isQuery ? payload.PageSize : 50;
        entity.Function = isQuery ? null : payload.Function;
        entity.FunctionInputs = isQuery ? [] : [.. payload.FunctionInputs.Where(b => !string.IsNullOrWhiteSpace(b.Parameter) || !string.IsNullOrWhiteSpace(b.Value))];
        entity.ActsForUser = payload.ActsForUser;
    }

    protected override Task<ApiEndpointPayload> ToPayloadAsync(IntegrationDbContext context, ApiEndpoint entity, CancellationToken cancellationToken) =>
        Task.FromResult(new ApiEndpointPayload
        {
            Path = entity.Path,
            Kind = entity.Kind,
            DescriptionAr = entity.DescriptionAr,
            DescriptionEn = entity.DescriptionEn,
            Parameters = entity.Parameters,
            Query = entity.Query is null ? null : JsonNode.Parse(entity.Query),
            Fields = entity.Fields,
            SingleRow = entity.SingleRow,
            PageSize = entity.PageSize,
            Function = entity.Function,
            FunctionInputs = entity.FunctionInputs,
            ActsForUser = entity.ActsForUser,
        });

    /// <summary>The path's shape: placeholders count as one (<c>/requests/{number}</c> and <c>/requests/{id}</c> are the same path).</summary>
    internal static string Shape(string path) => PlaceholderPattern().Replace(path.Trim().ToLowerInvariant(), "{}");

    internal static HashSet<string> Placeholders(string path) =>
        [.. PlaceholderPattern().Matches(path).Select(m => m.Groups[1].Value)];

    private static async Task UniquePathAsync(IntegrationRefs refs, string? code, string path, ValidationErrors errors, CancellationToken cancellationToken)
    {
        var shape = Shape(path);
        var taken = (await refs.Context.ApiEndpoints.AsNoTracking().Where(e => e.Code != code).Select(e => new { e.Code, e.Path }).ToListAsync(cancellationToken))
            .FirstOrDefault(e => Shape(e.Path) == shape);
        var pending = refs.Draft?.Items
            .Where(i => i.EntityType == IntegrationChangeTypes.ApiEndpoint && i.EntityCode != code && i.After is not null && i.Operation != ChangeOperation.Delete)
            .Select(i => (i.EntityCode, Path: JsonNode.Parse(i.After!)?["path"]?.GetValue<string>()))
            .FirstOrDefault(i => i.Path is not null && Shape(i.Path) == shape);
        if (taken is not null || pending?.Path is not null)
        {
            errors.Add("path", $"Endpoint {taken?.Code ?? pending?.EntityCode} already answers this path.");
        }
    }

    [GeneratedRegex(@"^(/([A-Za-z0-9_\-]+|\{[A-Za-z][A-Za-z0-9_]{0,63}\}))+$")]
    private static partial Regex PathPattern();

    [GeneratedRegex(@"\{([A-Za-z][A-Za-z0-9_]{0,63})\}")]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{0,63}$")]
    private static partial Regex ParameterName();
}

public sealed class ApiClientPayload : ConfigPayload
{
    public string? ClientId { get; set; }

    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    /// <summary>The service account it acts as; empty: its own (<c>svc-{code}</c>), created with the client.</summary>
    public string? Account { get; set; }

    public int RateLimitPerMinute { get; set; } = 60;

    public string? ContactEmail { get; set; }
}

/// <summary>
/// API clients (ADR-0042): a sign-in client id used by no other client, a service account used by no other client and a
/// rate limit. The account is a service account of Apps Control (or one created in the same change set); when none is
/// named the client gets its own, created when the client is published. What the client may call and see is given to that
/// account as roles, like to any user.
/// </summary>
internal sealed partial class ApiClientApplier(IntegrationDbContext db, IServiceAccounts accounts)
    : IntegrationApplier<ApiClient, ApiClientPayload>(db)
{
    public const int MaxRateLimit = 6000;

    public override string EntityType => IntegrationChangeTypes.ApiClient;

    protected override IQueryable<ApiClient> Query(IntegrationDbContext context) => context.ApiClients;

    protected override async Task ApplyAsync(ApiClient entity, ApiClientPayload payload, IntegrationRefs refs, ValidationErrors errors, ApiClient? existing, CancellationToken cancellationToken)
    {
        var code = entity.Code.Length > 0 ? entity.Code : existing?.Code;
        var clientId = string.IsNullOrWhiteSpace(payload.ClientId) ? DefaultClientId(code ?? string.Empty) : payload.ClientId.Trim();
        if (!ClientIdPattern().IsMatch(clientId))
        {
            errors.Add("clientId", "Use letters, digits, - and _ (up to 100).");
        }
        else if (await refs.Context.ApiClients.AsNoTracking().AnyAsync(c => c.ClientId == clientId && c.Code != code, cancellationToken)
            || OtherPending(refs, code, "clientId", clientId))
        {
            errors.Add("clientId", $"Another API client signs in as {clientId}.");
        }

        var account = string.IsNullOrWhiteSpace(payload.Account) ? DefaultAccount(code ?? string.Empty) : payload.Account.Trim();
        await CheckAccountAsync(refs, code, account, named: !string.IsNullOrWhiteSpace(payload.Account), errors, cancellationToken);

        errors.MaxLength("descriptionAr", payload.DescriptionAr, 2000);
        errors.MaxLength("descriptionEn", payload.DescriptionEn, 2000);
        if (!string.IsNullOrWhiteSpace(payload.ContactEmail) && (payload.ContactEmail.Length > 200 || !payload.ContactEmail.Contains('@', StringComparison.Ordinal)))
        {
            errors.Add("contactEmail", "Give an email address.");
        }

        ConnectionApplier.Range(errors, "rateLimitPerMinute", payload.RateLimitPerMinute, 1, MaxRateLimit);

        entity.ClientId = clientId;
        entity.Account = account;
        entity.DescriptionAr = payload.DescriptionAr;
        entity.DescriptionEn = payload.DescriptionEn;
        entity.RateLimitPerMinute = payload.RateLimitPerMinute;
        entity.ContactEmail = string.IsNullOrWhiteSpace(payload.ContactEmail) ? null : payload.ContactEmail.Trim();
    }

    /// <summary>
    /// The account: a username no other client acts as, that is no person's; a named one must be a service account (or one
    /// created in the same change set), while the client's own is created with it when missing.
    /// </summary>
    private async Task CheckAccountAsync(IntegrationRefs refs, string? code, string account, bool named, ValidationErrors errors, CancellationToken cancellationToken)
    {
        if (!ValidationErrors.UserName().IsMatch(account))
        {
            errors.Add("account", "A username: letters, digits and . _ - @.");
            return;
        }

        if (await refs.Context.ApiClients.AsNoTracking().AnyAsync(c => c.Account == account && c.Code != code, cancellationToken) || OtherPending(refs, code, "account", account))
        {
            errors.Add("account", $"Another API client acts as {account}.");
            return;
        }

        var pending = refs.Draft?.Items.LastOrDefault(i => i.EntityType == UserChangeType && i.EntityCode == account && i.After is not null);
        if (pending is not null)
        {
            if (!string.Equals(JsonNode.Parse(pending.After!)?["kind"]?.GetValue<string>(), "SERVICE", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("account", $"{account} is a person's account: choose a service account.");
            }

            return;
        }

        if (await accounts.IsPersonAsync(account, cancellationToken))
        {
            errors.Add("account", $"{account} is a person's account: choose a service account.");
        }
        else if (named && await accounts.FindAsync(account, cancellationToken) is null)
        {
            errors.Add("account", $"There is no service account {account}: leave it empty to create the client's own.");
        }
    }

    /// <summary>Whether another API client in the change set has the same value of a field.</summary>
    private static bool OtherPending(IntegrationRefs refs, string? code, string field, string value) =>
        refs.Draft?.Items.Any(i => i.EntityType == IntegrationChangeTypes.ApiClient && i.EntityCode != code && i.After is not null
            && JsonNode.Parse(i.After)?[field]?.GetValue<string>() is { } other && string.Equals(other.Trim(), value, StringComparison.Ordinal)) == true;

    /// <summary>The change type of Apps Control users (a service account may be created in the same change set).</summary>
    internal const string UserChangeType = "apps.user";

    /// <summary>The service account a client acts as unless one is named.</summary>
    internal static string DefaultAccount(string code) => "svc-" + code.ToLowerInvariant().Replace('_', '-');

    /// <summary>The client id a new API client signs in as unless one is given.</summary>
    internal static string DefaultClientId(string code) => "dc-api-" + code.ToLowerInvariant().Replace('_', '-');

    protected override Task<ApiClientPayload> ToPayloadAsync(IntegrationDbContext context, ApiClient entity, CancellationToken cancellationToken) =>
        Task.FromResult(new ApiClientPayload
        {
            ClientId = entity.ClientId,
            DescriptionAr = entity.DescriptionAr,
            DescriptionEn = entity.DescriptionEn,
            Account = entity.Account,
            RateLimitPerMinute = entity.RateLimitPerMinute,
            ContactEmail = entity.ContactEmail,
        });

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_\\-]{0,99}$")]
    private static partial Regex ClientIdPattern();
}
