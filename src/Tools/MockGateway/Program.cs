using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// Development stand-in for the MoMRAH API gateway (apiservicesstg.balady.gov.sa): the OAuth2 token endpoint and the MCI
// operations, answering with the response envelope and codes of the MCI integration guide; and a SADAD stand-in that
// registers bills, offers a payment page and notifies the platform of payments, signed. Never deploy it.
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();
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

// SADAD: bills registered by the platform (Integration Manager operation SADAD_CREATE_BILL), paid on a page of the mock.
var sadad = app.MapGroup("/v1/SADAD").AddEndpointFilter(async (context, next) =>
{
    var authorization = context.HttpContext.Request.Headers.Authorization.ToString();
    var token = authorization.StartsWith("Bearer ", StringComparison.Ordinal) ? authorization[7..] : string.Empty;
    return tokens.TryGetValue(token, out var expires) && expires > DateTimeOffset.UtcNow
        ? await next(context)
        : Results.Json(Status(401, "Access Token Expired"), statusCode: 401);
});
var bills = new ConcurrentDictionary<string, SadadBill>();
var callbackUrl = app.Configuration["Sadad:CallbackUrl"] ?? "http://localhost:5081/api/payments/v1/callbacks/SADAD";
var callbackKey = app.Configuration["Sadad:CallbackKey"] ?? "dev-sadad-callback-key";
var returnUrl = app.Configuration["Sadad:ReturnUrl"] ?? "http://localhost:4201/requests";

sadad.MapPost("/bills", (JsonObject body) =>
{
    if (!decimal.TryParse(body["amount"]?.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
    {
        return Business("002-000001", "Invalid amount");
    }

    var number = "9" + Random.Shared.NextInt64(100_000_000, 999_999_999).ToString(CultureInfo.InvariantCulture);
    bills[number] = new SadadBill(number, body["billNumber"]?.ToString() ?? string.Empty, amount, body["payerName"]?.ToString() ?? string.Empty, body["description"]?.ToString() ?? string.Empty, body["dueDate"]?.ToString() ?? string.Empty);
    return Success(new JsonObject { ["sadadNumber"] = number, ["billerCode"] = "901" });
});

app.MapGet("/sadad/pay/{number}", (string number) => bills.TryGetValue(number, out var bill)
    ? Results.Content(PayPage(bill, null), "text/html; charset=utf-8")
    : Results.Content(Page("الفاتورة غير موجودة", "<p>لا توجد فاتورة سداد بهذا الرقم.</p>"), "text/html; charset=utf-8", statusCode: 404));

app.MapPost("/sadad/pay/{number}", async (string number, IHttpClientFactory clients) =>
{
    if (!bills.TryGetValue(number, out var bill))
    {
        return Results.NotFound();
    }

    if (!bill.Paid)
    {
        // The notification the platform verifies: an HMAC-SHA256 of the raw body with the shared callback key.
        var body = new JsonObject
        {
            ["sadadNumber"] = bill.Number,
            ["status"] = "PAID",
            ["amount"] = bill.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            ["paymentReference"] = "SADAD-" + Random.Shared.NextInt64(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture),
            ["paidAt"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
        }.ToJsonString();
        using var request = new HttpRequestMessage(HttpMethod.Post, callbackUrl) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Signature", Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(callbackKey), Encoding.UTF8.GetBytes(body))));
        HttpResponseMessage response;
        try
        {
            response = await clients.CreateClient().SendAsync(request);
        }
        catch (HttpRequestException ex)
        {
            return Results.Content(PayPage(bill, $"تعذّر إبلاغ المنصة: {WebUtility.HtmlEncode(ex.Message)}"), "text/html; charset=utf-8");
        }

        if (!response.IsSuccessStatusCode)
        {
            return Results.Content(PayPage(bill, $"رفضت المنصة الإشعار ({(int)response.StatusCode})."), "text/html; charset=utf-8");
        }

        bills[number] = bill = bill with { Paid = true };
    }

    return Results.Content(PayPage(bill, null), "text/html; charset=utf-8");
});

app.Run();

string PayPage(SadadBill bill, string? error) => Page(
    bill.Paid ? "تم السداد" : "سداد فاتورة",
    $"""
    <dl>
      <dt>رقم فاتورة سداد</dt><dd dir="ltr">{bill.Number}</dd>
      <dt>رمز المفوتر</dt><dd dir="ltr">901</dd>
      <dt>الوصف</dt><dd>{WebUtility.HtmlEncode(bill.Description)}</dd>
      <dt>المبلغ</dt><dd dir="ltr">{bill.Amount.ToString("N2", CultureInfo.InvariantCulture)} SAR</dd>
      <dt>آخر موعد للسداد</dt><dd dir="ltr">{WebUtility.HtmlEncode(bill.DueDate)}</dd>
    </dl>
    {(error is null ? string.Empty : $"<p class=\"error\">{error}</p>")}
    {(bill.Paid
        ? $"<p class=\"ok\">تم سداد الفاتورة وإبلاغ المنصة.</p><a class=\"button\" href=\"{returnUrl}\">العودة إلى البوابة</a>"
        : "<form method=\"post\"><button class=\"button\" type=\"submit\" data-testid=\"sadad-pay\">ادفع الآن</button></form>")}
    """);

static string Page(string title, string content) => $$"""
    <!doctype html>
    <html lang="ar" dir="rtl"><head><meta charset="utf-8"><title>{{title}} — سداد (بيئة التطوير)</title>
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <style>
      body { margin: 0; font-family: system-ui, sans-serif; background: #f3f4f6; color: #161616; }
      header { background: #0f5132; color: #fff; padding: 16px 24px; font-weight: 700; }
      main { max-width: 32rem; margin: 32px auto; background: #fff; border-radius: 12px; padding: 24px; box-shadow: 0 2px 8px rgb(0 0 0 / 8%); }
      dl { display: grid; grid-template-columns: auto 1fr; gap: 8px 16px; }
      dt { color: #4d5761; } dd { margin: 0; font-weight: 600; }
      .button { display: inline-block; margin-top: 16px; padding: 12px 24px; border: none; border-radius: 8px; background: #1b8354; color: #fff; font: inherit; font-weight: 600; text-decoration: none; cursor: pointer; }
      .ok { color: #1b8354; font-weight: 600; } .error { color: #b42318; }
      footer { text-align: center; color: #6c737f; font-size: 13px; }
    </style></head>
    <body><header>سداد — محاكاة لبيئة التطوير</header><main><h1>{{title}}</h1>{{content}}</main>
    <footer>صفحة تجريبية لا تُستخدم في الإنتاج</footer></body></html>
    """;

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

internal sealed record SadadBill(string Number, string PlatformNumber, decimal Amount, string PayerName, string Description, string DueDate, bool Paid = false);
