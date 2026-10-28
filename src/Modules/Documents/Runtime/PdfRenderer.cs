using System.Globalization;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace DynamicCube.Modules.Documents.Runtime;

public sealed class DocumentsOptions
{
    public const string SectionName = "Documents";

    /// <summary>Base URL of the Gotenberg service that turns HTML into PDF.</summary>
    public Uri GotenbergUrl { get; set; } = new("http://localhost:3000");

    /// <summary>Public page the QR code of a document links to (the code is appended).</summary>
    public Uri VerifyBaseUrl { get; set; } = new("http://localhost:4201/verify/");

    public TimeSpan RenderTimeout { get; set; } = TimeSpan.FromSeconds(60);
}

/// <summary>Page geometry of a rendered document.</summary>
public sealed record PdfPage(string Size, bool Landscape);

/// <summary>HTML → PDF. The default implementation calls Gotenberg (headless Chromium), see ADR-0011.</summary>
public interface IPdfRenderer
{
    /// <param name="assets">Files the HTML refers to by name (fonts, images).</param>
    Task<byte[]> RenderAsync(string html, IReadOnlyDictionary<string, byte[]> assets, PdfPage page, CancellationToken cancellationToken);
}

/// <summary>The PDF service failed or is not reachable.</summary>
public sealed class PdfRenderException(string message, Exception? inner = null) : Exception(message, inner);

internal sealed class GotenbergPdfRenderer(IHttpClientFactory clients, IOptions<DocumentsOptions> options) : IPdfRenderer
{
    public const string HttpClientName = "documents.gotenberg";

    // Paper sizes in inches (width × height, portrait).
    private static readonly Dictionary<string, (double Width, double Height)> Sizes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A4"] = (8.27, 11.69),
        ["A5"] = (5.83, 8.27),
        ["LETTER"] = (8.5, 11),
    };

    public async Task<byte[]> RenderAsync(string html, IReadOnlyDictionary<string, byte[]> assets, PdfPage page, CancellationToken cancellationToken)
    {
        var (width, height) = Sizes.TryGetValue(page.Size, out var size) ? size : Sizes["A4"];
        using var form = new MultipartFormDataContent();
        Add(form, "index.html", System.Text.Encoding.UTF8.GetBytes(html), "text/html");
        foreach (var (name, content) in assets)
        {
            Add(form, name, content, "application/octet-stream");
        }

        form.Add(new StringContent(width.ToString(CultureInfo.InvariantCulture)), "paperWidth");
        form.Add(new StringContent(height.ToString(CultureInfo.InvariantCulture)), "paperHeight");
        form.Add(new StringContent(page.Landscape ? "true" : "false"), "landscape");
        form.Add(new StringContent("true"), "printBackground");
        foreach (var margin in new[] { "marginTop", "marginBottom", "marginLeft", "marginRight" })
        {
            form.Add(new StringContent("0"), margin);
        }

        var client = clients.CreateClient(HttpClientName);
        client.Timeout = options.Value.RenderTimeout;
        try
        {
            using var response = await client.PostAsync(new Uri(options.Value.GotenbergUrl, "forms/chromium/convert/html"), form, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new PdfRenderException($"The PDF service answered {(int)response.StatusCode}: {body[..Math.Min(body.Length, 500)]}");
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new PdfRenderException($"The PDF service at {options.Value.GotenbergUrl} is not reachable.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PdfRenderException("The PDF service did not answer in time.", ex);
        }
    }

    private static void Add(MultipartFormDataContent form, string name, byte[] content, string type)
    {
        var part = new ByteArrayContent(content);
        part.Headers.ContentType = new MediaTypeHeaderValue(type);
        form.Add(part, "files", name);
    }
}
