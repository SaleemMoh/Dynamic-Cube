using System.Text.RegularExpressions;
using DynamicCube.Modules.Content.Domain;
using DynamicCube.Modules.Content.Persistence;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Content.Runtime;

/// <summary>How a page was found: answers so far and the visitor's own answer, if any.</summary>
public sealed record FeedbackSummary(string Page, int Helpful, int Total, bool? Mine);

/// <summary>A service's average rating, how many rated it and the user's own stars, if any.</summary>
public sealed record RatingSummary(string Service, double Average, int Count, int? Mine);

public sealed record FeedbackRequest(string Page, bool Helpful, Guid? Visitor);

public sealed record RatingRequest(int Stars);

/// <summary>Staff view: pages by answers and services by rating.</summary>
public sealed record FeedbackReport(IReadOnlyList<FeedbackSummary> Pages, IReadOnlyList<RatingSummary> Services);

public sealed class FeedbackValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}

/// <summary>"Was this page helpful?" and service ratings on the portal.</summary>
internal sealed partial class FeedbackService(ContentDbContext context, ICurrentUser user, TimeProvider clock)
{
    public async Task<FeedbackSummary> PageAsync(string page, Guid? visitor, CancellationToken cancellationToken)
    {
        var key = PageKey(page);
        var answers = context.Feedback.AsNoTracking().Where(f => f.PageKey == key);
        var helpful = await answers.CountAsync(f => f.Helpful, cancellationToken);
        var total = await answers.CountAsync(cancellationToken);
        var voter = Voter(visitor, required: false);
        bool? mine = voter is null ? null : await answers.Where(f => f.VoterKey == voter).Select(f => (bool?)f.Helpful).FirstOrDefaultAsync(cancellationToken);
        return new FeedbackSummary(key, helpful, total, mine);
    }

    public async Task<FeedbackSummary> AnswerAsync(FeedbackRequest request, CancellationToken cancellationToken)
    {
        var key = PageKey(request.Page);
        var voter = Voter(request.Visitor, required: true)!;
        var answer = await context.Feedback.SingleOrDefaultAsync(f => f.PageKey == key && f.VoterKey == voter, cancellationToken);
        if (answer is null)
        {
            answer = new PageFeedback { PageKey = key, VoterKey = voter };
            context.Feedback.Add(answer);
        }

        answer.Helpful = request.Helpful;
        answer.AnsweredAt = clock.GetUtcNow();
        await SaveAsync(cancellationToken);
        return await PageAsync(key, request.Visitor, cancellationToken);
    }

    public async Task<RatingSummary> RatingAsync(string service, CancellationToken cancellationToken)
    {
        var code = ServiceCode(service);
        var ratings = context.Ratings.AsNoTracking().Where(r => r.ServiceCode == code);
        var count = await ratings.CountAsync(cancellationToken);
        var average = count == 0 ? 0 : await ratings.AverageAsync(r => r.Stars, cancellationToken);
        int? mine = user.IsAuthenticated ? await ratings.Where(r => r.UserId == user.UserId).Select(r => (int?)r.Stars).FirstOrDefaultAsync(cancellationToken) : null;
        return new RatingSummary(code, Math.Round(average, 1), count, mine);
    }

    public async Task<RatingSummary> RateAsync(string service, int stars, CancellationToken cancellationToken)
    {
        var code = ServiceCode(service);
        if (stars is < 1 or > 5)
        {
            throw new FeedbackValidationException("stars", "Give one to five stars.");
        }

        var rating = await context.Ratings.SingleOrDefaultAsync(r => r.ServiceCode == code && r.UserId == user.UserId, cancellationToken);
        if (rating is null)
        {
            rating = new ServiceRating { ServiceCode = code, UserId = user.UserId };
            context.Ratings.Add(rating);
        }

        rating.Stars = stars;
        rating.RatedAt = clock.GetUtcNow();
        await SaveAsync(cancellationToken);
        return await RatingAsync(code, cancellationToken);
    }

    public async Task<FeedbackReport> ReportAsync(CancellationToken cancellationToken)
    {
        var pages = await context.Feedback.AsNoTracking()
            .GroupBy(f => f.PageKey)
            .Select(g => new FeedbackSummary(g.Key, g.Count(f => f.Helpful), g.Count(), null))
            .ToListAsync(cancellationToken);
        var services = await context.Ratings.AsNoTracking()
            .GroupBy(r => r.ServiceCode)
            .Select(g => new RatingSummary(g.Key, g.Average(r => r.Stars), g.Count(), null))
            .ToListAsync(cancellationToken);
        return new FeedbackReport(
            [.. pages.OrderByDescending(p => p.Total).ThenBy(p => p.Page, StringComparer.Ordinal)],
            [.. services.Select(s => s with { Average = Math.Round(s.Average, 1) }).OrderByDescending(s => s.Count).ThenBy(s => s.Service, StringComparer.Ordinal)]);
    }

    /// <summary>A signed-in user answers as themselves; anyone else by the visitor id their browser keeps.</summary>
    private string? Voter(Guid? visitor, bool required)
    {
        if (user.IsAuthenticated)
        {
            return $"user:{user.UserId}";
        }

        if (visitor is { } id && id != Guid.Empty)
        {
            return $"visitor:{id:N}";
        }

        return required ? throw new FeedbackValidationException("visitor", "Send the visitor id kept by the browser.") : null;
    }

    private static string PageKey(string? page)
    {
        var key = page?.Trim().ToLowerInvariant() ?? string.Empty;
        return PageKeyPattern().IsMatch(key) ? key : throw new FeedbackValidationException("page", "Use a page key such as home, faq, service:CODE or page:slug.");
    }

    private static string ServiceCode(string? service)
    {
        var code = service?.Trim().ToUpperInvariant() ?? string.Empty;
        return ServiceCodePattern().IsMatch(code) ? code : throw new FeedbackValidationException("service", "Unknown service code.");
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two answers from the same visitor raced; the other one is kept.
            context.ChangeTracker.Clear();
        }
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9:._/-]{0,199}$")]
    private static partial Regex PageKeyPattern();

    [GeneratedRegex("^[A-Z0-9][A-Z0-9_-]{0,99}$")]
    private static partial Regex ServiceCodePattern();
}
