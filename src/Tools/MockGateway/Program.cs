using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

// Development stand-in for the MoMRAH API gateway (apiservicesstg.balady.gov.sa): the OAuth2 token endpoint and the MCI
// operations, answering with the response envelope and codes of the MCI integration guide. Never deploy it.
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var samples = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "samples", "mci.json")))!;
var tokens = new ConcurrentDictionary<string, DateTimeOffset>();

app.MapPost("/oauth/v1/token", (HttpRequest request) =>
{
    var authorization = request.Headers.Authorization.ToString();
    if (!authorization.StartsWith("Basic ", StringComparison.Ordinal) || authorization.Length < 10)
    {
        return Results.Json(Status(401, "API authorization failed"), statusCode: 401);
    }

    var token = Guid.NewGuid().ToString("N");
    tokens[token] = DateTimeOffset.UtcNow.AddSeconds(17999);
    return Results.Json(new { access_token = token, token_type = "Bearer", expires_in = 17999 });
});

var mci = app.MapGroup("/v1/MCI-DIP").AddEndpointFilter(async (context, next) =>
{
    var authorization = context.HttpContext.Request.Headers.Authorization.ToString();
    var token = authorization.StartsWith("Bearer ", StringComparison.Ordinal) ? authorization[7..] : string.Empty;
    return tokens.TryGetValue(token, out var expires) && expires > DateTimeOffset.UtcNow
        ? await next(context)
        : Results.Json(Status(401, "Access Token Expired"), statusCode: 401);
});

mci.MapGet("/cr-information-id-number/{idNumber}", (string idNumber) =>
{
    if (idNumber.Length != 10 || !idNumber.All(char.IsAsciiDigit))
    {
        return Business("001-000008", "Wrong ID Number");
    }

    return samples["byIdNumber"]?[idNumber] is JsonArray licenses
        ? Success(new JsonObject { ["Licenses"] = licenses.DeepClone() })
        : Business("001-000001", "Record Not Found");
});

mci.MapGet("/CR-Information/{crNumber}", (string crNumber) =>
    samples["byCrNumber"]?[crNumber] is JsonObject cr ? Success(cr.DeepClone()) : Business("001-000001", "Record Not Found"));

app.Run();

static object Status(int code, string message) => new { statusDetails = new[] { new { code, message } } };

static IResult Success(JsonNode result) => Results.Json(new JsonObject
{
    ["statusDetails"] = new JsonArray(new JsonObject { ["code"] = 200, ["message"] = "Ok" }),
    ["data"] = new JsonObject { ["responseCode"] = "1", ["responseMessage"] = "success", ["result"] = result },
}, JsonSerializerOptions.Web);

static IResult Business(string code, string message) => Results.Json(new JsonObject
{
    ["statusDetails"] = new JsonArray(new JsonObject { ["code"] = 200, ["message"] = "Ok" }),
    ["data"] = new JsonObject { ["responseCode"] = code, ["responseMessage"] = message },
}, JsonSerializerOptions.Web);
