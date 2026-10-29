using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using Microsoft.EntityFrameworkCore;

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

internal sealed class InstallationApplier(AppsControlDbContext db) : ConfigApplier<Installation, InstallationPayload>(db)
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

    /// <summary>Id of an uploaded branding asset (see <c>admin/branding/assets</c>).</summary>
    public string? LogoFileKey { get; set; }

    public List<FooterLink> FooterLinks { get; set; } = [];

    /// <summary>Main colour of the internal system, <c>#RRGGBB</c>.</summary>
    public string? InternalThemeColor { get; set; }

    /// <summary>Main colour of the portal, <c>#RRGGBB</c>.</summary>
    public string? PortalThemeColor { get; set; }

    /// <summary>Id of an uploaded branding asset shown in the footer.</summary>
    public string? FooterImageKey { get; set; }

    /// <summary>Id of an uploaded branding asset shown beside the sign-in form.</summary>
    public string? LoginImageKey { get; set; }

    /// <summary>Id of an uploaded branding asset behind the portal's page titles.</summary>
    public string? PortalHeroImageKey { get; set; }

    public EntityContact? Contact { get; set; }
}

internal sealed partial class EntityApplier(AppsControlDbContext db) : ConfigApplier<GovernmentEntity, EntityPayload>(db)
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

        entity.LogoFileKey = await AssetAsync(refs, payload.LogoFileKey, "logoFileKey", errors, cancellationToken);
        entity.FooterImageKey = await AssetAsync(refs, payload.FooterImageKey, "footerImageKey", errors, cancellationToken);
        entity.LoginImageKey = await AssetAsync(refs, payload.LoginImageKey, "loginImageKey", errors, cancellationToken);
        entity.PortalHeroImageKey = await AssetAsync(refs, payload.PortalHeroImageKey, "portalHeroImageKey", errors, cancellationToken);
        entity.Contact = Contact(payload.Contact ?? new EntityContact(), errors);
        entity.InternalThemeColor = Color(payload.InternalThemeColor, "internalThemeColor", errors);
        entity.PortalThemeColor = Color(payload.PortalThemeColor, "portalThemeColor", errors);

        if (payload.FooterLinks.Count > MaxFooterLinks)
        {
            errors.Add("footerLinks", $"At most {MaxFooterLinks} links.");
        }

        foreach (var (link, i) in payload.FooterLinks.Select((l, i) => (l, i)))
        {
            errors.Required($"footerLinks[{i}].labelAr", link.LabelAr);
            errors.Required($"footerLinks[{i}].labelEn", link.LabelEn);
            errors.MaxLength($"footerLinks[{i}].labelAr", link.LabelAr, 100);
            errors.MaxLength($"footerLinks[{i}].labelEn", link.LabelEn, 100);
            if (!IsAllowedUrl(link.Url))
            {
                errors.Add($"footerLinks[{i}].url", "Use a web address (https://…), a mailto: address or a path starting with /.");
            }
        }

        entity.FooterLinks = payload.FooterLinks
            .Select(l => new FooterLink { LabelAr = l.LabelAr.Trim(), LabelEn = l.LabelEn.Trim(), Url = l.Url.Trim() })
            .ToList();
    }

    private const int MaxFooterLinks = 12;

    private const int MaxSocialLinks = 8;

    private static readonly string[] Networks = ["X", "LINKEDIN", "INSTAGRAM", "YOUTUBE", "FACEBOOK", "SNAPCHAT", "TIKTOK", "WHATSAPP"];

    private static EntityContact Contact(EntityContact contact, ValidationErrors errors)
    {
        static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        var result = new EntityContact
        {
            Phone = Clean(contact.Phone),
            Email = Clean(contact.Email),
            AddressAr = Clean(contact.AddressAr),
            AddressEn = Clean(contact.AddressEn),
            MapUrl = Clean(contact.MapUrl),
        };
        if (result.Phone is not null && !PhoneNumber().IsMatch(result.Phone))
        {
            errors.Add("contact.phone", "Use digits, spaces, + ( ) and -, 3 to 20 characters.");
        }

        if (result.Email is not null && (result.Email.Length > 200 || !EmailAddress().IsMatch(result.Email)))
        {
            errors.Add("contact.email", "Use an email address like info@example.gov.sa.");
        }

        errors.MaxLength("contact.addressAr", result.AddressAr, 200);
        errors.MaxLength("contact.addressEn", result.AddressEn, 200);
        if (result.MapUrl is not null && !IsWebAddress(result.MapUrl))
        {
            errors.Add("contact.mapUrl", "Use a web address (https://…).");
        }

        if (contact.Social.Count > MaxSocialLinks)
        {
            errors.Add("contact.social", $"At most {MaxSocialLinks} accounts.");
        }

        foreach (var (link, i) in contact.Social.Select((l, i) => (l, i)))
        {
            var network = link.Network?.Trim().ToUpperInvariant() ?? string.Empty;
            if (!Networks.Contains(network))
            {
                errors.Add($"contact.social[{i}].network", $"Use one of {string.Join(", ", Networks)}.");
            }

            if (!IsWebAddress(link.Url))
            {
                errors.Add($"contact.social[{i}].url", "Use a web address (https://…).");
            }

            result.Social.Add(new SocialLink { Network = network, Url = link.Url?.Trim() ?? string.Empty });
        }

        return result;
    }

    private static bool IsWebAddress(string? url) =>
        !string.IsNullOrWhiteSpace(url) && url.Length <= 500 && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    [System.Text.RegularExpressions.GeneratedRegex(@"^[0-9+() \-]{3,20}$")]
    private static partial System.Text.RegularExpressions.Regex PhoneNumber();

    [System.Text.RegularExpressions.GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial System.Text.RegularExpressions.Regex EmailAddress();

    /// <summary>An uploaded image's id, or null; reported when it matches no upload.</summary>
    private static async Task<string?> AssetAsync(RefResolver refs, string? key, string field, ValidationErrors errors, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        key = key.Trim();
        if (!Guid.TryParse(key, out var assetId) || !await refs.Context.BrandingAssets.AnyAsync(a => a.Id == assetId, cancellationToken))
        {
            errors.Add(field, "Upload the image first; the key does not match an uploaded image.");
        }

        return key;
    }

    private static string? Color(string? value, string field, ValidationErrors errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim().ToLowerInvariant();
        if (!HexColor().IsMatch(value))
        {
            errors.Add(field, "Use a colour like #ca9f45.");
        }

        return value;
    }

    [System.Text.RegularExpressions.GeneratedRegex("^#[0-9a-f]{6}$")]
    private static partial System.Text.RegularExpressions.Regex HexColor();

    private static bool IsAllowedUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > 500)
        {
            return false;
        }

        url = url.Trim();
        if (url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal))
        {
            return true;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeMailto);
    }

    protected override Task<EntityPayload> ToPayloadAsync(AppsControlDbContext context, GovernmentEntity entity, CancellationToken cancellationToken) =>
        Task.FromResult(new EntityPayload
        {
            EntityKind = entity.EntityKind,
            UnifiedNumber = entity.UnifiedNumber,
            CrNumber = entity.CrNumber,
            LogoFileKey = entity.LogoFileKey,
            FooterLinks = entity.FooterLinks,
            InternalThemeColor = entity.InternalThemeColor,
            PortalThemeColor = entity.PortalThemeColor,
            FooterImageKey = entity.FooterImageKey,
            LoginImageKey = entity.LoginImageKey,
            PortalHeroImageKey = entity.PortalHeroImageKey,
            Contact = entity.Contact,
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

internal sealed class AppApplier(AppsControlDbContext db) : ConfigApplier<App, AppPayload>(db)
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

public sealed class HolidayPayload : ConfigPayload
{
    public DateOnly FromDate { get; set; }

    /// <summary>Last day of the holiday; the same as <see cref="FromDate"/> when empty.</summary>
    public DateOnly? ToDate { get; set; }
}

/// <summary>Public holidays of the working calendar (e.g. <c>EID_AL_FITR_2027</c>).</summary>
internal sealed class HolidayApplier(AppsControlDbContext db) : ConfigApplier<Holiday, HolidayPayload>(db)
{
    public override string EntityType => AppsControlChangeTypes.Holiday;

    public override string Area => AppsControlAreas.System;

    public override string ResourceCode => AppsControlResources.Installation;

    protected override IQueryable<Holiday> Query(AppsControlDbContext context) => context.Holidays;

    protected override IQueryable<Holiday> ListQuery(AppsControlDbContext context, string? parent) => Query(context).OrderBy(h => h.FromDate);

    protected override Task ApplyAsync(Holiday entity, HolidayPayload payload, RefResolver refs, ValidationErrors errors, Holiday? existing, CancellationToken cancellationToken)
    {
        var to = payload.ToDate ?? payload.FromDate;
        if (payload.FromDate == default)
        {
            errors.Add("fromDate", "Required.");
        }
        else if (to < payload.FromDate)
        {
            errors.Add("toDate", "Must not be before the first day.");
        }
        else if (to.DayNumber - payload.FromDate.DayNumber > 60)
        {
            errors.Add("toDate", "A holiday lasts at most 60 days.");
        }

        entity.FromDate = payload.FromDate;
        entity.ToDate = to;
        return Task.CompletedTask;
    }

    protected override Task<HolidayPayload> ToPayloadAsync(AppsControlDbContext context, Holiday entity, CancellationToken cancellationToken) =>
        Task.FromResult(new HolidayPayload { FromDate = entity.FromDate, ToDate = entity.ToDate });
}
