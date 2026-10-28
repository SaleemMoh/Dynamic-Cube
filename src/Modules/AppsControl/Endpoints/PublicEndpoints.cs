using System.Text.Json;
using DynamicCube.Modules.AppsControl.Access;
using DynamicCube.Modules.AppsControl.Lovs;
using DynamicCube.Modules.AppsControl.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Endpoints;

public sealed record UiTextItem(string Key, string TextAr, string TextEn);

public sealed record SettingValue(string Key, string? App, JsonElement Value);

/// <summary>Read endpoints shared by both hosts. On the external host only public LOVs and settings are served.</summary>
internal static class PublicEndpoints
{
    public static void Map(RouteGroupBuilder group, bool external)
    {
        BrandingEndpoints.MapPublic(group);

        group.MapGet("/ui-texts", async (string? app, AppsControlDbContext db, CancellationToken ct) =>
            {
                // App-specific texts override global ones with the same key.
                var rows = await db.UiTexts.AsNoTracking()
                    .Where(t => t.IsActive && (t.AppId == null || t.App!.Code == app))
                    .Select(t => new { t.Code, t.NameAr, t.NameEn, Global = t.AppId == null })
                    .ToListAsync(ct);
                return TypedResults.Ok(rows.GroupBy(r => r.Code).Select(g => g.OrderBy(r => r.Global).First()).Select(r => new UiTextItem(r.Code, r.NameAr, r.NameEn)).ToList());
            })
            .AllowAnonymous()
            .WithName(external ? "GetUiTextsExternal" : "GetUiTexts");

        var lovValues = group.MapGet("/lovs/{code}/values", async (string code, string? parent, string? q, int? page, int? size, string? resource, AccessResolver resolver, LovService lovs, CancellationToken ct) =>
            {
                var access = await resolver.GetAsync(ct);
                return TypedResults.Ok(await lovs.GetValuesAsync(code, access, new LovQuery(parent, q, page ?? 1, size ?? 0, resource), external, ct));
            })
            .WithName(external ? "GetLovValuesExternal" : "GetLovValues")
            .AddEndpointFilter(AppsControlFilters.Errors);

        var settings = group.MapGet("/settings/{key}", async (string key, string? app, AppsControlDbContext db, CancellationToken ct) =>
            {
                var rows = await db.Settings.AsNoTracking()
                    .Where(s => s.Code == key && s.IsActive && !s.IsSecret && (!external || s.IsPublic) && (s.AppId == null || s.App!.Code == app))
                    .Select(s => new { s.Code, App = s.App != null ? s.App.Code : null, s.Value })
                    .ToListAsync(ct);
                var row = rows.OrderBy(r => r.App is null).FirstOrDefault();
                return row is null
                    ? Results.NotFound()
                    : Results.Ok(new SettingValue(row.Code, row.App, JsonSerializer.Deserialize<JsonElement>(row.Value)));
            })
            .WithName(external ? "GetSettingExternal" : "GetSetting");

        if (external)
        {
            // Public LOVs and settings feed the portal's forms before and after sign-in.
            lovValues.AllowAnonymous();
            settings.AllowAnonymous();
        }
    }
}
