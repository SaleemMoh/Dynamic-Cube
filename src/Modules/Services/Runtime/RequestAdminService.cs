using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Expressions;
using DynamicCube.Platform.Payments;
using DynamicCube.Platform.Visits;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Services.Runtime;

public sealed record RequestPage(IReadOnlyList<RequestSummary> Items, int Total);

public sealed record PreviewRequest(string ServiceCode, VersionDefinition Definition);

/// <summary>Employee-side reads: requests with their timeline, and previews of definitions before they are published.</summary>
internal sealed class RequestAdminService(ServicesDbContext context, ServiceCatalogCache cache, JsonLogicEvaluator evaluator, IServiceProvider services)
{
    /// <param name="app">Only requests for the services of this business app.</param>
    public async Task<RequestPage> SearchAsync(string? service, StatusCategory? status, string? search, int skip, int take, CancellationToken cancellationToken, string? app = null)
    {
        var query = context.Requests.AsNoTracking().Where(r => r.StatusCategory != StatusCategory.Draft);
        if (!string.IsNullOrWhiteSpace(app))
        {
            var services = context.Services.Where(s => s.BusinessApp == app).Select(s => s.Code);
            query = query.Where(r => services.Contains(r.ServiceCode));
        }

        if (!string.IsNullOrWhiteSpace(service))
        {
            query = query.Where(r => r.ServiceCode == service);
        }

        if (status is not null)
        {
            query = query.Where(r => r.StatusCategory == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = "%" + search.Trim().Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
            query = query.Where(r => EF.Functions.ILike(r.Number!, pattern) || EF.Functions.ILike(r.BeneficiaryNameAr, pattern) || EF.Functions.ILike(r.BeneficiaryNameEn, pattern));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(r => r.SubmittedAt).Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200)).ToListAsync(cancellationToken);
        var ids = rows.Select(r => r.ServiceId).Distinct().ToList();
        var names = await context.Services.AsNoTracking().Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => (s.NameAr, s.NameEn), cancellationToken);
        return new RequestPage([.. rows.Select(r => RequestService.Summary(r, names[r.ServiceId]))], total);
    }

    public async Task<RequestDetail> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await context.Requests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id && r.StatusCategory != StatusCategory.Draft, cancellationToken)
            ?? throw new RequestNotFoundException($"Request {id} does not exist.");
        var service = await context.Services.AsNoTracking().SingleAsync(s => s.Id == entity.ServiceId, cancellationToken);
        var version = await cache.GetAsync(context, entity.ServiceVersionId, cancellationToken);
        var timeline = await context.RequestEvents.AsNoTracking().Where(e => e.RequestId == id).OrderBy(e => e.At)
            .Select(e => new TimelineEntry(e.At, e.UserId, e.Type, e.FromStatus, e.ToStatus, e.Comment))
            .ToListAsync(cancellationToken);
        var view = new RequestView(RequestService.Summary(entity, (service.NameAr, service.NameEn)), entity.VersionNumber, entity.LevelCode, entity.CurrentStep,
            JsonNode.Parse(entity.Data)!.AsObject(), entity.Fees, new Dictionary<string, IReadOnlyList<FeeLine>>(), version, [], timeline,
            services.GetService<IPayments>() is { } payments ? await payments.OfSubjectAsync(RequestSubject.Type, entity.Id, cancellationToken) : [],
            services.GetService<IVisits>() is { } visits ? await visits.OfSubjectAsync(RequestSubject.Type, entity.Id, cancellationToken) : []);
        return new RequestDetail(view, entity.SubmitterPartyId, entity.SubmitterName, entity.BeneficiaryPartyId, timeline);
    }

    /// <summary>A published version as the wizard sees it.</summary>
    public async Task<ResolvedVersion> VersionAsync(string serviceCode, int number, CancellationToken cancellationToken)
    {
        var id = await context.Versions.AsNoTracking().Where(v => v.Service!.Code == serviceCode && v.Number == number && v.Resolved != null).Select(v => (Guid?)v.Id).SingleOrDefaultAsync(cancellationToken)
            ?? throw new RequestNotFoundException($"Version {serviceCode}/{number} is not published.");
        return await cache.GetAsync(context, id, cancellationToken);
    }

    /// <summary>Checks a definition being authored and resolves it with the current question bank, for the preview.</summary>
    public async Task<ResolvedVersion> PreviewAsync(PreviewRequest request, CancellationToken cancellationToken)
    {
        var questions = await context.Questions.AsNoTracking().Where(q => q.IsActive).ToDictionaryAsync(q => q.Code, cancellationToken);
        var errors = new ValidationErrors();
        DefinitionValidator.Validate(request.Definition, questions.ContainsKey, evaluator, errors);
        errors.ThrowIfAny();
        var used = request.Definition.Steps.SelectMany(s => s.Sections).SelectMany(s => s.Questions).Select(q => q.Code).Distinct();
        return new ResolvedVersion(request.ServiceCode, 0, request.Definition, used.ToDictionary(c => c, c => QuestionSnapshot.Of(questions[c])));
    }

    internal static string Serialize(ResolvedVersion version) => JsonSerializer.Serialize(version, ChangeJson.Options);
}
