using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Documents.Domain;
using DynamicCube.Platform.Templates;
using Microsoft.Extensions.Options;
using QRCoder;

namespace DynamicCube.Modules.Documents.Runtime;

/// <summary>What a template sees: the document, the holder, its data and lines, and the verification link and QR code.</summary>
internal sealed record DocumentModel(JsonObject Document, JsonObject Holder, JsonObject Data, JsonArray Lines, string VerifyUrl)
{
    public JsonObject ToJson(string qr) => new()
    {
        ["document"] = Document.DeepClone(),
        ["holder"] = Holder.DeepClone(),
        ["data"] = Data.DeepClone(),
        ["lines"] = Lines.DeepClone(),
        ["verifyUrl"] = VerifyUrl,
        ["qr"] = qr,
    };
}

/// <summary>Turns a template and a document model into HTML (with the bundled Arabic font) and PDF.</summary>
internal sealed class DocumentRenderer(LiquidRenderer liquid, IPdfRenderer pdf, IOptions<DocumentsOptions> options)
{
    private const string FontFamily = "IBM Plex Sans Arabic";
    private static readonly Lazy<IReadOnlyDictionary<string, byte[]>> Fonts = new(LoadFonts);

    public string VerifyUrl(string code) => new Uri(options.Value.VerifyBaseUrl, Uri.EscapeDataString(code)).ToString();

    /// <param name="inlineFonts">Embed the fonts (HTML shown in a browser) instead of referring to files sent with it (PDF).</param>
    public string Html(DocumentTemplate template, DocumentModel model, bool inlineFonts)
    {
        var body = liquid.RenderHtml(template.Html, model.ToJson(QrDataUri(model.VerifyUrl)));
        var style = new StringBuilder("<style>");
        foreach (var (name, content) in Fonts.Value)
        {
            var weight = name.Contains("-700-", StringComparison.Ordinal) ? 700 : name.Contains("-600-", StringComparison.Ordinal) ? 600 : 400;
            var source = inlineFonts ? $"data:font/woff2;base64,{Convert.ToBase64String(content)}" : name;
            var range = name.Contains("-arabic-arabic-", StringComparison.Ordinal) ? "unicode-range:U+0600-06FF,U+0750-077F,U+08A0-08FF,U+FB50-FDFF,U+FE70-FEFF;" : string.Empty;
            style.Append(CultureInfo.InvariantCulture, $"@font-face{{font-family:'{FontFamily}';src:url('{source}') format('woff2');font-weight:{weight};{range}}}");
        }

        style.Append(CultureInfo.InvariantCulture, $"html,body{{margin:0;font-family:'{FontFamily}',sans-serif;-webkit-print-color-adjust:exact;print-color-adjust:exact}}</style>");
        var head = body.IndexOf("<head>", StringComparison.OrdinalIgnoreCase);
        return head >= 0
            ? body.Insert(head + "<head>".Length, $"<meta charset=\"utf-8\">{style}")
            : $"<!doctype html><html><head><meta charset=\"utf-8\">{style}</head><body>{body}</body></html>";
    }

    public Task<byte[]> PdfAsync(DocumentTemplate template, DocumentModel model, CancellationToken cancellationToken) =>
        pdf.RenderAsync(Html(template, model, inlineFonts: false), Fonts.Value, new PdfPage(template.PageSize, template.Landscape), cancellationToken);

    /// <summary>The verification link as a PNG QR code (data URI) for an &lt;img&gt;.</summary>
    public static string QrDataUri(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(8);
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }

    private static Dictionary<string, byte[]> LoadFonts()
    {
        var assembly = typeof(DocumentRenderer).Assembly;
        var fonts = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith("fonts/", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            fonts[resource["fonts/".Length..]] = buffer.ToArray();
        }

        // Latin faces first: the Arabic faces (later, with a unicode range) win for Arabic letters.
        return fonts.OrderBy(f => f.Key.Contains("-arabic-arabic-", StringComparison.Ordinal)).ToDictionary(f => f.Key, f => f.Value, StringComparer.Ordinal);
    }
}
