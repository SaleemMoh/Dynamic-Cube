extern alias ExternalApi;

using System.Net;
using System.Net.Http.Json;
using DynamicCube.Modules.Content.Changes;
using DynamicCube.Modules.Content.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task A_designed_page_shows_its_published_version_and_versions_never_change()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        await using var host = new TestHost(fixture.RequireConnectionString());
        var code = TestHost.Unique("ABOUT");
        object Design(string title) => new
        {
            version = 1,
            sections = new[]
            {
                new
                {
                    id = "s1",
                    layout = "1-1",
                    background = "light",
                    columns = new object[]
                    {
                        new object[] { new { id = "h1", kind = "heading", level = 1, textAr = title, textEn = title } },
                        new object[] { new { id = "b1", kind = "button", labelAr = "الخدمات", labelEn = "Services", url = "/services" } },
                    },
                },
            },
        };
        Change Version(int n, object design) => new(ContentChangeTypes.PageVersion, $"{code}/{n}", ChangeOperation.Create, new { nameAr = $"الإصدار {n}", nameEn = $"Version {n}", design });
        Change Page(int? published) => new(ContentChangeTypes.Page, code, published is null ? ChangeOperation.Create : ChangeOperation.Update, new { nameAr = "عن المنصة", nameEn = "About", publishedVersion = published });

        // A page made in the designer: no Markdown body, and its first version published in the same change set.
        await host.PublishAsync(ContentChangeTypes.Area, Page(null) with { Payload = new { nameAr = "عن المنصة", nameEn = "About", bodyAr = "نص", bodyEn = "Text" } });
        await host.PublishAsync(ContentChangeTypes.Area, Version(1, Design("الأول")), Page(1));
        await host.PublishAsync(ContentChangeTypes.Area, Version(2, Design("الثاني")));

        using var client = _factory.CreateClient();
        var slug = code.ToLowerInvariant().Replace('_', '-');
        var shown = await client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/content/v1/pages/{slug}", ct);
        shown.GetProperty("design").GetProperty("sections")[0].GetProperty("columns")[0][0].GetProperty("textAr").GetString().ShouldBe("الأول", "version 2 is not published yet");

        await host.PublishAsync(ContentChangeTypes.Area, Page(2));
        shown = await client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/content/v1/pages/{slug}", ct);
        shown.GetProperty("design").GetProperty("sections")[0].GetProperty("columns")[0][0].GetProperty("textAr").GetString().ShouldBe("الثاني");

        async Task<IEnumerable<string>> Problems(params Change[] changes) =>
            (await Should.ThrowAsync<ChangeValidationException>(() => host.PublishAsync(ContentChangeTypes.Area, changes))).Errors.Keys;
        (await Problems(Version(4, Design("x")))).ShouldContain("code", "versions take the next number");
        (await Problems(new Change(ContentChangeTypes.PageVersion, $"{code}/1", ChangeOperation.Update, new { nameAr = "س", nameEn = "X", design = Design("changed") }))).ShouldContain("design");
        (await Problems(Page(9))).ShouldContain("publishedVersion");
        var unsafeLink = new { version = 1, sections = new[] { new { layout = "1", columns = new object[] { new object[] { new { kind = "button", url = "javascript:alert(1)" } } } } } };
        (await Problems(Version(3, unsafeLink))).ShouldContain("design.sections[0].columns[0][0].url");
        await Should.ThrowAsync<ChangeSetException>(() => host.PublishAsync(ContentChangeTypes.Area, new Change(ContentChangeTypes.PageVersion, $"{code}/2", ChangeOperation.Delete, null)));
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

    [Fact]
    public async Task Visitors_answer_whether_a_page_helped_once_and_may_change_their_answer()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var page = $"service:{TestHost.Unique("FB").ToLowerInvariant()}";
        var visitor = Guid.NewGuid();

        (await client.PostAsJsonAsync("/api/content/v1/feedback", new { page, helpful = true, visitor }, ct)).EnsureSuccessStatusCode();
        await client.PostAsJsonAsync("/api/content/v1/feedback", new { page, helpful = false, visitor = Guid.NewGuid() }, ct);
        var changed = await client.PostAsJsonAsync("/api/content/v1/feedback", new { page, helpful = false, visitor }, ct);
        var summary = await changed.Content.ReadFromJsonAsync<FeedbackSummary>(ct);

        summary!.Total.ShouldBe(2, "one answer per visitor");
        summary.Helpful.ShouldBe(0);
        summary.Mine.ShouldBe(false);
        var anonymous = await client.GetFromJsonAsync<FeedbackSummary>($"/api/content/v1/feedback?page={page}", ct);
        anonymous!.Mine.ShouldBeNull();
        (await client.PostAsJsonAsync("/api/content/v1/feedback", new { page, helpful = true }, ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest, "visitors send their id");
        (await client.PostAsJsonAsync("/api/content/v1/feedback", new { page = "<script>", helpful = true, visitor }, ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Signed_in_users_rate_a_service_once_and_anyone_reads_the_average()
    {
        fixture.RequireConnectionString();
        var ct = TestContext.Current.CancellationToken;
        await using var host = new TestHost(fixture.RequireConnectionString());
        var service = TestHost.Unique("RATED");

        await host.AsAsync("applicant1", sp => sp.GetRequiredService<FeedbackService>().RateAsync(service, 5, ct));
        await host.AsAsync("applicant1", sp => sp.GetRequiredService<FeedbackService>().RateAsync(service, 4, ct));
        var mine = await host.AsAsync("applicant2", sp => sp.GetRequiredService<FeedbackService>().RateAsync(service, 3, ct));
        await Should.ThrowAsync<FeedbackValidationException>(() => host.AsAsync("applicant2", sp => sp.GetRequiredService<FeedbackService>().RateAsync(service, 6, ct)));

        mine.Count.ShouldBe(2);
        mine.Average.ShouldBe(3.5);
        mine.Mine.ShouldBe(3);
        using var client = _factory.CreateClient();
        (await client.GetFromJsonAsync<RatingSummary>($"/api/content/v1/ratings/{service}", ct))!.Count.ShouldBe(2);
        (await client.PutAsJsonAsync($"/api/content/v1/ratings/{service}", new { stars = 5 }, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
