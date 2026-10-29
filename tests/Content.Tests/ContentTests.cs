extern alias ExternalApi;

using System.Net;
using System.Net.Http.Json;
using DynamicCube.Modules.Content.Changes;
using DynamicCube.Modules.Content.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace DynamicCube.Modules.Content.Tests;

/// <summary>Pages and questions are configured through change sets and read by anyone through the external API.</summary>
public sealed class ContentTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<ExternalApi::Program> _factory = null!;

    public ValueTask InitializeAsync()
    {
        var connectionString = fixture.ConnectionString;
        _factory = new WebApplicationFactory<ExternalApi::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DynamicCube", connectionString ?? "Host=127.0.0.1;Port=1;Database=none;Username=none");
        });
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Pages_are_published_and_read_anonymously_by_their_address()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        await using var host = new TestHost(fixture.RequireConnectionString());
        var terms = TestHost.Unique("TERMS");
        var draft = TestHost.Unique("DRAFT_PAGE");
        await host.PublishAsync(ContentChangeTypes.Area,
            new Change(ContentChangeTypes.Page, terms, ChangeOperation.Create, new ContentPagePayload { NameAr = "شروط الاستخدام", NameEn = "Terms of use", BodyAr = "## الشروط\n\nنص", BodyEn = "## Terms\n\nText", ShowInFooter = true, Order = 10 }),
            new Change(ContentChangeTypes.Page, draft, ChangeOperation.Create, new ContentPagePayload { NameAr = "مسودة", NameEn = "Draft", BodyAr = "س", BodyEn = "S", IsActive = false }));
        using var client = _factory.CreateClient();
        var slug = terms.ToLowerInvariant().Replace('_', '-');

        var pages = (await client.GetFromJsonAsync<List<PageLink>>("/api/content/v1/pages", ct))!;
        pages.ShouldContain(p => p.Slug == slug && p.TitleEn == "Terms of use" && p.ShowInFooter);
        pages.ShouldNotContain(p => p.Slug == draft.ToLowerInvariant().Replace('_', '-'), "inactive pages are not shown");

        var page = await client.GetFromJsonAsync<PageView>($"/api/content/v1/pages/{slug}", ct);
        page!.BodyEn.ShouldBe("## Terms\n\nText");
        (await client.GetAsync($"/api/content/v1/pages/{draft.ToLowerInvariant()}", ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/content/v1/pages/no-such-page", ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Questions_are_grouped_by_topic_and_filtered_by_service()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        await using var host = new TestHost(fixture.RequireConnectionString());
        var service = TestHost.Unique("SVC");
        var general = TestHost.Unique("FAQ");
        var specific = TestHost.Unique("FAQ");
        await host.PublishAsync(ContentChangeTypes.Area,
            new Change(ContentChangeTypes.Faq, general, ChangeOperation.Create, new FaqPayload { NameAr = "كيف أسجل؟", NameEn = "How do I sign up?", AnswerAr = "عبر النفاذ الوطني.", AnswerEn = "Through Nafath.", TopicAr = "الحساب", TopicEn = "Account" }),
            new Change(ContentChangeTypes.Faq, specific, ChangeOperation.Create, new FaqPayload { NameAr = "كم الرسوم؟", NameEn = "What are the fees?", AnswerAr = "حسب الفئة.", AnswerEn = "By grade.", ServiceCode = service, Order = 5 }));
        using var client = _factory.CreateClient();

        var all = (await client.GetFromJsonAsync<List<FaqView>>("/api/content/v1/faqs", ct))!;
        all.ShouldContain(f => f.Code == general && f.TopicEn == "Account");
        all.ShouldContain(f => f.Code == specific);

        var ofService = await client.GetFromJsonAsync<List<FaqView>>($"/api/content/v1/faqs?service={service.ToLowerInvariant()}", ct);
        ofService!.Select(f => f.Code).ShouldBe([specific]);
    }

    [Fact]
    public async Task Pages_and_questions_are_checked()
    {
        await using var host = new TestHost(fixture.RequireConnectionString());
        var page = await Should.ThrowAsync<ChangeValidationException>(() => host.PublishAsync(ContentChangeTypes.Area,
            new Change(ContentChangeTypes.Page, TestHost.Unique("BAD"), ChangeOperation.Create, new ContentPagePayload { NameAr = "س", NameEn = "S", BodyAr = " ", BodyEn = "x", Order = -1 })));
        page.Errors.Keys.Order().ShouldBe(["bodyAr", "order"]);

        var faq = await Should.ThrowAsync<ChangeValidationException>(() => host.PublishAsync(ContentChangeTypes.Area,
            new Change(ContentChangeTypes.Faq, TestHost.Unique("BAD"), ChangeOperation.Create, new FaqPayload { NameAr = "س", NameEn = "S", AnswerAr = "ج", TopicAr = "موضوع", ServiceCode = "not a code" })));
        faq.Errors.Keys.Order().ShouldBe(["answerEn", "serviceCode", "topicEn"]);
    }
}
