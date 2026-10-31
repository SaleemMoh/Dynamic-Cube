using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Access;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Lovs;
using DynamicCube.Modules.AppsControl.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Endpoints;

public sealed record MeInfo(
    string UserName,
    string? NameAr,
    string? NameEn,
    string? PreferredLanguage,
    string App,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> OrgUnits,
    string? OrgUnit,
    IReadOnlyList<string> Positions,
    string? EntityCode = null,
    IReadOnlyCollection<string>? BusinessApps = null,
    string? Company = null);

/// <summary>A company the user may work in, with its organization, for the workspace switcher.</summary>
public sealed record WorkspaceCompany(string Code, string NameAr, string NameEn, string? EntityCode, string? EntityNameAr, string? EntityNameEn);

/// <summary>
/// The user's workspace (ADR-0036 step 5): the company they work in now, whether they still have to choose one (several are
/// open and none was chosen), and the companies they may work in.
/// </summary>
public sealed record WorkspaceInfo(string? Company, bool Chosen, IReadOnlyList<WorkspaceCompany> Companies);

public sealed record WorkspaceChoice(string? Company);

/// <summary>A business app the user may work in, for the app selector.</summary>
public sealed record MyBusinessApp(string Code, string NameAr, string NameEn, string? EntityCode, string? Icon, int DisplayOrder);

public sealed record GrantInfo(string Lov, bool All, IReadOnlyCollection<string> Values, string? DefaultValue);

public sealed record DomainItem(string Code, string NameAr, string NameEn, string? ParentCode, int DisplayOrder, bool IsDefault, JsonObject Attributes);

/// <summary>The signed-in employee's own view of Apps Control: profile, apps, menu, privileges and grants.</summary>
internal static class MeEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        var me = group.MapGroup("/me");

        me.MapGet("/", async (AccessResolver resolver, CancellationToken ct) =>
        {
            var access = await resolver.GetAsync(ct);
            var user = access.User;
            return TypedResults.Ok(new MeInfo(
                user.UserName, access.NameAr, access.NameEn, access.PreferredLanguage, user.App, user.Roles, user.OrgUnits, user.PrimaryOrgUnit, user.Positions,
                access.Scope.Entities?.SingleOrDefault(), access.Scope.Apps?.Order(StringComparer.Ordinal).ToList(), access.Company));
        }).WithName("GetMyProfile");

        me.MapGet("/workspaces", async (AccessResolver resolver, AppsControlDbContext db, CancellationToken ct) =>
            TypedResults.Ok(await WorkspaceAsync(await resolver.GetAsync(ct), db, ct))).WithName("GetMyWorkspaces");

        // Remembers the company the user works in; the request header still decides per browser tab.
        me.MapPut("/workspace", async Task<IResult> (WorkspaceChoice choice, AccessResolver resolver, AccessCache accessCache, AppsControlDbContext db, CancellationToken ct) =>
        {
            var access = await resolver.GetAsync(ct);
            if (access.User.UserId is not { } userId)
            {
                return Results.Forbid();
            }

            if (choice.Company is null || !access.Companies.Contains(choice.Company, StringComparer.Ordinal))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["company"] = ["Choose one of the companies you may work in."] });
            }

            await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(u => u.SetProperty(x => x.LastCompanyCode, choice.Company), ct);
            accessCache.Invalidate();
            var companies = await WorkspaceAsync(access, db, ct);
            return TypedResults.Ok(companies with { Company = choice.Company, Chosen = true });
        }).WithName("ChooseMyWorkspace");

        me.MapGet("/business-apps", async (AccessResolver resolver, AppsControlDbContext db, CancellationToken ct) =>
        {
            var access = await resolver.GetAsync(ct);
            var scope = access.Scope;
            var apps = await db.BusinessApps.AsNoTracking().Where(a => a.IsActive).OrderBy(a => a.DisplayOrder).ThenBy(a => a.Code).ToListAsync(ct);
            return TypedResults.Ok(apps
                .Where(a => scope.CanSee(a.EntityCode, a.Code)
                    && (access.Company is null || a.Companies.Contains(CompanyScope.All) || a.Companies.Contains(access.Company)))
                .Select(a => new MyBusinessApp(a.Code, a.NameAr, a.NameEn, a.EntityCode, a.Icon, a.DisplayOrder))
                .ToList());
        }).WithName("GetMyBusinessApps");

        me.MapGet("/apps", async (AccessResolver resolver, MenuService menus, CancellationToken ct) =>
            TypedResults.Ok(await menus.GetAppsAsync(await resolver.GetAsync(ct), ct))).WithName("GetMyApps");

        me.MapGet("/menu", async (string? app, AccessResolver resolver, MenuService menus, CancellationToken ct) =>
        {
            var access = await resolver.GetAsync(ct);
            return TypedResults.Ok(await menus.GetMenuAsync(app ?? access.User.App, access, ct));
        }).WithName("GetMyMenu");

        me.MapGet("/privileges", async (AccessResolver resolver, CancellationToken ct) =>
        {
            var access = await resolver.GetAsync(ct);
            return TypedResults.Ok(access.Privileges.ToDictionary(p => p.Key, p => p.Value.Order().ToArray()));
        }).WithName("GetMyPrivileges");

        me.MapGet("/grants", async (string lov, string? resource, AccessResolver resolver, GrantResolver grants, AppsControlDbContext db, CancellationToken ct) =>
        {
            var definition = await db.Lovs.AsNoTracking().SingleOrDefaultAsync(l => l.Code == lov, ct) ?? throw new LovNotFoundException(lov);
            var grant = await grants.ResolveAsync(definition, await resolver.GetAsync(ct), resource, ct);
            return TypedResults.Ok(new GrantInfo(definition.Code, grant.All, grant.Values.Order().ToList(), grant.DefaultValue));
        }).WithName("GetMyGrant").AddEndpointFilter(AppsControlFilters.Errors);

        group.MapGet("/classes/{code}/domains", async (string code, string? parent, AccessResolver resolver, AppsControlDbContext db, CancellationToken ct) =>
        {
            // Values of the company the user works in (none on the portal: every value).
            var company = (await resolver.GetAsync(ct)).Company;
            var rows = await LovService.ForCompany(db.Domains.AsNoTracking(), company)
                .Where(d => d.Class!.Code == code && d.IsActive && (parent == null || d.ParentDomain!.Code == parent))
                .OrderBy(d => d.DisplayOrder).ThenBy(d => d.Code)
                .Select(d => new { d.Code, d.NameAr, d.NameEn, ParentCode = d.ParentDomain != null ? d.ParentDomain.Code : null, d.DisplayOrder, d.IsDefault, d.Attributes })
                .ToListAsync(ct);
            return TypedResults.Ok(rows.Select(d => new DomainItem(d.Code, d.NameAr, d.NameEn, d.ParentCode, d.DisplayOrder, d.IsDefault, JsonNode.Parse(d.Attributes) as JsonObject ?? [])).ToList());
        }).WithName("GetClassDomains");
    }

    private static async Task<WorkspaceInfo> WorkspaceAsync(AccessSnapshot access, AppsControlDbContext db, CancellationToken cancellationToken)
    {
        var codes = access.Companies.ToList();
        var companies = await db.Companies.AsNoTracking().Where(c => codes.Contains(c.Code)).ToListAsync(cancellationToken);
        var entityCodes = companies.Select(c => c.EntityCode).OfType<string>().Distinct().ToList();
        var entities = await db.Entities.AsNoTracking().Where(e => entityCodes.Contains(e.Code)).ToDictionaryAsync(e => e.Code, cancellationToken);
        var list = codes
            .Select(code => companies.Single(c => c.Code == code))
            .Select(c =>
            {
                var entity = c.EntityCode is null ? null : entities.GetValueOrDefault(c.EntityCode);
                return new WorkspaceCompany(c.Code, c.NameAr, c.NameEn, c.EntityCode, entity?.NameAr, entity?.NameEn);
            })
            .ToList();
        return new WorkspaceInfo(access.Company, access.CompanyChosen, list);
    }
}
