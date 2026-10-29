using DynamicCube.Modules.Content.Domain;
using DynamicCube.Modules.Content.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Content.Runtime;

public sealed record PageLink(string Slug, string TitleAr, string TitleEn, bool ShowInFooter, int Order);

public sealed record PageView(string Slug, string TitleAr, string TitleEn, string BodyAr, string BodyEn, DateTimeOffset UpdatedAt);

public sealed record FaqView(string Code, string QuestionAr, string QuestionEn, string AnswerAr, string AnswerEn, string? TopicAr, string? TopicEn, string? ServiceCode);

public sealed class ContentNotFoundException(string message) : Exception(message);

/// <summary>The portal's pages and frequently asked questions, as anyone may read them (active rows only).</summary>
internal sealed class ContentService(ContentDbContext context)
{
    /// <summary><c>TERMS_OF_USE</c> is addressed as <c>terms-of-use</c>.</summary>
    public static string Slug(string code) => code.ToLowerInvariant().Replace('_', '-');

    public static string CodeOf(string slug) => slug.ToUpperInvariant().Replace('-', '_');

    public async Task<IReadOnlyList<PageLink>> PagesAsync(CancellationToken cancellationToken)
    {
        var pages = await context.Pages.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Order).ThenBy(p => p.Code).ToListAsync(cancellationToken);
        return [.. pages.Select(p => new PageLink(Slug(p.Code), p.NameAr, p.NameEn, p.ShowInFooter, p.Order))];
    }

    public async Task<PageView> PageAsync(string slug, CancellationToken cancellationToken)
    {
        // Slugs are the code's spelling: '-' and '_' both stand for '_' in codes, which may hold either.
        var candidates = new[] { CodeOf(slug), slug.ToUpperInvariant() };
        var page = await context.Pages.AsNoTracking().Where(p => p.IsActive && candidates.Contains(p.Code)).OrderBy(p => p.Code).FirstOrDefaultAsync(cancellationToken)
            ?? throw new ContentNotFoundException($"Page '{slug}' not found.");
        return new PageView(Slug(page.Code), page.NameAr, page.NameEn, page.BodyAr, page.BodyEn, page.UpdatedAt ?? page.CreatedAt);
    }

    /// <summary>All questions in topic order, or only those of a service.</summary>
    public async Task<IReadOnlyList<FaqView>> FaqsAsync(string? service, CancellationToken cancellationToken)
    {
        var query = context.Faqs.AsNoTracking().Where(f => f.IsActive);
        if (!string.IsNullOrWhiteSpace(service))
        {
            var code = service.Trim().ToUpperInvariant();
            query = query.Where(f => f.ServiceCode == code);
        }

        var faqs = await query.OrderBy(f => f.Order).ThenBy(f => f.Code).ToListAsync(cancellationToken);
        return [.. faqs.Select(View)];
    }

    private static FaqView View(Faq f) => new(f.Code, f.NameAr, f.NameEn, f.AnswerAr, f.AnswerEn, f.TopicAr, f.TopicEn, f.ServiceCode);
}
