using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using DynamicCube.Modules.AppsControl.Access;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Endpoints;

public sealed record BrandingInfo(
    string NameAr,
    string NameEn,
    string? LogoUrl,
    IReadOnlyList<FooterLink> FooterLinks,
    string? InternalThemeColor = null,
    string? PortalThemeColor = null,
    string? FooterImageUrl = null,
    string? LoginImageUrl = null,
    string? PortalHeroImageUrl = null,
    EntityContact? Contact = null,
    string? ThemeColor = null,
    string? TitleAr = null,
    string? TitleEn = null);

public sealed record UploadedAsset(string Key, string Url);

/// <summary>
/// Entity name, logo, footer links and image, colours and the sign-in image for every app's shell and the sign-in pages;
/// image upload for administrators. Each channel and app may have its own look (<see cref="AppBranding"/>): what an app
/// leaves empty comes from the channel, and what the channel leaves empty from the organization.
/// </summary>
internal static partial class BrandingEndpoints
{
    public const int MaxLogoBytes = 2 * 1024 * 1024;

    private const string InternalChannel = "INTERNAL";
    private const string PortalChannel = "EXTERNAL";

    public static string AssetUrl(string key) => $"{AppsControlModule.ApiPrefix}/branding/assets/{key}";

    public static void MapPublic(RouteGroupBuilder group)
    {
        group.MapGet("/branding", async (string? channel, string? app, HttpContext http, AppsControlDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Entities.AsNoTracking().FirstOrDefaultAsync(e => e.Code == GovernmentEntity.SingletonCode, ct);
            channel = string.IsNullOrWhiteSpace(channel) ? null : channel.Trim().ToUpperInvariant();
            app = string.IsNullOrWhiteSpace(app) ? null : app.Trim();
            var codes = new[] { InternalChannel, PortalChannel, channel, app }.OfType<string>().ToList();
            var rows = await db.AppBrandings.AsNoTracking().Where(b => b.IsActive && codes.Contains(b.Code)).ToListAsync(ct);
            AppBranding? Of(string? code) => code is null ? null : rows.FirstOrDefault(r => r.Code == code);
            var own = Of(app);
            var looks = new[] { own, Of(channel) };
            string? Pick(Func<AppBranding, string?> field, string? fallback) =>
                looks.OfType<AppBranding>().Select(field).FirstOrDefault(v => !string.IsNullOrEmpty(v)) ?? fallback;
            string? Url(string? key) => key is null ? null : AssetUrl(key);

            var internalColor = new[] { own, Of(InternalChannel) }.OfType<AppBranding>().Select(b => b.ThemeColor).FirstOrDefault(v => v is not null) ?? entity?.InternalThemeColor;
            var portalColor = new[] { own, Of(PortalChannel) }.OfType<AppBranding>().Select(b => b.ThemeColor).FirstOrDefault(v => v is not null) ?? entity?.PortalThemeColor;
            var title = looks.OfType<AppBranding>().FirstOrDefault();

            // Public and read-only: the sign-in pages (another origin) read it too.
            http.Response.Headers.AccessControlAllowOrigin = "*";
            return TypedResults.Ok(new BrandingInfo(
                entity?.NameAr ?? string.Empty,
                entity?.NameEn ?? string.Empty,
                Url(Pick(b => b.LogoFileKey, entity?.LogoFileKey)),
                entity?.FooterLinks ?? [],
                internalColor,
                portalColor,
                Url(Pick(b => b.FooterImageKey, entity?.FooterImageKey)),
                Url(Pick(b => b.LoginImageKey, entity?.LoginImageKey)),
                Url(Pick(b => b.HeroImageKey, entity?.PortalHeroImageKey)),
                entity?.Contact ?? new EntityContact(),
                channel == PortalChannel ? portalColor : internalColor,
                title?.NameAr,
                title?.NameEn));
        }).AllowAnonymous();

        // Anyone may load the images in use; other (e.g. not yet approved) uploads only for Apps Control administrators.
        group.MapGet("/branding/assets/{id:guid}", async (Guid id, HttpContext http, AppsControlDbContext db, AccessResolver resolver, CancellationToken ct) =>
        {
            var entity = await db.Entities.AsNoTracking().FirstOrDefaultAsync(e => e.Code == GovernmentEntity.SingletonCode, ct);
            var looks = await db.AppBrandings.AsNoTracking().Where(b => b.IsActive).ToListAsync(ct);
            var inUse = (entity?.AssetKeys() ?? []).Concat(looks.SelectMany(b => b.AssetKeys()));
            if (!inUse.Any(k => string.Equals(k, id.ToString(), StringComparison.OrdinalIgnoreCase)))
            {
                var access = await resolver.GetAsync(ct);
                if (!access.Can(AppsControlResources.Installation, PrivilegeActions.View))
                {
                    return Results.NotFound();
                }
            }

            var asset = await db.BrandingAssets.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct);
            if (asset is null)
            {
                return Results.NotFound();
            }

            // Assets never change, so they can be cached for good. SVGs are shown as images only: no scripts, no fetches.
            http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            http.Response.Headers.XContentTypeOptions = "nosniff";
            http.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; sandbox";
            return Results.File(asset.Content, asset.ContentType);
        }).AllowAnonymous();
    }

    public static void MapAdmin(RouteGroupBuilder admin)
    {
        admin.MapPost("/branding/assets", async (IFormFile file, AppsControlDbContext db, CancellationToken ct) =>
        {
            if (file.Length == 0 || file.Length > MaxLogoBytes)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [$"Upload an image of at most {MaxLogoBytes / 1024} KB."] });
            }

            byte[] content;
            await using (var stream = file.OpenReadStream())
            using (var buffer = new MemoryStream())
            {
                await stream.CopyToAsync(buffer, ct);
                content = buffer.ToArray();
            }

            var contentType = DetectImageType(content);
            if (contentType is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Use a PNG, JPEG, WebP or SVG image (SVG without scripts or external references)."] });
            }

            var asset = new BrandingAsset
            {
                FileName = Path.GetFileName(file.FileName ?? "logo"),
                ContentType = contentType,
                Content = content,
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(content)),
            };
            db.BrandingAssets.Add(asset);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new UploadedAsset(asset.Id.ToString(), AssetUrl(asset.Id.ToString())));
        })
        .DisableAntiforgery()
        .RequirePrivilege(AppsControlResources.Installation, PrivilegeActions.Edit)
        .WithName("UploadBrandingAsset");
    }

    /// <summary>Identifies the image by its content (never by the file name or the browser's claim).</summary>
    internal static string? DetectImageType(byte[] content)
    {
        ReadOnlySpan<byte> bytes = content;
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return "image/png";
        }

        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return "image/jpeg";
        }

        if (bytes.Length > 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        return IsSafeSvg(content) ? "image/svg+xml" : null;
    }

    private static bool IsSafeSvg(byte[] content)
    {
        string text;
        try
        {
            text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(content);
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        if (UnsafeSvg().IsMatch(text))
        {
            return false;
        }

        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var reader = XmlReader.Create(new StringReader(text), settings);
            reader.MoveToContent();
            return reader.LocalName == "svg";
        }
        catch (XmlException)
        {
            return false;
        }
    }

    // Scripts, event handlers, javascript: URLs, embedded HTML and references to other files are refused.
    [GeneratedRegex(@"<\s*script|<\s*foreignObject|\son[a-z]+\s*=|javascript:|(?:xlink:)?href\s*=\s*[""'](?!#|data:image/)", RegexOptions.IgnoreCase)]
    private static partial Regex UnsafeSvg();
}
