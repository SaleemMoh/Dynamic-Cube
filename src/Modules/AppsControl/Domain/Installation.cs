namespace DynamicCube.Modules.AppsControl.Domain;

public enum CalendarDisplay
{
    Gregorian,
    Hijri,
    Both,
}

/// <summary>The single installation row: product-wide display and calendar defaults.</summary>
public sealed class Installation : ConfigEntity
{
    public const string SingletonCode = "INSTALLATION";

    public string SystemType { get; set; } = "PRODUCTION";

    public string? Copyright { get; set; }

    public string DefaultLanguage { get; set; } = "ar";

    public string TimeZone { get; set; } = "Asia/Riyadh";

    /// <summary>ISO day numbers (1 = Monday … 7 = Sunday).</summary>
    public List<int> WeekendDays { get; set; } = [5, 6];

    public CalendarDisplay CalendarDisplay { get; set; } = CalendarDisplay.Both;

    public string? LogoFileKey { get; set; }
}

/// <summary>A public holiday (one day or a range) that does not count as a working day in SLA calculations.</summary>
public sealed class Holiday : ConfigEntity
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }
}

/// <summary>The government entity (ministry, authority or company) that owns the installation.</summary>
public sealed class GovernmentEntity : ConfigEntity
{
    /// <summary>The default entity: the one an installation starts with, whose branding the apps show.</summary>
    public const string SingletonCode = "ENTITY";

    /// <summary>Domain code of class <c>ENTITY_KIND</c>.</summary>
    public string? EntityKind { get; set; }

    public string? UnifiedNumber { get; set; }

    public string? CrNumber { get; set; }

    /// <summary>Id of the <see cref="BrandingAsset"/> shown as the entity logo in headers.</summary>
    public string? LogoFileKey { get; set; }

    /// <summary>Links shown in the footer of every app.</summary>
    public List<FooterLink> FooterLinks { get; set; } = [];

    /// <summary>Main colour of the internal system (<c>#RRGGBB</c>); its own gold when empty.</summary>
    public string? InternalThemeColor { get; set; }

    /// <summary>Main colour of the portal (<c>#RRGGBB</c>); the DGA green when empty.</summary>
    public string? PortalThemeColor { get; set; }

    /// <summary>Id of the <see cref="BrandingAsset"/> shown in the footer (e.g. a Vision 2030 or DGA badge).</summary>
    public string? FooterImageKey { get; set; }

    /// <summary>Id of the <see cref="BrandingAsset"/> shown beside the sign-in form.</summary>
    public string? LoginImageKey { get; set; }

    /// <summary>Id of the <see cref="BrandingAsset"/> behind the titles of the portal's pages.</summary>
    public string? PortalHeroImageKey { get; set; }

    /// <summary>Phone, email, location and social accounts shown on the portal.</summary>
    public EntityContact Contact { get; set; } = new();

    /// <summary>The uploaded images in use (anyone may load these).</summary>
    public IEnumerable<string> AssetKeys() => new[] { LogoFileKey, FooterImageKey, LoginImageKey, PortalHeroImageKey }.OfType<string>();
}

public enum AppType
{
    Internal,
    External,
    Field,
    Other,
}

/// <summary>
/// A product channel (the internal system <c>INTERNAL</c>, the portal <c>EXTERNAL</c>, the field app <c>FIELD</c>):
/// fixed rows the product seeds, where Dynamic Cube apps appear and to which screens, roles and menus belong.
/// </summary>
public sealed class App : ConfigEntity
{
    public AppType AppType { get; set; }

    public string? BaseUrl { get; set; }

    public int DisplayOrder { get; set; }
}

public enum AppKind
{
    /// <summary>Built with the platform: services, portal, branding; data in the platform database.</summary>
    DynamicCube,

    /// <summary>An outside application that uses the platform as its provider (sign-in, users, roles, workflow…).</summary>
    Connected,
}

/// <summary>
/// An app of an organization (ADR-0036; called a business app before): e.g. Classification, Permits, Licensing. It owns
/// services, workflows, document types, roles and users through their <see cref="ConfigEntity.BusinessApp"/>; its
/// organization is <see cref="ConfigEntity.EntityCode"/>. A Dynamic Cube app appears on the channels it ticks (the
/// internal system, the portal, the field app: the codes of <see cref="App"/>); a connected app runs on its own screens.
/// </summary>
public sealed class BusinessApplication : ConfigEntity
{
    public string? Icon { get; set; }

    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    public int DisplayOrder { get; set; }

    public AppKind Kind { get; set; } = AppKind.DynamicCube;

    /// <summary>Channel codes (<c>INTERNAL</c>, <c>EXTERNAL</c>, <c>FIELD</c>) of a Dynamic Cube app; empty for a connected app.</summary>
    public List<string> Channels { get; set; } = [];

    /// <summary>Companies of the organization the app is enabled for; empty: all of them.</summary>
    public List<string> Companies { get; set; } = [];

    /// <summary>Branches of those companies the app is enabled for; empty: all of them.</summary>
    public List<string> Branches { get; set; } = [];

    /// <summary>Address of a connected app.</summary>
    public string? BaseUrl { get; set; }

    public bool OnChannel(string channel) => Kind == AppKind.DynamicCube && Channels.Contains(channel);
}
