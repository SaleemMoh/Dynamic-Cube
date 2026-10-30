using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.Configuration;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Changes;

/// <summary>The look of a channel or an app; the code names it, the names are its title.</summary>
public sealed class AppBrandingPayload : ConfigPayload
{
    public string? ThemeColor { get; set; }

    public string? LogoFileKey { get; set; }

    public string? FooterImageKey { get; set; }

    public string? LoginImageKey { get; set; }

    public string? HeroImageKey { get; set; }
}

internal sealed class AppBrandingApplier(AppsControlDbContext db) : ConfigApplier<AppBranding, AppBrandingPayload>(db)
{
    public override string EntityType => AppsControlChangeTypes.AppBranding;

    public override string Area => AppsControlAreas.System;

    public override string ResourceCode => AppsControlResources.Installation;

    protected override IQueryable<AppBranding> Query(AppsControlDbContext context) => context.AppBrandings;

    protected override async Task ApplyAsync(AppBranding entity, AppBrandingPayload payload, RefResolver refs, ValidationErrors errors, AppBranding? existing, CancellationToken cancellationToken)
    {
        // A channel (internal system, portal, field app) or an app built with the platform (connected apps have their own look).
        entity.EntityCode = null;
        entity.BusinessApp = null;
        var channel = await refs.Context.Apps.AnyAsync(a => a.Code == entity.Code, cancellationToken);
        var app = !channel && await refs.Context.BusinessApps.AnyAsync(a => a.Code == entity.Code && a.Kind == AppKind.DynamicCube, cancellationToken);
        if (!channel && !app && !refs.IsPendingCreate(AppsControlChangeTypes.BusinessApp, entity.Code))
        {
            errors.Add("code", $"'{entity.Code}' is neither a channel (INTERNAL, EXTERNAL, FIELD) nor an app built with the platform.");
        }

        entity.ThemeColor = EntityApplier.Color(payload.ThemeColor, "themeColor", errors);
        entity.LogoFileKey = await EntityApplier.AssetAsync(refs, payload.LogoFileKey, "logoFileKey", errors, cancellationToken);
        entity.FooterImageKey = await EntityApplier.AssetAsync(refs, payload.FooterImageKey, "footerImageKey", errors, cancellationToken);
        entity.LoginImageKey = await EntityApplier.AssetAsync(refs, payload.LoginImageKey, "loginImageKey", errors, cancellationToken);
        entity.HeroImageKey = await EntityApplier.AssetAsync(refs, payload.HeroImageKey, "heroImageKey", errors, cancellationToken);
    }

    protected override Task<AppBrandingPayload> ToPayloadAsync(AppsControlDbContext context, AppBranding entity, CancellationToken cancellationToken) =>
        Task.FromResult(new AppBrandingPayload
        {
            ThemeColor = entity.ThemeColor,
            LogoFileKey = entity.LogoFileKey,
            FooterImageKey = entity.FooterImageKey,
            LoginImageKey = entity.LoginImageKey,
            HeroImageKey = entity.HeroImageKey,
        });
}
