using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DynamicCube.MockGateway;

/// <summary>
/// A sample connected app (ADR-0036 step 7) at <c>/connected-app</c>: an outside system with its own pages that signs its
/// users in through the platform's Keycloak (authorization code with PKCE and the client secret) and asks the platform's
/// connected-app API who they are, the menu they may open, their screens and actions, and the values of a list with their
/// grants applied. It shows, in one small file, what a real connected app does. Development only.
/// </summary>
internal static class ConnectedAppSample
{
    private const string Cookie = "dc_sample_session";

    /// <summary>Signed-in sessions (access token by session id) and sign-ins in progress (PKCE verifier by state).</summary>
    private static readonly ConcurrentDictionary<string, string> Sessions = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, string> Pending = new(StringComparer.Ordinal);

    public static void Map(WebApplication app)
    {
        var options = app.Configuration.GetSection("ConnectedApp").Get<SampleOptions>() ?? new SampleOptions();
        var group = app.MapGroup("/connected-app");

        group.MapGet("/", async (HttpContext http, IHttpClientFactory clients, string? company) =>
        {
            if (!http.Request.Cookies.TryGetValue(Cookie, out var session) || !Sessions.TryGetValue(session, out var token))
            {
                return Html(Page(options, null, $"""
                    <p>تطبيق مرتبط تجريبي: نظام خارجي بصفحاته الخاصة يستخدم ديناميك كيوب لتسجيل الدخول والقوائم والصلاحيات وقوائم القيم.</p>
                    <p class="muted">A sample connected app: an outside system that uses Dynamic Cube for sign-in, menus, permissions and lists.</p>
                    <a class="button" href="/connected-app/login" data-testid="sample-sign-in">تسجيل الدخول عبر ديناميك كيوب</a>
                    """));
            }

            var api = clients.CreateClient();
            api.BaseAddress = new Uri(options.ApiUrl.TrimEnd('/') + "/api/connect/v1/");
            api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (!string.IsNullOrEmpty(company))
            {
                api.DefaultRequestHeaders.Add("X-Company", company);
            }

            var me = await GetAsync(api, "me");
            if (me.Status == HttpStatusCode.Unauthorized)
            {
                Sessions.TryRemove(session, out _);
                return Results.Redirect("/connected-app/login");
            }

            if (!me.Ok || me.Json is not JsonObject profile)
            {
                return Html(Page(options, null, Problem("me", me)));
            }

            var body = new StringBuilder();
            body.Append(Workspace(profile));
            if (profile["hasAccess"]?.GetValue<bool>() != true)
            {
                body.Append("""<p class="error" data-testid="sample-no-access">لا تعمل في أي شركة تستخدم هذا التطبيق.</p>""");
                return Html(Page(options, profile, body.ToString()));
            }

            var menu = await GetAsync(api, "menu");
            var privileges = await GetAsync(api, "privileges");
            var list = await GetAsync(api, $"lovs/{Uri.EscapeDataString(options.ListCode)}/values?size=20");
            body.Append("<div class=\"cols\">");
            body.Append("<section><h2>القائمة <small>Menu</small></h2>").Append(menu.Ok && menu.Json is JsonArray groups ? Menu(groups) : Problem("menu", menu)).Append("</section>");
            body.Append("<section><h2>الشاشات والإجراءات <small>Screens and actions</small></h2>").Append(privileges.Ok && privileges.Json is JsonObject p ? Privileges(p) : Problem("privileges", privileges)).Append("</section>");
            body.Append("</div>");
            body.Append("<section><h2>قائمة القيم ").Append(WebUtility.HtmlEncode(options.ListCode)).Append(" <small>with your grants</small></h2>")
                .Append(list.Ok && list.Json is JsonObject values ? Values(values) : Problem("list", list)).Append("</section>");
            return Html(Page(options, profile, body.ToString()));
        });

        group.MapGet("/login", (HttpContext http) =>
        {
            var state = Token(16);
            var verifier = Token(32);
            Pending[state] = verifier;
            var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
            var url = $"{options.Issuer.TrimEnd('/')}/protocol/openid-connect/auth?response_type=code&scope=openid"
                + $"&client_id={Uri.EscapeDataString(options.ClientId)}&redirect_uri={Uri.EscapeDataString(options.RedirectUri)}"
                + $"&state={state}&code_challenge={challenge}&code_challenge_method=S256";
            return Results.Redirect(url);
        });

        group.MapGet("/callback", async (HttpContext http, IHttpClientFactory clients, string? code, string? state, string? error) =>
        {
            if (error is not null || code is null || state is null || !Pending.TryRemove(state, out var verifier))
            {
                return Html(Page(options, null, $"<p class=\"error\">لم يكتمل تسجيل الدخول ({WebUtility.HtmlEncode(error ?? "state")}).</p><a class=\"button\" href=\"/connected-app\">رجوع</a>"));
            }

            // The code is exchanged on the server, with the client secret: the browser never sees the secret or the token.
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = options.RedirectUri,
                ["client_id"] = options.ClientId,
                ["client_secret"] = options.ClientSecret,
                ["code_verifier"] = verifier,
            });
            using var response = await clients.CreateClient().PostAsync(new Uri($"{options.TokenIssuer.TrimEnd('/')}/protocol/openid-connect/token"), form);
            var answer = await response.Content.ReadFromJsonAsync<JsonObject>();
            if (!response.IsSuccessStatusCode || answer?["access_token"]?.GetValue<string>() is not { } token)
            {
                var detail = answer?["error_description"]?.GetValue<string>() ?? answer?["error"]?.GetValue<string>() ?? ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture);
                return Html(Page(options, null, $"<p class=\"error\">رفض خادم الدخول الرمز: {WebUtility.HtmlEncode(detail)}</p><a class=\"button\" href=\"/connected-app\">رجوع</a>"));
            }

            var session = Token(24);
            Sessions[session] = token;
            http.Response.Cookies.Append(Cookie, session, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, Path = "/connected-app" });
            return Results.Redirect("/connected-app");
        });

        group.MapGet("/logout", (HttpContext http) =>
        {
            if (http.Request.Cookies.TryGetValue(Cookie, out var session))
            {
                Sessions.TryRemove(session, out _);
            }

            http.Response.Cookies.Delete(Cookie, new CookieOptions { Path = "/connected-app" });
            var back = Uri.EscapeDataString(options.RedirectUri.Replace("/callback", string.Empty, StringComparison.Ordinal));
            return Results.Redirect($"{options.Issuer.TrimEnd('/')}/protocol/openid-connect/logout?client_id={Uri.EscapeDataString(options.ClientId)}&post_logout_redirect_uri={back}");
        });
    }

    private static string Workspace(JsonObject me)
    {
        var current = me["company"]?["code"]?.GetValue<string>();
        var companies = me["companies"] as JsonArray ?? [];
        var links = string.Join(" ", companies.OfType<JsonObject>().Select(c =>
        {
            var code = c["code"]!.GetValue<string>();
            var name = WebUtility.HtmlEncode(c["nameAr"]?.GetValue<string>() ?? code);
            return code == current ? $"<b>{name}</b>" : $"<a href=\"/connected-app?company={Uri.EscapeDataString(code)}\">{name}</a>";
        }));
        var branches = me["allBranches"]?.GetValue<bool>() == true
            ? "كل الفروع"
            : string.Join("، ", (me["branches"] as JsonArray ?? []).OfType<JsonObject>().Select(b => WebUtility.HtmlEncode(b["nameAr"]?.GetValue<string>() ?? string.Empty)));
        var roles = string.Join(", ", (me["roles"] as JsonArray ?? []).Select(r => WebUtility.HtmlEncode(r?.GetValue<string>() ?? string.Empty)));
        return $"""
            <dl data-testid="sample-me">
              <dt>المستخدم</dt><dd>{WebUtility.HtmlEncode(me["nameAr"]?.GetValue<string>() ?? me["userName"]?.GetValue<string>() ?? string.Empty)} <span class="muted">({WebUtility.HtmlEncode(me["userName"]?.GetValue<string>() ?? string.Empty)})</span></dd>
              <dt>الشركة</dt><dd data-testid="sample-company">{(links.Length > 0 ? links : "—")}</dd>
              <dt>الفروع</dt><dd data-testid="sample-branches">{(branches.Length > 0 ? branches : "—")}</dd>
              <dt>الأدوار</dt><dd class="ltr">{roles}</dd>
            </dl>
            """;
    }

    private static string Menu(JsonArray groups)
    {
        if (groups.Count == 0)
        {
            return "<p class=\"muted\" data-testid=\"sample-menu-empty\">لا توجد عناصر تستطيع فتحها.</p>";
        }

        static string Items(JsonArray items) => "<ul>" + string.Concat(items.OfType<JsonObject>().Select(i =>
            $"<li data-testid=\"sample-menu-{WebUtility.HtmlEncode(i["code"]?.GetValue<string>())}\">{WebUtility.HtmlEncode(i["nameAr"]?.GetValue<string>())}"
            + $" <span class=\"muted ltr\">{WebUtility.HtmlEncode(i["target"]?.GetValue<string>())}</span>"
            + (i["children"] is JsonArray { Count: > 0 } children ? Items(children) : string.Empty) + "</li>")) + "</ul>";

        return "<div data-testid=\"sample-menu\">" + string.Concat(groups.OfType<JsonObject>().Select(g =>
            (string.IsNullOrEmpty(g["nameAr"]?.GetValue<string>()) ? string.Empty : $"<h3>{WebUtility.HtmlEncode(g["nameAr"]!.GetValue<string>())}</h3>")
            + Items(g["items"] as JsonArray ?? []))) + "</div>";
    }

    private static string Privileges(JsonObject privileges) =>
        privileges.Count == 0
            ? "<p class=\"muted\">لا شيء.</p>"
            : "<table data-testid=\"sample-privileges\"><tr><th>الشاشة</th><th>الإجراءات</th></tr>" + string.Concat(privileges.Select(p =>
                $"<tr><td class=\"ltr\">{WebUtility.HtmlEncode(p.Key)}</td><td class=\"ltr\">{WebUtility.HtmlEncode(string.Join(", ", (p.Value as JsonArray ?? []).Select(a => a?.GetValue<string>())))}</td></tr>")) + "</table>";

    private static string Values(JsonObject page)
    {
        var items = page["items"] as JsonArray ?? [];
        return items.Count == 0
            ? "<p class=\"muted\" data-testid=\"sample-values-empty\">لا توجد قيم مسموحة لك.</p>"
            : "<table data-testid=\"sample-values\"><tr><th>القيمة</th><th>الاسم</th><th>الأعلى</th></tr>" + string.Concat(items.OfType<JsonObject>().Select(i =>
                $"<tr><td class=\"ltr\">{WebUtility.HtmlEncode(i["value"]?.GetValue<string>())}</td><td>{WebUtility.HtmlEncode(i["labelAr"]?.GetValue<string>())}</td><td class=\"ltr\">{WebUtility.HtmlEncode(i["parent"]?.GetValue<string>())}</td></tr>")) + "</table>";
    }

    private static string Problem(string what, ApiAnswer answer) =>
        $"<p class=\"error\" data-testid=\"sample-problem-{what}\">{(int)answer.Status}: {WebUtility.HtmlEncode(answer.Json?["detail"]?.GetValue<string>() ?? answer.Text)}</p>";

    private static async Task<ApiAnswer> GetAsync(HttpClient api, string path)
    {
        try
        {
            using var response = await api.GetAsync(new Uri(path, UriKind.Relative));
            var text = await response.Content.ReadAsStringAsync();
            JsonNode? json = null;
            try
            {
                json = text.Length > 0 ? JsonNode.Parse(text) : null;
            }
            catch (JsonException)
            {
            }

            return new ApiAnswer(response.StatusCode, response.IsSuccessStatusCode || json is JsonObject { } o && o.ContainsKey("detail") ? json : null, text);
        }
        catch (HttpRequestException ex)
        {
            return new ApiAnswer(HttpStatusCode.ServiceUnavailable, null, ex.Message);
        }
    }

    private static string Page(SampleOptions options, JsonObject? me, string content) => $$"""
        <!doctype html>
        <html lang="ar" dir="rtl"><head><meta charset="utf-8"><title>{{WebUtility.HtmlEncode(options.Title)}} — تطبيق مرتبط تجريبي</title>
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <style>
          body { margin: 0; font-family: system-ui, sans-serif; background: #f3f4f6; color: #161616; }
          header { display: flex; align-items: center; gap: 12px; background: #1f2a44; color: #fff; padding: 14px 24px; }
          header b { flex: 1; } header a { color: #fff; }
          main { max-width: 64rem; margin: 24px auto; padding: 0 16px; display: grid; gap: 16px; }
          section, dl { background: #fff; border-radius: 12px; padding: 16px 20px; box-shadow: 0 2px 8px rgb(0 0 0 / 6%); margin: 0; }
          .cols { display: grid; grid-template-columns: repeat(auto-fit, minmax(18rem, 1fr)); gap: 16px; }
          dl { display: grid; grid-template-columns: auto 1fr; gap: 8px 16px; } dt { color: #4d5761; } dd { margin: 0; font-weight: 600; }
          h2 { margin: 0 0 8px; font-size: 18px; } h2 small { color: #6c737f; font-weight: 400; font-size: 13px; } h3 { margin: 8px 0 4px; font-size: 15px; }
          table { width: 100%; border-collapse: collapse; } th, td { padding: 6px 8px; border-bottom: 1px solid #e5e7eb; text-align: start; }
          .ltr { direction: ltr; unicode-bidi: isolate; } .muted { color: #6c737f; font-weight: 400; } .error { color: #b42318; }
          .button { display: inline-block; margin-top: 8px; padding: 10px 20px; border-radius: 8px; background: #1f2a44; color: #fff; text-decoration: none; font-weight: 600; }
          footer { text-align: center; color: #6c737f; font-size: 13px; padding: 16px; }
        </style></head>
        <body><header><b>{{WebUtility.HtmlEncode(options.Title)}}</b>
        {{(me is null ? string.Empty : "<a href=\"/connected-app/logout\" data-testid=\"sample-sign-out\">تسجيل الخروج</a>")}}</header>
        <main>{{content}}</main>
        <footer>تطبيق مرتبط تجريبي لبيئة التطوير — client {{WebUtility.HtmlEncode(options.ClientId)}}</footer></body></html>
        """;

    private static IResult Html(string html) => Results.Content(html, "text/html; charset=utf-8");

    private static string Token(int bytes) => Base64Url(RandomNumberGenerator.GetBytes(bytes));

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record ApiAnswer(HttpStatusCode Status, JsonNode? Json, string Text)
    {
        public bool Ok => (int)Status is >= 200 and < 300;
    }

    /// <summary>Configuration section <c>ConnectedApp</c> (development values; a real app keeps its secret in its own secret store).</summary>
    private sealed class SampleOptions
    {
        public string Title { get; init; } = "نظام إدارة المشاريع";

        /// <summary>The realm as browsers see it (sign-in and sign-out pages).</summary>
        public string Issuer { get; init; } = "http://localhost:8080/realms/dynamic-cube-internal";

        /// <summary>The realm as this server reaches it (token endpoint); defaults to <see cref="Issuer"/>.</summary>
        public string? TokenUrl { get; init; }

        public string TokenIssuer => string.IsNullOrEmpty(TokenUrl) ? Issuer : TokenUrl;

        public string ClientId { get; init; } = "dc-app-pms";

        public string ClientSecret { get; init; } = "dev-connected-app-secret";

        public string RedirectUri { get; init; } = "http://localhost:5090/connected-app/callback";

        /// <summary>The platform's internal API, which serves the connected-app API.</summary>
        public string ApiUrl { get; init; } = "http://localhost:5080";

        public string ListCode { get; init; } = "MY_BRANCHES_BY_REGION";
    }
}
