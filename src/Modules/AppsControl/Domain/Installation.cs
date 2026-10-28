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
    public const string SingletonCode = "ENTITY";

    /// <summary>Domain code of class <c>ENTITY_KIND</c>.</summary>
    public string? EntityKind { get; set; }

    public string? UnifiedNumber { get; set; }

    public string? CrNumber { get; set; }

    /// <summary>Id of the <see cref="BrandingAsset"/> shown as the entity logo in headers.</summary>
    public string? LogoFileKey { get; set; }

    /// <summary>Links shown in the footer of every app.</summary>
    public List<FooterLink> FooterLinks { get; set; } = [];
}

public enum AppType
{
    Internal,
    External,
    Field,
    Other,
}

/// <summary>An application served by Apps Control (internal system, external portal, field app, …).</summary>
public sealed class App : ConfigEntity
{
    public AppType AppType { get; set; }

    public string? BaseUrl { get; set; }

    public int DisplayOrder { get; set; }
}
