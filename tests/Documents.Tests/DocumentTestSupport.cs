using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Documents.Changes;
using DynamicCube.Modules.Documents.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Documents;
using DynamicCube.Platform.Parties;
using DynamicCube.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Documents.Tests;

public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>Party facts for any id: an establishment named after the id, with a CR number.</summary>
internal sealed class FakeParties : IPartyFacts
{
    public Task<PartyFacts?> GetAsync(Guid partyId, bool refreshProfile, CancellationToken cancellationToken) =>
        Task.FromResult<PartyFacts?>(new PartyFacts(
            partyId, PartyKind.Establishment, "مؤسسة " + partyId.ToString()[..4], "Establishment " + partyId.ToString()[..4],
            new Dictionary<string, string> { ["CR_NUMBER"] = "1010" + partyId.ToString("N")[..6] }, new Dictionary<string, JsonNode?>(), [], []));
}

/// <summary>Keeps the HTML it is asked to print and answers with a tiny PDF.</summary>
internal sealed class FakePdf : IPdfRenderer
{
    public ConcurrentQueue<string> Printed { get; } = new();

    public Task<byte[]> RenderAsync(string html, IReadOnlyDictionary<string, byte[]> assets, PdfPage page, CancellationToken cancellationToken)
    {
        Printed.Enqueue(html);
        return Task.FromResult("%PDF-1.7 fake"u8.ToArray());
    }
}

internal sealed class DocumentHost : IAsyncDisposable
{
    public DocumentHost(DatabaseFixture fixture, bool realPdf = false)
    {
        Host = new TestHost(fixture.RequireConnectionString(), s =>
        {
            s.AddSingleton(Clock);
            s.AddSingleton<TimeProvider>(Clock);
            s.AddSingleton<IPartyFacts>(new FakeParties());
            if (!realPdf)
            {
                s.AddSingleton<IPdfRenderer>(Pdf);
            }
        });
    }

    public TestHost Host { get; }

    public TestClock Clock { get; } = new();

    public FakePdf Pdf { get; } = new();

    public static string Unique(string prefix) => TestHost.Unique(prefix);

    public const string TemplateHtml = """
        <html dir="rtl"><head><title>{{ document.number }}</title></head><body>
        <h1>{{ document.typeNameAr }}</h1>
        <p class="holder">{{ holder.nameAr }} — {{ holder.identifiers.CR_NUMBER }}</p>
        <p class="data">{{ data.city }}</p>
        <table>{% for line in lines %}<tr><td>{{ line.sector }}</td><td>{{ line.grade }}</td></tr>{% endfor %}</table>
        <p>{{ document.issuedOn }} – {{ document.expiresOn }}</p>
        <img src="{{ qr }}" alt="{{ verifyUrl }}">
        </body></html>
        """;

    /// <summary>Publishes a template and a document type; returns the type code.</summary>
    public async Task<string> TypeAsync(bool onePerHolder = true, int? validityMonths = 24, bool amendResets = false, int[]? alerts = null)
    {
        var template = Unique("TPL");
        var type = Unique("CERT");
        await Host.PublishAsync(
            DocumentChangeTypes.Area,
            new Change(DocumentChangeTypes.Template, template, ChangeOperation.Create, new { nameAr = "قالب", nameEn = "Template", html = TemplateHtml }),
            new Change(DocumentChangeTypes.DocumentType, type, ChangeOperation.Create, new
            {
                nameAr = "شهادة التصنيف",
                nameEn = "Classification certificate",
                numberPrefix = "CC" + type[^4..].Where(char.IsLetterOrDigit).Aggregate(string.Empty, (a, c) => a + char.ToUpperInvariant(c)),
                validityMonths,
                onePerHolder,
                amendResetsValidity = amendResets,
                templateCode = template,
                alertDaysBefore = alerts ?? [],
                fields = new[] { new { code = "city", nameAr = "المدينة", nameEn = "City", isPublic = true }, new { code = "internalNote", nameAr = "ملاحظة", nameEn = "Note", isPublic = false } },
                lineFields = new[] { new { code = "sector", nameAr = "القطاع", nameEn = "Sector", isPublic = true }, new { code = "points", nameAr = "النقاط", nameEn = "Points", isPublic = false }, new { code = "grade", nameAr = "الدرجة", nameEn = "Grade", isPublic = true } },
            }));
        return type;
    }

    public Task<IssuedDocumentInfo> IssueAsync(string type, Guid holder, string city = "Riyadh", params (string Sector, int Grade)[] lines) =>
        Host.AsAsync("issuer", sp => sp.GetRequiredService<IDocumentIssuer>().IssueAsync(
            new DocumentIssue(
                type,
                holder,
                new JsonObject { ["city"] = city, ["internalNote"] = "secret" },
                new JsonArray([.. lines.Select(l => (JsonNode?)new JsonObject { ["sector"] = l.Sector, ["grade"] = l.Grade, ["points"] = 77 })]),
                "SERVICE_REQUEST",
                Guid.CreateVersion7(),
                "CLS-2026-000001",
                "Approved"),
            CancellationToken.None));

    public Task<T> RegistryAsync<T>(string user, Func<DocumentRegistry, Task<T>> action) =>
        Host.AsAsync(user, sp => action(sp.GetRequiredService<DocumentRegistry>()));

    public async ValueTask DisposeAsync() => await Host.DisposeAsync();
}
