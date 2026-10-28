using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Access;
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
    IReadOnlyList<string> Positions);

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
            return TypedResults.Ok(new MeInfo(user.UserName, access.NameAr, access.NameEn, access.PreferredLanguage, user.App, user.Roles, user.OrgUnits, user.PrimaryOrgUnit, user.Positions));
        }).WithName("GetMyProfile");

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

        group.MapGet("/classes/{code}/domains", async (string code, string? parent, AppsControlDbContext db, CancellationToken ct) =>
        {
            var rows = await db.Domains.AsNoTracking()
                .Where(d => d.Class!.Code == code && d.IsActive && (parent == null || d.ParentDomain!.Code == parent))
                .OrderBy(d => d.DisplayOrder).ThenBy(d => d.Code)
                .Select(d => new { d.Code, d.NameAr, d.NameEn, ParentCode = d.ParentDomain != null ? d.ParentDomain.Code : null, d.DisplayOrder, d.IsDefault, d.Attributes })
                .ToListAsync(ct);
            return TypedResults.Ok(rows.Select(d => new DomainItem(d.Code, d.NameAr, d.NameEn, d.ParentCode, d.DisplayOrder, d.IsDefault, JsonNode.Parse(d.Attributes) as JsonObject ?? [])).ToList());
        }).WithName("GetClassDomains");
    }
}
