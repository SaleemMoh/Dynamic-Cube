extern alias InternalApi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Endpoints;
using DynamicCube.Modules.AppsControl.Tests.Infrastructure;
using DynamicCube.Platform.ChangeSets;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.AppsControl.Tests;

public sealed class BrandingTests(AppsControlFixture fixture) : IAsyncLifetime
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13];

    private WebApplicationFactory<InternalApi::Program> _factory = null!;
    private Harness _harness = null!;

    public ValueTask InitializeAsync()
    {
        _harness = new Harness(fixture);
        _factory = new WebApplicationFactory<InternalApi::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DynamicCube", fixture.ConnectionString ?? "Host=127.0.0.1;Port=1;Database=none;Username=none");
            builder.UseSetting("AppsControl:BootstrapUsers:admin:0", "SYSTEM_ADMIN");
            builder.ConfigureTestServices(services => services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null));
        });
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _harness.DisposeAsync();
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"1\" height=\"1\"/></svg>", "image/svg+xml")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>", null)]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\" onload=\"alert(1)\"></svg>", null)]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><image href=\"https://evil.example/x.png\"/></svg>", null)]
    [InlineData("<html><body>not an image</body></html>", null)]
    public void Only_images_and_safe_svgs_are_accepted(string text, string? expected) =>
        BrandingEndpoints.DetectImageType(Encoding.UTF8.GetBytes(text)).ShouldBe(expected);

    [Fact]
    public void Raster_images_are_recognised_by_content() =>
        BrandingEndpoints.DetectImageType(Png).ShouldBe("image/png");

    [Fact]
    public async Task Uploaded_logo_and_footer_links_become_public_once_published()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        using var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.Add(TestAuthHandler.Header, "admin");
        using var anonymous = _factory.CreateClient();

        using var form = new MultipartFormDataContent();
        var image = new ByteArrayContent(Png);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(image, "file", "logo.png");
        var uploaded = await (await admin.PostAsync(new Uri("/api/apps-control/v1/admin/branding/assets", UriKind.Relative), form, ct))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<JsonElement>(ct);
        var key = uploaded.GetProperty("key").GetString()!;

        // Not public before the change is approved.
        (await anonymous.GetAsync(new Uri($"/api/apps-control/v1/branding/assets/{key}", UriKind.Relative), ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await admin.GetAsync(new Uri($"/api/apps-control/v1/branding/assets/{key}", UriKind.Relative), ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await _harness.PublishAsync(AppsControlAreas.System, new Change(AppsControlChangeTypes.Entity, "ENTITY", ChangeOperation.Update, new
        {
            nameAr = "وزارة تجريبية",
            nameEn = "Test Ministry",
            logoFileKey = key,
            footerLinks = new[] { new { labelAr = "سياسة الخصوصية", labelEn = "Privacy policy", url = "https://example.gov.sa/privacy" } },
        }));

        var branding = await anonymous.GetFromJsonAsync<JsonElement>("/api/apps-control/v1/branding", ct);
        branding.GetProperty("nameEn").GetString().ShouldBe("Test Ministry");
        branding.GetProperty("logoUrl").GetString().ShouldBe($"/api/apps-control/v1/branding/assets/{key}");
        branding.GetProperty("footerLinks")[0].GetProperty("labelEn").GetString().ShouldBe("Privacy policy");

        var logo = await anonymous.GetAsync(new Uri($"/api/apps-control/v1/branding/assets/{key}", UriKind.Relative), ct);
        logo.StatusCode.ShouldBe(HttpStatusCode.OK);
        logo.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
    }

    [Fact]
    public async Task Footer_links_must_be_safe_addresses_and_the_logo_must_exist()
    {
        var ex = await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(AppsControlAreas.System,
            new Change(AppsControlChangeTypes.Entity, "ENTITY", ChangeOperation.Update, new
            {
                nameAr = "جهة",
                nameEn = "Entity",
                logoFileKey = Guid.NewGuid().ToString(),
                footerLinks = new[] { new { labelAr = "رابط", labelEn = "Link", url = "javascript:alert(1)" } },
            })));

        ex.Errors.Keys.ShouldContain("logoFileKey");
        ex.Errors.Keys.ShouldContain("footerLinks[0].url");
    }

    [Fact]
    public async Task Contact_details_and_social_accounts_are_checked()
    {
        var ex = await Should.ThrowAsync<ChangeValidationException>(() => _harness.DraftAsync(AppsControlAreas.System,
            new Change(AppsControlChangeTypes.Entity, "ENTITY", ChangeOperation.Update, new
            {
                nameAr = "جهة",
                nameEn = "Entity",
                portalHeroImageKey = Guid.NewGuid().ToString(),
                contact = new
                {
                    phone = "call us",
                    email = "not-an-email",
                    mapUrl = "javascript:alert(1)",
                    social = new[] { new { network = "MYSPACE", url = "https://example.com" }, new { network = "X", url = "ftp://x" } },
                },
            })));

        ex.Errors.Keys.ShouldBe(
            ["portalHeroImageKey", "contact.phone", "contact.email", "contact.mapUrl", "contact.social[0].network", "contact.social[1].url"],
            ignoreOrder: true);
    }
}
