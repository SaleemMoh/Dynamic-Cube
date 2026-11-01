using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DynamicCube.MockGateway;

/// <summary>
/// A Nafath stand-in (ADR-0043) at <c>/nafath</c>, answering like Elm's Rabet API: web sign-in sessions
/// (<c>/api/v2/oidc/session</c>, <c>/api/v2/oidc/jwt</c>) with a page standing in for Nafath's sign-in page, requests in
/// the app (<c>/api/v1/mfa/request</c>, <c>/status</c>) with a page standing in for the Nafath app, and the keys answers
/// are signed with (<c>/api/v1/mfa/jwk</c>). Calls need the development APP-ID and APP-KEY. Development only.
/// </summary>
internal static class NafathMock
{
    private static readonly RSA Rsa = RSA.Create(2048);
    private static readonly RsaSecurityKey Key = new(Rsa) { KeyId = "nafath-mock-1" };
    private static readonly ConcurrentDictionary<string, WebSession> Sessions = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, AppRequest> Requests = new(StringComparer.Ordinal);
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(200);

    public static void Map(WebApplication app, JsonNode people)
    {
        var config = app.Configuration.GetSection("Nafath");
        var appId = config["AppId"] ?? "dev-nafath-app";
        var appKey = config["AppKey"] ?? "dev-nafath-key";
        var publicUrl = (config["PublicUrl"] ?? "http://localhost:5090").TrimEnd('/');
        var webCallback = config["WebCallbackUrl"] ?? "http://localhost:5081/api/nafath/v1/oidc/callback";
        var mfaCallback = config["MfaCallbackUrl"] ?? "http://localhost:5081/api/nafath/v1/callbacks/mfa";

        var api = app.MapGroup("/nafath/api").AddEndpointFilter(async (context, next) =>
        {
            var headers = context.HttpContext.Request.Headers;
            return headers["APP-ID"] == appId && headers["APP-KEY"] == appKey
                ? await next(context)
                : Results.Json(new { code = "403-000-001", message = "Invalid APP-ID or APP-KEY" }, statusCode: 403);
        });

        api.MapGet("/v1/mfa/jwk", () =>
        {
            var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(Rsa.ExportParameters(false)) { KeyId = Key.KeyId });
            return Results.Json(new { keys = new[] { new { kty = jwk.Kty, kid = Key.KeyId, use = "sig", alg = "RS256", n = jwk.N, e = jwk.E } } });
        });

        // Web sign-in: a session whose page carries the state; the state redeems the signed answer once the person approved.
        api.MapGet("/v2/oidc/session", (string? requestId, string? locale) =>
        {
            var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var hashed = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(state)));
            Sessions[state] = new WebSession(requestId ?? string.Empty, DateTimeOffset.UtcNow.Add(Lifetime));
            return Results.Json(new { url = $"{publicUrl}/nafath/portal?state={Uri.EscapeDataString(state)}&aud=dynamic-cube-mock", hashedState = hashed, requestId });
        });

        api.MapPost("/v2/oidc/jwt", (JsonObject body) =>
        {
            var state = body["state"]?.GetValue<string>() ?? string.Empty;
            if (!Sessions.TryGetValue(state, out var session) || session.ExpiresAt < DateTimeOffset.UtcNow)
            {
                return Results.Json(new { code = "400-034-053", message = "Request not found" }, statusCode: 400);
            }

            if (session.Rejected)
            {
                return Results.Json(new { code = "400-034-052", message = "Rejected by the user" }, statusCode: 400);
            }

            if (session.NationalId is null)
            {
                return Results.Json(new { code = "401-033-024", message = "Waiting for the user" }, statusCode: 401);
            }

            Sessions.TryRemove(state, out _);
            return Results.Json(new { token = Token(people, session.NationalId, session.RequestId) });
        });

        // Requests in the app: one waiting request per person; the number is picked in the app.
        api.MapPost("/v1/mfa/request", (JsonObject body, string? requestId) =>
        {
            var nationalId = body["nationalId"]?.GetValue<string>() ?? string.Empty;
            if (nationalId.Length != 10 || !nationalId.All(char.IsAsciiDigit))
            {
                return Results.Json(new { code = "400-034-010", message = "Invalid national ID" }, statusCode: 400);
            }

            if (Requests.Values.Any(r => r.NationalId == nationalId && r.Status == "WAITING" && r.ExpiresAt > DateTimeOffset.UtcNow))
            {
                return Results.Json(new { code = "400-034-050", message = "There is an active transaction for this user" }, statusCode: 400);
            }

            var request = new AppRequest(Guid.NewGuid().ToString(), nationalId, Random.Shared.Next(10, 100).ToString(CultureInfo.InvariantCulture),
                body["service"]?.GetValue<string>() ?? "Login", requestId ?? string.Empty, DateTimeOffset.UtcNow.Add(Lifetime));
            Requests[request.TransId] = request;
            return Results.Json(new { transId = request.TransId, random = request.Random });
        });

        api.MapPost("/v1/mfa/request/status", (JsonObject body) =>
        {
            var transId = body["transId"]?.GetValue<string>() ?? string.Empty;
            if (!Requests.TryGetValue(transId, out var request) || request.NationalId != body["nationalId"]?.GetValue<string>())
            {
                return Results.Json(new { code = "400-034-053", message = "Request not found" }, statusCode: 400);
            }

            var status = request.Status == "WAITING" && request.ExpiresAt < DateTimeOffset.UtcNow ? "EXPIRED" : request.Status;
            return Results.Json(new { status });
        });

        // Stands in for Nafath's sign-in page: the person is identified, approves (as in the app) and is sent back.
        app.MapGet("/nafath/portal", (string? state) => Sessions.ContainsKey(state ?? string.Empty)
            ? Html(PortalPage(state!, null))
            : Html(Page("انتهت الجلسة", "<p>جلسة الدخول غير موجودة أو انتهت.</p>"), 404));

        app.MapPost("/nafath/portal", async (HttpRequest request) =>
        {
            var form = await request.ReadFormAsync();
            var state = form["state"].ToString();
            if (!Sessions.TryGetValue(state, out var session))
            {
                return Html(Page("انتهت الجلسة", "<p>جلسة الدخول غير موجودة أو انتهت.</p>"), 404);
            }

            if (form["decision"] == "reject")
            {
                Sessions[state] = session with { Rejected = true };
                return Results.Redirect($"{webCallback}?state={Uri.EscapeDataString(state)}");
            }

            var nationalId = form["nationalId"].ToString().Trim();
            if (nationalId.Length != 10 || !nationalId.All(char.IsAsciiDigit))
            {
                return Html(PortalPage(state, "أدخل رقم هوية من عشرة أرقام."));
            }

            Sessions[state] = session with { NationalId = nationalId };
            return Results.Redirect($"{webCallback}?state={Uri.EscapeDataString(state)}");
        }).DisableAntiforgery();

        // Stands in for the Nafath app: the waiting requests, each answered by picking a number.
        app.MapGet("/nafath/app", (string? id) => Html(AppPage(id, null)));

        app.MapPost("/nafath/app", async (HttpRequest http, IHttpClientFactory clients) =>
        {
            var form = await http.ReadFormAsync();
            var id = form["id"].ToString();
            if (!Requests.TryGetValue(form["transId"].ToString(), out var request) || request.Status != "WAITING")
            {
                return Html(AppPage(id, "الطلب غير موجود أو انتهى."));
            }

            if (form["choice"] != request.Random)
            {
                Requests[request.TransId] = request with { Status = "REJECTED" };
                return Html(AppPage(id, form["choice"] == "reject" ? "رُفض الطلب." : "اخترت رقمًا غير الظاهر في الموقع: رُفض الطلب."));
            }

            Requests[request.TransId] = request with { Status = "COMPLETED" };
            try
            {
                // Nafath posts the signed answer to the service provider's callback.
                using var response = await clients.CreateClient().PostAsJsonAsync(mfaCallback, new
                {
                    token = Token(people, request.NationalId, request.RequestId, request.TransId),
                    transId = request.TransId,
                    requestId = request.RequestId,
                });
                return Html(AppPage(id, response.IsSuccessStatusCode ? "تمت الموافقة وأُبلغت الجهة." : $"تمت الموافقة، وردّت الجهة {(int)response.StatusCode}."));
            }
            catch (HttpRequestException ex)
            {
                return Html(AppPage(id, $"تمت الموافقة، وتعذّر إبلاغ الجهة: {WebUtility.HtmlEncode(ex.Message)}"));
            }
        }).DisableAntiforgery();
    }

    /// <summary>The signed answer: the person as Nafath describes them (names from the NIC sample when known).</summary>
    private static string Token(JsonNode people, string nationalId, string requestId, string? transId = null)
    {
        var person = people["byIdNumber"]?[nationalId] as JsonObject;
        var arabic = (person?["nameAr"]?.GetValue<string>() ?? "مستخدم تجريبي").Replace(" بن ", " ", StringComparison.Ordinal).Replace(" بنت ", " ", StringComparison.Ordinal)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var english = (person?["nameEn"]?.GetValue<string>() ?? "Test User").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var claims = new Dictionary<string, object>
        {
            ["nin"] = nationalId,
            ["firstName"] = arabic[0],
            ["familyName"] = arabic[^1],
            ["englishFirstName"] = english[0],
            ["englishLastName"] = english[^1],
            ["nationality"] = person?["nationality"]?["code"]?.GetValue<string>() ?? "SA",
            ["gender"] = person?["gender"]?["code"]?.GetValue<string>() ?? "M",
            ["dateOfBirthG"] = person?["birthDate"]?.GetValue<string>() ?? "1990-01-01",
            ["requestId"] = requestId,
            ["status"] = "COMPLETED",
        };
        if (arabic.Length > 2)
        {
            claims["fatherName"] = string.Join(' ', arabic[1..^1]);
        }

        if (transId is not null)
        {
            claims["transId"] = transId;
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "nafath-mock",
            Audience = "dynamic-cube-mock",
            Claims = claims,
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.RsaSha256),
        });
    }

    private static string PortalPage(string state, string? error) => Page("الدخول عبر نفاذ", $"""
        <p>تمثل هذه الصفحة صفحة نفاذ في بيئة التطوير: يُدخل الشخص رقم هويته ويوافق في تطبيق نفاذ، ثم يعود إلى الجهة.</p>
        <p class="muted">Stands in for Nafath's sign-in page: enter an ID, approve, and return to the service.</p>
        {(error is null ? string.Empty : $"<p class=\"error\">{WebUtility.HtmlEncode(error)}</p>")}
        <form method="post">
          <input type="hidden" name="state" value="{WebUtility.HtmlEncode(state)}">
          <label>رقم الهوية <input name="nationalId" value="1000000001" inputmode="numeric" maxlength="10" data-testid="nafath-id"></label>
          <div class="row">
            <button class="button" name="decision" value="approve" data-testid="nafath-approve">موافقة في تطبيق نفاذ</button>
            <button class="button secondary" name="decision" value="reject" data-testid="nafath-reject">رفض</button>
          </div>
        </form>
        """);

    private static string AppPage(string? id, string? message)
    {
        var waiting = Requests.Values
            .Where(r => r.Status == "WAITING" && r.ExpiresAt > DateTimeOffset.UtcNow && (string.IsNullOrEmpty(id) || r.NationalId == id))
            .OrderByDescending(r => r.ExpiresAt).ToList();
        var rows = new StringBuilder();
        foreach (var r in waiting)
        {
            var choices = new[] { r.Random, ((int.Parse(r.Random, CultureInfo.InvariantCulture) + 37) % 90 + 10).ToString(CultureInfo.InvariantCulture), ((int.Parse(r.Random, CultureInfo.InvariantCulture) + 61) % 90 + 10).ToString(CultureInfo.InvariantCulture) }
                .Distinct().OrderBy(_ => Random.Shared.Next()).ToList();
            rows.Append(CultureInfo.InvariantCulture, $"""
                <form method="post" class="request" data-testid="nafath-request-{r.NationalId}">
                  <input type="hidden" name="transId" value="{r.TransId}"><input type="hidden" name="id" value="{WebUtility.HtmlEncode(id ?? string.Empty)}">
                  <p><b dir="ltr">{r.NationalId}</b> · {WebUtility.HtmlEncode(r.Service)} · ينتهي {r.ExpiresAt.ToLocalTime():HH:mm:ss}</p>
                  <p>اختر الرقم الظاهر في موقع الجهة:</p>
                  <div class="row">{string.Concat(choices.Select(c => $"<button class=\"button\" name=\"choice\" value=\"{c}\" data-testid=\"nafath-choice-{c}\">{c}</button>"))}
                  <button class="button secondary" name="choice" value="reject">رفض</button></div>
                </form>
                """);
        }

        return Page("تطبيق نفاذ (محاكاة)", $"""
            <p>تمثل هذه الصفحة تطبيق نفاذ في الجوال: الطلبات المنتظرة، ويُجاب كل منها باختيار الرقم الظاهر في موقع الجهة.</p>
            <p class="muted">Stands in for the Nafath app: pick the number the service shows to approve.</p>
            <form method="get" class="row"><input name="id" value="{WebUtility.HtmlEncode(id ?? string.Empty)}" placeholder="رقم الهوية" inputmode="numeric"><button class="button secondary">تحديث</button></form>
            {(message is null ? string.Empty : $"<p class=\"ok\" data-testid=\"nafath-message\">{WebUtility.HtmlEncode(message)}</p>")}
            {(waiting.Count == 0 ? "<p class=\"muted\">لا طلبات منتظرة.</p>" : rows.ToString())}
            """);
    }

    private static string Page(string title, string content) => $$"""
        <!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8"><title>{{title}} — محاكاة نفاذ</title>
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <style>
          body { margin: 0; font-family: system-ui, sans-serif; background: #f3f4f6; color: #161616; }
          header { background: #0b7a5b; color: #fff; padding: 16px 24px; font-weight: 700; }
          main { max-width: 32rem; margin: 32px auto; background: #fff; border-radius: 12px; padding: 24px; box-shadow: 0 2px 8px rgb(0 0 0 / 8%); display: grid; gap: 12px; }
          label { display: grid; gap: 6px; } input { padding: 10px; border: 1px solid #d2d6db; border-radius: 8px; font: inherit; }
          .row { display: flex; gap: 8px; flex-wrap: wrap; align-items: center; }
          .button { padding: 10px 20px; border: none; border-radius: 8px; background: #0b7a5b; color: #fff; font: inherit; font-weight: 600; cursor: pointer; }
          .secondary { background: #e5e7eb; color: #161616; } .request { padding: 12px; border: 1px solid #e5e7eb; border-radius: 8px; }
          .ok { color: #0b7a5b; font-weight: 600; } .error { color: #b42318; } .muted { color: #6c737f; font-size: 13px; } p { margin: 0; }
        </style></head><body><header>نفاذ — محاكاة (بيئة التطوير)</header><main><h1>{{title}}</h1>{{content}}</main></body></html>
        """;

    private static IResult Html(string html, int status = 200) => Results.Content(html, "text/html; charset=utf-8", Encoding.UTF8, status);

    private sealed record WebSession(string RequestId, DateTimeOffset ExpiresAt, string? NationalId = null, bool Rejected = false);

    private sealed record AppRequest(string TransId, string NationalId, string Random, string Service, string RequestId, DateTimeOffset ExpiresAt, string Status = "WAITING");
}
