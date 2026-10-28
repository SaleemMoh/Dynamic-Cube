using DynamicCube.Platform.Persistence;

namespace DynamicCube.Modules.AppsControl.Domain;

/// <summary>
/// An uploaded branding image (entity logo, later the DGA badge). Stored in the database so both API hosts can serve
/// it without shared file storage. Assets are immutable: a new upload is a new asset, made current by a change set.
/// </summary>
public sealed class BrandingAsset : IHasAuditColumns
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public byte[] Content { get; set; } = [];

    public string Sha256 { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}

/// <summary>A link in the footer of every app (privacy policy, terms, contact…).</summary>
public sealed class FooterLink
{
    public string LabelAr { get; set; } = string.Empty;

    public string LabelEn { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;
}
