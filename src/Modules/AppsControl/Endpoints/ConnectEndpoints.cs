using DynamicCube.Modules.AppsControl.Access;
using DynamicCube.Modules.AppsControl.Accounts;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.ConnectedApps;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Lovs;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.AppsControl.Endpoints;

/// <summary>A connected app as the app reads its own registration: companies and branches, screens and menu tree.</summary>
public sealed record ConnectApp(
    string Code,
    string NameAr,
    string NameEn,
    IReadOnlyList<string> Companies,
    IReadOnlyList<string> Branches,
    IReadOnlyList<ConnectScreen> Screens,
    IReadOnlyList<MenuGroupNode> Menu);

/// <summary>A screen (or other protected part) of a connected app and the actions it offers.</summary>
public sealed record ConnectScreen(string Code, string NameAr, string NameEn, string Type, string? ParentCode, IReadOnlyList<string> Actions);

public sealed record ConnectPlace(string Code, string NameAr, string NameEn);

/// <summary>
/// The signed-in user in a connected app: who they are, the company they work in now (one at a time, ADR-0036 step 5) and
/// the companies they may choose among those using the app, and the branches they work in there.
/// </summary>
/// <param name="HasAccess">False when none of the user's companies uses the app: the app should not let them in.</param>
/// <param name="AllBranches">True when the user works in every branch of the company that the app serves.</param>
public sealed record ConnectMe(
    string UserName,
    string? NameAr,
    string? NameEn,
    string? Email,
    string? PreferredLanguage,
    bool HasAccess,
    ConnectPlace? Company,
    IReadOnlyList<ConnectPlace> Companies,
    bool AllBranches,
    IReadOnlyList<ConnectPlace> Branches,
    IReadOnlyList<string> Roles,
    string? OrgUnit,
    IReadOnlyList<string> OrgUnits,
    IReadOnlyList<string> Positions);

/// <summary>The app's own registration as resolved from the token (the client it was issued to).</summary>
internal sealed record CallingApp(string Code, string NameAr, string NameEn, string? EntityCode, IReadOnlyList<string> Companies, IReadOnlyList<string> Branches, bool ForUser)
{
    public bool Serves(string company) => Companies.Contains(CompanyScope.All) || Companies.Contains(company);
}

/// <summary>
/// The connected-app API (ADR-0036 step 7): what an outside system that uses the platform as its provider asks about its
/// signed-in users — profile and workspace, its menu tree and the screens and actions they may use, and lists of values
/// with their grants applied. The app is the one the token was issued to (its <c>azp</c>), never a parameter, so one app
/// cannot read another's menus or permissions.
/// </summary>
internal static class ConnectEndpoints
{
    private const string AppKey = "dc.connected-app";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var connect = endpoints.MapGroup(ConnectedAppService.ApiPath)
            .RequireAuthorization(ConnectedAppAuthorization.Policy)
            .AddEndpointFilter(App)
            .WithTags("Connected apps");

        // The app's registration; also with the app's own (client credentials) token.
        connect.MapGet("/app", async (HttpContext http, AppsControlDbContext db, MenuService menus, CancellationToken ct) =>
        {
            var app = Calling(http);
            var rows = await db.Resources.AsNoTracking()
                .Where(r => r.BusinessApp == app.Code && r.IsActive)
                .OrderBy(r => r.Code)
                .Select(r => new { r.Code, r.NameAr, r.NameEn, r.ResourceType, ParentCode = r.Parent != null ? r.Parent.Code : null, r.AvailableActions })
                .ToListAsync(ct);
            var screens = rows.Select(r => new ConnectScreen(r.Code, r.NameAr, r.NameEn, r.ResourceType.ToString().ToUpperInvariant(), r.ParentCode, r.AvailableActions)).ToList();
            return TypedResults.Ok(new ConnectApp(app.Code, app.NameAr, app.NameEn, app.Companies, app.Branches, screens, await menus.GetConnectedMenuAsync(app.Code, null, ct)));
        }).WithName("GetConnectedApp");

        var user = connect.MapGroup(string.Empty).AddEndpointFilter(SignedInUser);

        user.MapGet("/me", async (HttpContext http, AppsControlDbContext db, CancellationToken ct) =>
        {
            var app = Calling(http);
            var access = Access(http);
            var companies = await PlacesAsync(db.Companies, access.Companies.Where(app.Serves).ToList(), ct);
            var company = access.Company is { } code && app.Serves(code) ? companies.FirstOrDefault(c => c.Code == code) : null;
            var (allBranches, branches) = company is null ? (false, []) : await BranchesAsync(db, app, access, company.Code, ct);
            var email = access.User.UserId is { } id ? await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.Email).SingleOrDefaultAsync(ct) : null;
            var u = access.User;
            return TypedResults.Ok(new ConnectMe(
                u.UserName, access.NameAr, access.NameEn, email, access.PreferredLanguage, company is not null, company, companies, allBranches, branches,
                u.Roles, u.PrimaryOrgUnit, u.OrgUnits, u.Positions));
        }).WithName("GetConnectedMe");

        var working = user.MapGroup(string.Empty).AddEndpointFilter(WorksInApp);

        working.MapGet("/menu", async (HttpContext http, MenuService menus, CancellationToken ct) =>
            TypedResults.Ok(await menus.GetConnectedMenuAsync(Calling(http).Code, Access(http), ct))).WithName("GetConnectedMenu");

        working.MapGet("/privileges", async (HttpContext http, AppsControlDbContext db, CancellationToken ct) =>
        {
            var app = Calling(http);
            var access = Access(http);
            var codes = await db.Resources.AsNoTracking().Where(r => r.BusinessApp == app.Code).Select(r => r.Code).ToListAsync(ct);
            return TypedResults.Ok(access.Privileges
                .Where(p => codes.Contains(p.Key))
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .ToDictionary(p => p.Key, p => p.Value.Order(StringComparer.Ordinal).ToArray()));
        }).WithName("GetConnectedPrivileges");

        working.MapGet("/lovs/{code}/values", async Task<IResult> (string code, string? parent, string? q, int? page, int? size, string? resource, HttpContext http, AppsControlDbContext db, LovService lovs, CancellationToken ct) =>
        {
            if (await RefuseListAsync(db, Calling(http), code, resource, ct) is { } refused)
            {
                return refused;
            }

            return TypedResults.Ok(await lovs.GetValuesAsync(code, Access(http), new LovQuery(parent, q, page ?? 1, size ?? 0, resource), publicOnly: false, ct));
        }).WithName("GetConnectedLovValues").AddEndpointFilter(AppsControlFilters.Errors);

        working.MapGet("/lovs/{code}/grant", async Task<IResult> (string code, string? resource, HttpContext http, AppsControlDbContext db, GrantResolver grants, CancellationToken ct) =>
        {
            if (await RefuseListAsync(db, Calling(http), code, resource, ct) is { } refused)
            {
                return refused;
            }

            var definition = await db.Lovs.AsNoTracking().SingleAsync(l => l.Code == code, ct);
            var grant = await grants.ResolveAsync(definition, Access(http), resource, ct);
            return TypedResults.Ok(new GrantInfo(definition.Code, grant.All, grant.Values.Order(StringComparer.Ordinal).ToList(), grant.DefaultValue));
        }).WithName("GetConnectedLovGrant");
    }

    /// <summary>Resolves the app from the client the token was issued to; an unknown or inactive app is refused.</summary>
    private static async ValueTask<object?> App(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var clientId = http.User.FindFirst("azp")?.Value;
        if (string.IsNullOrEmpty(clientId))
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: "The token names no sign-in client (azp).");
        }

        var db = http.RequestServices.GetRequiredService<AppsControlDbContext>();
        var app = await db.BusinessApps.AsNoTracking().SingleOrDefaultAsync(a => a.ClientId == clientId && a.Kind == AppKind.Connected, http.RequestAborted);
        if (app is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: $"Sign-in client {clientId} is not a connected app registered in Apps Control.");
        }

        if (!app.IsActive)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: $"Connected app {app.Code} is not active.");
        }

        // A client-credentials token is the app's own (Keycloak names its user service-account-<client>).
        var forUser = !string.Equals(http.User.FindFirst("preferred_username")?.Value, "service-account-" + clientId, StringComparison.Ordinal);
        http.Items[AppKey] = new CallingApp(app.Code, app.NameAr, app.NameEn, app.EntityCode, app.Companies, app.Branches, forUser);
        return await next(context);
    }

    /// <summary>
    /// User calls need a signed-in, active user. Their access is computed for the company the request names (header
    /// <c>X-Company</c>), else the one they last chose, else the first of their companies that uses the app.
    /// </summary>
    private static async ValueTask<object?> SignedInUser(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var app = Calling(http);
        if (!app.ForUser)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: "This call is about a signed-in user; the app's own token cannot make it.");
        }

        var resolver = http.RequestServices.GetRequiredService<AccessResolver>();
        var access = await resolver.GetAsync(http.RequestAborted);
        if (!access.IsActive)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: "The account is not active.");
        }

        var requested = http.RequestServices.GetRequiredService<ICurrentUser>().RequestedCompany;
        if (requested is not null && (!access.Companies.Contains(requested, StringComparer.Ordinal) || !app.Serves(requested)))
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: $"You do not work in company {requested} in app {app.Code}.");
        }

        if (requested is null && access.Company is { } current && !app.Serves(current) && access.Companies.FirstOrDefault(app.Serves) is { } first)
        {
            access = await resolver.GetInCompanyAsync(first, http.RequestAborted);
        }

        http.Items[typeof(AccessSnapshot)] = access;
        return await next(context);
    }

    /// <summary>Menus, permissions and lists need a company of the user that uses the app.</summary>
    private static async ValueTask<object?> WorksInApp(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var app = Calling(context.HttpContext);
        var access = Access(context.HttpContext);
        return access.Company is { } company && app.Serves(company)
            ? await next(context)
            : Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: $"None of your companies uses app {app.Code}.");
    }

    /// <summary>A list the app may read: one of its own, one shared with it, or one of the whole organization; on its own screens.</summary>
    private static async Task<IResult?> RefuseListAsync(AppsControlDbContext db, CallingApp app, string code, string? resource, CancellationToken cancellationToken)
    {
        var lov = await db.Lovs.AsNoTracking().Where(l => l.Code == code && l.IsActive).Select(l => new { l.BusinessApp, l.SharedApps, l.EntityCode }).SingleOrDefaultAsync(cancellationToken);
        if (lov is null || (lov.EntityCode is not null && app.EntityCode is not null && lov.EntityCode != app.EntityCode)
            || (lov.BusinessApp is not null && lov.BusinessApp != app.Code && !lov.SharedApps.Contains(app.Code)))
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: $"App {app.Code} has no list of values {code}.");
        }

        if (resource is not null && !await db.Resources.AsNoTracking().AnyAsync(r => r.Code == resource && r.BusinessApp == app.Code, cancellationToken))
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: $"{resource} is not a screen of app {app.Code}.");
        }

        return null;
    }

    private static async Task<(bool All, IReadOnlyList<ConnectPlace> Branches)> BranchesAsync(
        AppsControlDbContext db, CallingApp app, AccessSnapshot access, string company, CancellationToken cancellationToken)
    {
        var rows = await db.Branches.AsNoTracking()
            .Where(b => b.CompanyCode == company && b.IsActive)
            .OrderBy(b => b.DisplayOrder).ThenBy(b => b.Code)
            .Select(b => new ConnectPlace(b.Code, b.NameAr, b.NameEn))
            .ToListAsync(cancellationToken);
        var served = CompanyScope.IsAll(app.Branches) ? rows : rows.Where(b => app.Branches.Contains(b.Code)).ToList();
        var mine = access.Branches is null ? served : served.Where(b => access.Branches.Contains(b.Code)).ToList();
        return (access.Branches is null && CompanyScope.IsAll(app.Branches), mine);
    }

    private static async Task<List<ConnectPlace>> PlacesAsync(IQueryable<Company> companies, List<string> codes, CancellationToken cancellationToken)
    {
        var rows = await companies.AsNoTracking().Where(c => codes.Contains(c.Code)).Select(c => new ConnectPlace(c.Code, c.NameAr, c.NameEn)).ToListAsync(cancellationToken);
        return [.. codes.Select(code => rows.First(r => r.Code == code))];
    }

    private static CallingApp Calling(HttpContext http) => (CallingApp)http.Items[AppKey]!;

    private static AccessSnapshot Access(HttpContext http) => (AccessSnapshot)http.Items[typeof(AccessSnapshot)]!;
}

/// <summary>Admin actions on a connected app's sign-in client (the Apps screen). Guarded by the Installation resource.</summary>
internal static class ConnectedAppEndpoints
{
    public static void MapAdmin(RouteGroupBuilder admin)
    {
        var apps = admin.MapGroup("/connected-apps/{code}").AddEndpointFilter(Errors);
        apps.MapGet("/sign-in", (string code, ConnectedAppService service, CancellationToken ct) => service.GetAsync(code, ct))
            .RequirePrivilege(AppsControlResources.Installation, PrivilegeActions.View).WithName("GetConnectedAppSignIn");
        apps.MapPost("/sign-in", (string code, ConnectedAppService service, CancellationToken ct) => service.SetUpAsync(code, ct))
            .RequirePrivilege(AppsControlResources.Installation, PrivilegeActions.Edit).WithName("SetUpConnectedAppSignIn");
        apps.MapPost("/secret", (string code, ConnectedAppService service, CancellationToken ct) => service.NewSecretAsync(code, ct))
            .RequirePrivilege(AppsControlResources.Installation, PrivilegeActions.Edit).WithName("ReplaceConnectedAppSecret");
    }

    private static async ValueTask<object?> Errors(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (AccountException ex)
        {
            return ex.Problem switch
            {
                AccountProblem.Invalid => Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Field ?? "app"] = [ex.Message] }),
                AccountProblem.NotFound => Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message),
                AccountProblem.Unavailable => Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, detail: ex.Message),
                _ => Results.Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message),
            };
        }
    }
}
