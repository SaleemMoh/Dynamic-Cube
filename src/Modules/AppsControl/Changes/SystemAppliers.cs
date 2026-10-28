using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;

namespace DynamicCube.Modules.AppsControl.Changes;

public sealed class InstallationPayload : ConfigPayload
{
    public string SystemType { get; set; } = "PRODUCTION";

    public string? Copyright { get; set; }

    public string DefaultLanguage { get; set; } = "ar";

    public string TimeZone { get; set; } = "Asia/Riyadh";

    public List<int> WeekendDays { get; set; } = [5, 6];

    public CalendarDisplay CalendarDisplay { get; set; } = CalendarDisplay.Both;

    public string? LogoFileKey { get; set; }
}

internal sealed class InstallationApplier : ConfigApplier<Installation, InstallationPayload>
{
    public override string EntityType => AppsControlChangeTypes.Installation;

    public override string Area => AppsControlAreas.System;

    public override string ResourceCode => AppsControlResources.Installation;

    protected override IQueryable<Installation> Query(AppsControlDbContext context) => context.Installations;

    protected override void ValidateKey(string entityCode, ValidationErrors errors)
    {
        if (entityCode != Installation.SingletonCode)
        {
            errors.Add("code", $"The installation code is always {Installation.SingletonCode}.");
        }
    }

    protected override Task ApplyAsync(Installation entity, InstallationPayload payload, RefResolver refs, ValidationErrors errors, Installation? existing, CancellationToken cancellationToken)
    {
        if (payload.DefaultLanguage is not ("ar" or "en"))
        {
            errors.Add("defaultLanguage", "Use 'ar' or 'en'.");
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(payload.TimeZone ?? string.Empty, out _))
        {
            errors.Add("timeZone", "Unknown time zone.");
        }

        if (payload.WeekendDays.Any(d => d is < 1 or > 7))
        {
            errors.Add("weekendDays", "Use ISO day numbers 1 (Monday) to 7 (Sunday).");
        }

        errors.Required("systemType", payload.SystemType);
        entity.SystemType = payload.SystemType;
        entity.Copyright = payload.Copyright;
        entity.DefaultLanguage = payload.DefaultLanguage!;
        entity.TimeZone = payload.TimeZone!;
        entity.WeekendDays = payload.WeekendDays.Distinct().Order().ToList();
        entity.CalendarDisplay = payload.CalendarDisplay;
        entity.LogoFileKey = payload.LogoFileKey;
        return Task.CompletedTask;
    }

    protected override Task<InstallationPayload> ToPayloadAsync(AppsControlDbContext context, Installation entity, CancellationToken cancellationToken) =>
        Task.FromResult(new InstallationPayload
        {
            SystemType = entity.SystemType,
            Copyright = entity.Copyright,
            DefaultLanguage = entity.DefaultLanguage,
            TimeZone = entity.TimeZone,
            WeekendDays = entity.WeekendDays,
            CalendarDisplay = entity.CalendarDisplay,
            LogoFileKey = entity.LogoFileKey,
        });

    protected override Task BeforeDeleteAsync(AppsControlDbContext context, Installation entity, CancellationToken cancellationToken) =>
        throw new DynamicCube.Platform.ChangeSets.ChangeSetException("The installation cannot be deleted.");
}

public sealed class EntityPayload : ConfigPayload
{
    public string? EntityKind { get; set; }

    public string? UnifiedNumber { get; set; }

    public string? CrNumber { get; set; }

    public string? LogoFileKey { get; set; }
}

internal sealed class EntityApplier : ConfigApplier<GovernmentEntity, EntityPayload>
{
    public const string EntityKindClass = "ENTITY_KIND";

    public override string EntityType => AppsControlChangeTypes.Entity;

    public override string Area => AppsControlAreas.System;

    public override string ResourceCode => AppsControlResources.Installation;

    protected override IQueryable<GovernmentEntity> Query(AppsControlDbContext context) => context.Entities;

    protected override void ValidateKey(string entityCode, ValidationErrors errors)
    {
        if (entityCode != GovernmentEntity.SingletonCode)
        {
            errors.Add("code", $"The entity code is always {GovernmentEntity.SingletonCode}.");
        }
    }

    protected override async Task ApplyAsync(GovernmentEntity entity, EntityPayload payload, RefResolver refs, ValidationErrors errors, GovernmentEntity? existing, CancellationToken cancellationToken)
    {
        await refs.DomainAsync(EntityKindClass, payload.EntityKind, "entityKind", errors, cancellationToken);
        errors.MaxLength("unifiedNumber", payload.UnifiedNumber, 50);
        errors.MaxLength("crNumber", payload.CrNumber, 50);
        entity.EntityKind = payload.EntityKind;
        entity.UnifiedNumber = payload.UnifiedNumber;
        entity.CrNumber = payload.CrNumber;
        entity.LogoFileKey = payload.LogoFileKey;
    }

    protected override Task<EntityPayload> ToPayloadAsync(AppsControlDbContext context, GovernmentEntity entity, CancellationToken cancellationToken) =>
        Task.FromResult(new EntityPayload
        {
            EntityKind = entity.EntityKind,
            UnifiedNumber = entity.UnifiedNumber,
            CrNumber = entity.CrNumber,
            LogoFileKey = entity.LogoFileKey,
        });

    protected override Task BeforeDeleteAsync(AppsControlDbContext context, GovernmentEntity entity, CancellationToken cancellationToken) =>
        throw new DynamicCube.Platform.ChangeSets.ChangeSetException("The entity cannot be deleted.");
}

public sealed class AppPayload : ConfigPayload
{
    public AppType AppType { get; set; } = AppType.Other;

    public string? BaseUrl { get; set; }

    public int DisplayOrder { get; set; }
}

internal sealed class AppApplier : ConfigApplier<App, AppPayload>
{
    public override string EntityType => AppsControlChangeTypes.App;

    public override string Area => AppsControlAreas.System;

    public override string ResourceCode => AppsControlResources.Installation;

    protected override System.Text.RegularExpressions.Regex CodePattern => ValidationErrors.IdentifierCode();

    protected override IQueryable<App> Query(AppsControlDbContext context) => context.Apps;

    protected override IQueryable<App> ListQuery(AppsControlDbContext context, string? parent) =>
        context.Apps.OrderBy(a => a.DisplayOrder).ThenBy(a => a.Code);

    protected override Task ApplyAsync(App entity, AppPayload payload, RefResolver refs, ValidationErrors errors, App? existing, CancellationToken cancellationToken)
    {
        errors.MaxLength("baseUrl", payload.BaseUrl, 500);
        entity.AppType = payload.AppType;
        entity.BaseUrl = payload.BaseUrl;
        entity.DisplayOrder = payload.DisplayOrder;
        return Task.CompletedTask;
    }

    protected override Task<AppPayload> ToPayloadAsync(AppsControlDbContext context, App entity, CancellationToken cancellationToken) =>
        Task.FromResult(new AppPayload { AppType = entity.AppType, BaseUrl = entity.BaseUrl, DisplayOrder = entity.DisplayOrder });
}
