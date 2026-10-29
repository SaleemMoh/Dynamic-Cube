using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Persistence;
using DynamicCube.Platform.Documents;
using DynamicCube.Platform.Parties;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Services.Runtime;

public sealed record PortalCategory(string Code, string NameAr, string NameEn, string? DescriptionAr, string? DescriptionEn, string? Icon);

/// <summary>Working days from the fastest to the slowest service level.</summary>
public sealed record WorkingDaysRange(int Min, int Max);

/// <summary>
/// What a service costs before anything is known about the applicant, in SAR with VAT. <see cref="Max"/> is null when a fee is
/// a formula; <see cref="DependsOnRequest"/> when the amount depends on the answers or the applicant (brackets, conditions).
/// </summary>
public sealed record CostRange(decimal Min, decimal? Max, bool DependsOnRequest)
{
    public static CostRange Free { get; } = new(0, 0, false);
}

/// <summary>A listed service as anyone sees it in the portal catalog.</summary>
public sealed record PortalServiceSummary(
    string Code,
    string NameAr,
    string NameEn,
    string? DescriptionAr,
    string? DescriptionEn,
    string? CategoryCode,
    string? Icon,
    IReadOnlyList<PartyKind> PartyKinds,
    string? AudienceAr,
    string? AudienceEn,
    WorkingDaysRange? Duration,
    CostRange Cost,
    PortalDocumentType? RequiredDocument = null);

/// <summary>The document a follow-up service is requested for (shown on its card).</summary>
public sealed record PortalDocumentType(string Code, string NameAr, string NameEn);

public sealed record PortalCatalog(IReadOnlyList<PortalCategory> Categories, IReadOnlyList<PortalServiceSummary> Services, DateTimeOffset? LastUpdated);

public sealed record PortalLevel(string Code, string NameAr, string NameEn, string? DescriptionAr, string? DescriptionEn, int? SlaWorkingDays, CostRange Cost);

/// <summary>The service card: the summary, its category, the levels with their duration and cost, and the card of the current version.</summary>
public sealed record ServiceCard(
    PortalServiceSummary Service,
    PortalCategory? Category,
    IReadOnlyList<PortalLevel> Levels,
    ServiceCardDefinition Card,
    int VersionNumber,
    DateTimeOffset? PublishedAt);

/// <summary>Counts for the portal's statistics tiles.</summary>
public sealed record PortalStatistics(int Services, int Requests, int InProgress, int Completed);

/// <summary>The public side of the catalog: categories, listed services and their cards, and statistics. Needs no sign-in.</summary>
public sealed class PortalCatalogService(ServicesDbContext context, ServiceCatalogCache cache, IServiceProvider providers)
{
    public async Task<PortalCatalog> CatalogAsync(CancellationToken cancellationToken)
    {
        var categories = await CategoriesAsync(cancellationToken);
        var services = new List<PortalServiceSummary>();
        DateTimeOffset? lastUpdated = null;
        foreach (var row in await ListedAsync(null, cancellationToken))
        {
            var version = await cache.GetAsync(context, row.VersionId, cancellationToken);
            services.Add(Summary(row.Service, version) with { RequiredDocument = await DocumentTypeAsync(row.Service.DocumentType, cancellationToken) });
            lastUpdated = Later(lastUpdated, Later(row.Service.UpdatedAt ?? row.Service.CreatedAt, row.PublishedAt));
        }

        return new PortalCatalog(categories, services, lastUpdated);
    }

    /// <summary>The card of a listed service (unlisted ones are reached through links, so they have a card too).</summary>
    public async Task<ServiceCard> CardAsync(string code, CancellationToken cancellationToken)
    {
        var rows = await ListedAsync(code, cancellationToken);
        if (rows.Count == 0)
        {
            throw new RequestNotFoundException($"Service '{code}' does not exist.");
        }

        var row = rows[0];
        var version = await cache.GetAsync(context, row.VersionId, cancellationToken);
        var category = row.Service.CategoryCode is { } categoryCode
            ? (await CategoriesAsync(cancellationToken)).FirstOrDefault(c => c.Code == categoryCode)
            : null;
        var definition = version.Definition;
        var levels = definition.Levels
            .Select(l => new PortalLevel(l.Code, l.NameAr, l.NameEn, l.DescriptionAr, l.DescriptionEn, l.SlaWorkingDays, Cost(definition, l.Code)))
            .ToList();
        var summary = Summary(row.Service, version) with { RequiredDocument = await DocumentTypeAsync(row.Service.DocumentType, cancellationToken) };
        return new ServiceCard(summary, category, levels, definition.Card, version.Number, row.PublishedAt);
    }

    public async Task<PortalStatistics> StatisticsAsync(CancellationToken cancellationToken)
    {
        var services = (await ListedAsync(null, cancellationToken)).Count;
        var counts = await context.Requests.AsNoTracking()
            .Where(r => r.StatusCategory != StatusCategory.Draft && r.StatusCategory != StatusCategory.Cancelled)
            .GroupBy(r => r.StatusCategory)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        int Count(params StatusCategory[] statuses) => counts.Where(c => statuses.Contains(c.Status)).Sum(c => c.Count);
        return new PortalStatistics(
            services,
            counts.Sum(c => c.Count),
            Count(StatusCategory.Submitted, StatusCategory.InProgress, StatusCategory.PendingCustomer, StatusCategory.PendingExternal, StatusCategory.PendingPayment),
            Count(StatusCategory.Approved, StatusCategory.Rejected, StatusCategory.Closed));
    }

    /// <summary>Cost of a service level (or of the service when <paramref name="level"/> is null and there are no levels).</summary>
    internal static CostRange Cost(VersionDefinition definition, string? level)
    {
        decimal min = 0;
        decimal? max = 0;
        var depends = false;
        foreach (var fee in definition.Fees.Where(f => level is null || f.Levels.Count == 0 || f.Levels.Contains(level)))
        {
            var gross = 1 + (fee.VatIncluded ? 0 : fee.VatRate);
            decimal low, high;
            if (fee.Fixed is { } amount)
            {
                low = high = amount * gross;
            }
            else if (fee.Bracket is { Rows.Count: > 0 } bracket)
            {
                low = bracket.Rows.Min(r => r.Amount) * gross;
                high = bracket.Rows.Max(r => r.Amount) * gross;
                depends |= low != high;
            }
            else
            {
                // A formula: nothing is known until the request is filled in.
                depends = true;
                max = null;
                continue;
            }

            if (fee.When is not null)
            {
                depends = true;
                low = 0;
            }

            min += low;
            max += high;
        }

        return new CostRange(decimal.Round(min, 2), max is { } m ? decimal.Round(m, 2) : null, depends);
    }

    private static PortalServiceSummary Summary(Service service, ResolvedVersion version)
    {
        var definition = version.Definition;
        var days = definition.Levels.Where(l => l.SlaWorkingDays is not null).Select(l => l.SlaWorkingDays!.Value).ToList();
        var levels = definition.Levels.Count == 0 ? [Cost(definition, null)] : definition.Levels.Select(l => Cost(definition, l.Code)).ToList();
        var cost = new CostRange(
            levels.Min(c => c.Min),
            levels.Any(c => c.Max is null) ? null : levels.Max(c => c.Max),
            levels.Any(c => c.DependsOnRequest) || levels.Select(c => (c.Min, c.Max)).Distinct().Count() > 1);
        return new PortalServiceSummary(
            service.Code,
            service.NameAr,
            service.NameEn,
            service.DescriptionAr,
            service.DescriptionEn,
            service.CategoryCode,
            service.Icon,
            service.PartyKinds,
            definition.Card.AudienceAr,
            definition.Card.AudienceEn,
            days.Count == 0 ? null : new WorkingDaysRange(days.Min(), days.Max()),
            cost);
    }

    private async Task<List<PortalCategory>> CategoriesAsync(CancellationToken cancellationToken) =>
        await context.Categories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Code)
            .Select(c => new PortalCategory(c.Code, c.NameAr, c.NameEn, c.DescriptionAr, c.DescriptionEn, c.Icon))
            .ToListAsync(cancellationToken);

    /// <summary>Active services with a published version: the listed ones, or the one named <paramref name="code"/> listed or not.</summary>
    private async Task<List<(Service Service, Guid VersionId, DateTimeOffset? PublishedAt)>> ListedAsync(string? code, CancellationToken cancellationToken)
    {
        var rows = await context.Services.AsNoTracking()
            .Where(s => s.IsActive && (code == null ? s.IsListed : s.Code == code))
            .Select(s => new
            {
                Service = s,
                Version = context.Versions
                    .Where(v => v.ServiceId == s.Id && v.IsActive && !v.IsRetired && v.Resolved != null)
                    .OrderByDescending(v => v.Number)
                    .Select(v => new { v.Id, v.PublishedAt })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
        return [.. rows
            .Where(r => r.Version is not null)
            .OrderBy(r => r.Service.DisplayOrder).ThenBy(r => r.Service.Code)
            .Select(r => (r.Service, r.Version!.Id, r.Version.PublishedAt))];
    }

    private async Task<PortalDocumentType?> DocumentTypeAsync(string? code, CancellationToken cancellationToken) =>
        code is not null && providers.GetService<IDocumentDirectory>() is { } documents && await documents.TypeAsync(code, cancellationToken) is var (ar, en)
            ? new PortalDocumentType(code, ar, en)
            : null;

    private static DateTimeOffset? Later(DateTimeOffset? a, DateTimeOffset? b) => a is null ? b : b is null ? a : a > b ? a : b;
}
