using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Persistence;
using DynamicCube.Platform.Documents;
using DynamicCube.Platform.Integration;
using DynamicCube.Platform.Parties;
using DynamicCube.Platform.Payments;
using DynamicCube.Platform.Persistence;
using DynamicCube.Platform.Security;
using DynamicCube.Platform.Workflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Services.Runtime;

/// <summary>
/// The applicant's side of services: catalog, drafts pinned to a version, saving answers, submission (which starts the
/// service's workflow) and resubmission after a return for correction. Requests belong to their beneficiary: whoever
/// acts for that party (and whose relationship covers the service) sees them. A follow-up service (one with a document
/// type) is requested for one of the beneficiary's documents, within the version's availability, one open request per
/// document at a time.
/// </summary>
internal sealed class RequestService(
    ServicesDbContext context,
    ServiceCatalogCache cache,
    IActingPartyAccessor actingParty,
    IPartyFacts partyFacts,
    Eligibility eligibility,
    FeeCalculator fees,
    FormValidator validator,
    IWorkflowRuntime workflows,
    IServiceWorkflows serviceWorkflows,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    IServiceProvider services,
    TimeProvider clock)
{
    private static readonly StatusCategory[] OpenStatuses = [StatusCategory.Draft, StatusCategory.Submitted, StatusCategory.InProgress, StatusCategory.PendingCustomer, StatusCategory.PendingExternal, StatusCategory.PendingPayment];

    public async Task<IReadOnlyList<CatalogEntry>> CatalogAsync(CancellationToken cancellationToken)
    {
        var acting = await actingParty.GetAsync(cancellationToken);
        var facts = await FactsAsync(acting, refresh: false, cancellationToken);
        var entries = new List<CatalogEntry>();
        foreach (var (service, versionId) in await ListedAsync(cancellationToken))
        {
            var version = await cache.GetAsync(context, versionId, cancellationToken);
            var options = await OptionsAsync(service, version, await HeldDocumentsAsync(service, acting, cancellationToken), cancellationToken);
            var document = await ChosenAsync(options, cancellationToken);
            entries.Add(Entry(service, version, await FailuresAsync(service, version, facts, acting, Context(facts, acting, [], null, document), options, cancellationToken)));
        }

        return entries;
    }

    public async Task<ServiceView> GetServiceAsync(string code, CancellationToken cancellationToken)
    {
        var acting = await actingParty.GetAsync(cancellationToken);
        var (service, versionId) = await CurrentAsync(code, cancellationToken);
        var version = await cache.GetAsync(context, versionId, cancellationToken);
        var facts = await FactsAsync(acting, refresh: false, cancellationToken);
        var options = await OptionsAsync(service, version, await HeldDocumentsAsync(service, acting, cancellationToken), cancellationToken);
        var document = await ChosenAsync(options, cancellationToken);
        var ruleContext = Context(facts, acting, Prefill(version, facts, acting, document), null, document);
        var byLevel = version.Definition.Levels.Count == 0
            ? new Dictionary<string, IReadOnlyList<FeeLine>> { [string.Empty] = fees.Compute(version.Definition, null, ruleContext) }
            : version.Definition.Levels.ToDictionary(l => l.Code, l => fees.Compute(version.Definition, l.Code, WithLevel(ruleContext, l.Code)));
        return new ServiceView(Entry(service, version, await FailuresAsync(service, version, facts, acting, ruleContext, options, cancellationToken)), version, byLevel, options);
    }

    /// <summary>The follow-up services of one of the beneficiary's documents, each available or not, with the reasons.</summary>
    public async Task<IReadOnlyList<FollowUpService>> FollowUpsAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var acting = await actingParty.GetAsync(cancellationToken);
        var document = await Documents.GetAsync(documentId, cancellationToken);
        if (document is null || document.HolderPartyId != acting.PartyId)
        {
            throw new RequestNotFoundException($"Document {documentId} does not exist.");
        }

        var facts = await FactsAsync(acting, refresh: false, cancellationToken);
        var followUps = new List<FollowUpService>();
        var candidates = await context.Services.AsNoTracking().Where(s => s.IsActive && s.DocumentType == document.TypeCode)
            .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Code).ToListAsync(cancellationToken);
        foreach (var service in candidates.Where(s => acting.Covers(s.Code)))
        {
            if (await CurrentVersionIdAsync(service.Id, cancellationToken) is not { } versionId)
            {
                continue;
            }

            var version = await cache.GetAsync(context, versionId, cancellationToken);
            var options = await OptionsAsync(service, version, [document], cancellationToken);
            var failures = await FailuresAsync(service, version, facts, acting, Context(facts, acting, Prefill(version, facts, acting, document), null, document), options, cancellationToken);
            followUps.Add(new FollowUpService(Entry(service, version, failures), options[0]));
        }

        return followUps;
    }

    public async Task<RequestView> CreateAsync(CreateRequest request, CancellationToken cancellationToken)
    {
        var acting = await actingParty.GetAsync(cancellationToken);
        var (service, versionId) = await CurrentAsync(request.ServiceCode, cancellationToken);
        var version = await cache.GetAsync(context, versionId, cancellationToken);
        var facts = await FactsAsync(acting, refresh: true, cancellationToken);
        var document = await RequestedDocumentAsync(service, request.DocumentId, acting, cancellationToken);
        var data = Prefill(version, facts, acting, document);
        var failures = await FailuresAsync(service, version, facts, acting, Context(facts, acting, data, null, document), await OptionsAsync(service, version, document is null ? [] : [document], cancellationToken), cancellationToken);
        if (failures.Count > 0)
        {
            throw new NotEligibleException(failures);
        }

        var individual = acting.IsSelf ? facts : await FactsAsync(acting.IndividualId, cancellationToken);
        var now = clock.GetUtcNow();
        var entity = new ServiceRequest
        {
            ServiceId = service.Id,
            ServiceCode = service.Code,
            ServiceVersionId = versionId,
            VersionNumber = version.Number,
            SubmitterPartyId = acting.IndividualId,
            SubmitterName = individual.NameAr,
            BeneficiaryPartyId = acting.PartyId,
            BeneficiaryNameAr = facts.NameAr,
            BeneficiaryNameEn = facts.NameEn,
            StatusCategory = StatusCategory.Draft,
            DisplayStatus = "DRAFT",
            Data = data.ToJsonString(),
            LevelCode = version.Definition.Levels.Count == 1 ? version.Definition.Levels[0].Code : null,
            CurrentStep = version.Definition.Steps.FirstOrDefault()?.Code,
            DraftExpiresAt = now.AddDays(service.DraftsAllowed ? service.DraftExpiryDays : 1),
            SourceDocumentId = document?.Id,
            SourceDocumentNumber = document?.Number,
        };
        context.Requests.Add(entity);
        AddEvent(entity, RequestEventTypes.Created, null, StatusCategory.Draft, null);
        await context.SaveChangesAsync(cancellationToken);
        return await ViewAsync(entity, service, version, facts, acting, null, cancellationToken, document);
    }

    public async Task<IReadOnlyList<RequestSummary>> ListMineAsync(StatusCategory? status, CancellationToken cancellationToken)
    {
        var acting = await actingParty.GetAsync(cancellationToken);
        var query = context.Requests.AsNoTracking().Where(r => r.BeneficiaryPartyId == acting.PartyId && r.StatusCategory != StatusCategory.Cancelled);
        if (status is not null)
        {
            query = query.Where(r => r.StatusCategory == status);
        }

        var requests = await query.OrderByDescending(r => r.CreatedAt).Take(200).ToListAsync(cancellationToken);
        var names = await ServiceNamesAsync(requests.Select(r => r.ServiceId), cancellationToken);
        return [.. requests.Where(r => acting.Covers(r.ServiceCode)).Select(r => Summary(r, names[r.ServiceId]))];
    }

    public async Task<RequestView> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var (entity, acting) = await LoadMineAsync(id, cancellationToken);
        var service = await context.Services.AsNoTracking().SingleAsync(s => s.Id == entity.ServiceId, cancellationToken);
        if (entity.StatusCategory == StatusCategory.Draft)
        {
            await ExpireOrMoveAsync(entity, service, cancellationToken);
        }

        var version = await cache.GetAsync(context, entity.ServiceVersionId, cancellationToken);
        var facts = await FactsAsync(acting, refresh: false, cancellationToken);
        return await ViewAsync(entity, service, version, facts, acting, null, cancellationToken);
    }

    /// <summary>Saves the answers of a draft as they are (autosave) and reports the problems of the given step.</summary>
    public async Task<RequestView> SaveAsync(Guid id, SaveRequest request, CancellationToken cancellationToken)
    {
        var (entity, acting) = await LoadMineAsync(id, cancellationToken);
        RequireEditable(entity);
        var service = await context.Services.AsNoTracking().SingleAsync(s => s.Id == entity.ServiceId, cancellationToken);
        var version = await cache.GetAsync(context, entity.ServiceVersionId, cancellationToken);
        if (entity.StatusCategory == StatusCategory.PendingCustomer && request.LevelCode is not null && request.LevelCode != entity.LevelCode)
        {
            throw new RequestConflictException("The service level cannot change after submission.");
        }

        if (request.LevelCode is not null && version.Definition.Levels.All(l => l.Code != request.LevelCode))
        {
            throw new RequestValidationException([new FieldError("level", "OPTION", "مستوى الخدمة غير موجود.", "The service level does not exist.", null)]);
        }

        if (request.CurrentStep is not null && version.Definition.Steps.All(s => s.Code != request.CurrentStep))
        {
            throw new RequestConflictException($"Step '{request.CurrentStep}' does not exist.");
        }

        if (request.Data is not null)
        {
            // Only answers to questions of this version are kept.
            var known = version.Questions.Keys.ToHashSet(StringComparer.Ordinal);
            entity.Data = new JsonObject(request.Data.Where(p => known.Contains(p.Key)).Select(p => KeyValuePair.Create(p.Key, p.Value?.DeepClone()))).ToJsonString();
        }

        entity.LevelCode = request.LevelCode ?? entity.LevelCode;
        var facts = await FactsAsync(acting, refresh: false, cancellationToken);
        var document = await SourceDocumentAsync(entity, cancellationToken);
        IReadOnlyList<FieldError> stepErrors = [];
        if (request.ValidateStep is not null)
        {
            var data = JsonNode.Parse(entity.Data)!.AsObject();
            stepErrors = (await validator.CheckAsync(version, data, Context(facts, acting, data, entity.LevelCode, document), request.ValidateStep, cancellationToken)).Errors;
            if (version.Definition.Steps.FirstOrDefault(s => s.Code == request.ValidateStep)?.Type == StepType.Level && entity.LevelCode is null)
            {
                stepErrors = [.. stepErrors, new FieldError("level", "REQUIRED", "اختر مستوى الخدمة.", "Choose a service level.", request.ValidateStep)];
            }
        }

        entity.CurrentStep = stepErrors.Count > 0 ? request.ValidateStep : request.CurrentStep ?? entity.CurrentStep;
        await context.SaveChangesAsync(cancellationToken);
        var view = await ViewAsync(entity, service, version, facts, acting, null, cancellationToken, document);
        return view with { StepErrors = stepErrors };
    }

    public async Task<RequestView> SubmitAsync(Guid id, SubmitRequest request, CancellationToken cancellationToken)
    {
        var (entity, acting) = await LoadMineAsync(id, cancellationToken);
        if (entity.StatusCategory == StatusCategory.PendingCustomer)
        {
            return await ResubmitAsync(entity, acting, cancellationToken);
        }

        RequireDraft(entity);
        var service = await context.Services.AsNoTracking().SingleAsync(s => s.Id == entity.ServiceId, cancellationToken);
        if (await ExpireOrMoveAsync(entity, service, cancellationToken))
        {
            throw new RequestConflictException("The draft has expired; start a new request.");
        }

        var version = await cache.GetAsync(context, entity.ServiceVersionId, cancellationToken);
        var facts = await FactsAsync(acting, refresh: true, cancellationToken);
        var data = JsonNode.Parse(entity.Data)!.AsObject();
        var document = await SourceDocumentAsync(entity, cancellationToken);
        var ruleContext = Context(facts, acting, data, entity.LevelCode, document);
        // The document is checked again: its status or the service's window may have changed since the draft was started.
        var failures = eligibility.Check(service, version, facts, acting, ruleContext).ToList();
        if (entity.SourceDocumentId is not null && document is null)
        {
            throw new RequestConflictException("The document this request was made for no longer exists.");
        }

        if (document is not null && DocumentAvailability.Check(version.Definition.Availability, document, Today) is { IsAvailable: false, Failure: { } closed })
        {
            failures.Add(closed);
        }

        if (failures.Count > 0)
        {
            throw new NotEligibleException(failures);
        }

        var check = await validator.CheckAsync(version, data, ruleContext, null, cancellationToken);
        var errors = check.Errors.ToList();
        if (version.Definition.Levels.Count > 0 && entity.LevelCode is null)
        {
            errors.Add(new FieldError("level", "REQUIRED", "اختر مستوى الخدمة.", "Choose a service level.", version.Definition.Steps.FirstOrDefault(s => s.Type == StepType.Level)?.Code));
        }

        var declaration = version.Definition.Steps.FirstOrDefault(s => s.Type == StepType.Declaration);
        if (declaration is not null && !request.DeclarationAccepted)
        {
            errors.Add(new FieldError("declaration", "REQUIRED", "يجب الموافقة على الإقرار.", "You must accept the declaration.", declaration.Code));
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        // Every request is processed by the service's own workflow (a new service's completes requests without review).
        var workflow = await serviceWorkflows.WorkflowOfAsync(service.Code, cancellationToken);
        if (workflow is null || !await workflows.CanStartAsync(workflow, cancellationToken))
        {
            throw new RequestConflictException("The service cannot take requests yet: its workflow has no published version.");
        }

        var lines = fees.Compute(version.Definition, entity.LevelCode, Context(facts, acting, check.Data, entity.LevelCode, document));
        var now = clock.GetUtcNow();
        entity.Data = check.Data.ToJsonString();
        entity.Fees = [.. lines];
        entity.FeeTotal = lines.Sum(l => l.Total);
        entity.Number = await NextNumberAsync(service.NumberPrefix, now, cancellationToken);
        entity.StatusCategory = StatusCategory.Submitted;
        entity.DisplayStatus = "SUBMITTED";
        entity.SubmittedAt = now;
        entity.DraftExpiresAt = null;
        entity.CurrentStep = null;
        AddEvent(entity, RequestEventTypes.Submitted, StatusCategory.Draft, StatusCategory.Submitted, null);
        RequestNotifications.Raise(context, clock, RequestNotifications.Submitted, entity, service.NameAr, service.NameEn, null);

        // The request and its workflow start together, or not at all.
        await using (var work = await unitOfWork.BeginAsync(cancellationToken))
        {
            await context.SaveChangesAsync(cancellationToken);
            var level = version.Definition.Levels.FirstOrDefault(l => l.Code == entity.LevelCode);
            await workflows.StartAsync(
                new WorkflowStart(workflow, RequestSubject.Type, entity.Id, entity.Number!, service.NameAr, service.NameEn, entity.BeneficiaryNameAr, entity.BeneficiaryNameEn,
                    service.Code, entity.LevelCode, level?.SlaWorkingDays),
                cancellationToken);

            await work.CommitAsync(cancellationToken);
        }

        return await ViewAsync(entity, service, version, facts, acting, null, cancellationToken, document);
    }

    /// <summary>Sends a request returned for correction back: the answers are checked again and the workflow resumes.</summary>
    private async Task<RequestView> ResubmitAsync(ServiceRequest entity, ActingParty acting, CancellationToken cancellationToken)
    {
        var service = await context.Services.AsNoTracking().SingleAsync(s => s.Id == entity.ServiceId, cancellationToken);
        var version = await cache.GetAsync(context, entity.ServiceVersionId, cancellationToken);
        var facts = await FactsAsync(acting, refresh: true, cancellationToken);
        var data = JsonNode.Parse(entity.Data)!.AsObject();
        var document = await SourceDocumentAsync(entity, cancellationToken);
        var check = await validator.CheckAsync(version, data, Context(facts, acting, data, entity.LevelCode, document), null, cancellationToken);
        if (check.Errors.Count > 0)
        {
            throw new RequestValidationException(check.Errors);
        }

        entity.Data = check.Data.ToJsonString();
        entity.CurrentStep = null;
        AddEvent(entity, RequestEventTypes.Resubmitted, StatusCategory.PendingCustomer, StatusCategory.InProgress, null);
        entity.StatusCategory = StatusCategory.InProgress;
        entity.DisplayStatus = "RESUBMITTED";
        RequestNotifications.Raise(context, clock, RequestNotifications.Resubmitted, entity, service.NameAr, service.NameEn, null);
        await using (var work = await unitOfWork.BeginAsync(cancellationToken))
        {
            await context.SaveChangesAsync(cancellationToken);
            await workflows.ResumeAsync(RequestSubject.Type, entity.Id, cancellationToken);
            await work.CommitAsync(cancellationToken);
        }

        return await ViewAsync(entity, service, version, facts, acting, null, cancellationToken, document);
    }

    public async Task CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var (entity, _) = await LoadMineAsync(id, cancellationToken);
        RequireDraft(entity);
        entity.StatusCategory = StatusCategory.Cancelled;
        entity.DisplayStatus = "CANCELLED";
        entity.ClosedAt = clock.GetUtcNow();
        AddEvent(entity, RequestEventTypes.Cancelled, StatusCategory.Draft, StatusCategory.Cancelled, null);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<RequestView> ViewAsync(ServiceRequest entity, Service service, ResolvedVersion version, PartyFacts facts, ActingParty acting, string? step, CancellationToken cancellationToken, DocumentFacts? document = null)
    {
        var data = JsonNode.Parse(entity.Data)!.AsObject();
        if (document is null && entity.SourceDocumentId is not null && entity.StatusCategory is StatusCategory.Draft or StatusCategory.PendingCustomer)
        {
            document = await SourceDocumentAsync(entity, cancellationToken);
        }

        IReadOnlyList<FeeLine> lines = entity.StatusCategory == StatusCategory.Draft
            ? fees.Compute(version.Definition, entity.LevelCode, Context(facts, acting, data, entity.LevelCode, document))
            : entity.Fees;
        var stepErrors = step is null ? [] : (await validator.CheckAsync(version, data, Context(facts, acting, data, entity.LevelCode, document), step, cancellationToken)).Errors;
        var byLevel = entity.StatusCategory == StatusCategory.Draft
            ? version.Definition.Levels.ToDictionary(l => l.Code, l => fees.Compute(version.Definition, l.Code, Context(facts, acting, data, l.Code, document)))
            : new Dictionary<string, IReadOnlyList<FeeLine>>();
        // The applicant sees what happened and the reasons given to them, not which employee did it.
        var me = user.UserId;
        var timeline = await context.RequestEvents.AsNoTracking().Where(e => e.RequestId == entity.Id).OrderBy(e => e.At)
            .Select(e => new TimelineEntry(e.At, e.UserId == me ? e.UserId : string.Empty, e.Type, e.FromStatus, e.ToStatus, e.Comment))
            .ToListAsync(cancellationToken);
        var bills = services.GetService<IPayments>() is { } payments && entity.StatusCategory != StatusCategory.Draft
            ? await payments.OfSubjectAsync(RequestSubject.Type, entity.Id, cancellationToken)
            : [];
        return new RequestView(Summary(entity, (service.NameAr, service.NameEn)), entity.VersionNumber, entity.LevelCode, entity.CurrentStep, data, lines, byLevel, version, stepErrors, timeline, bills);
    }

    /// <summary>Expires a draft past its date, or moves it to the current version when the service says so. True when expired.</summary>
    private async Task<bool> ExpireOrMoveAsync(ServiceRequest entity, Service service, CancellationToken cancellationToken)
    {
        if (entity.DraftExpiresAt <= clock.GetUtcNow())
        {
            entity.StatusCategory = StatusCategory.Cancelled;
            entity.DisplayStatus = "EXPIRED";
            entity.ClosedAt = clock.GetUtcNow();
            AddEvent(entity, RequestEventTypes.Expired, StatusCategory.Draft, StatusCategory.Cancelled, null);
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }

        if (service.OnNewVersion != NewVersionPolicy.Restart)
        {
            return false;
        }

        var current = await CurrentVersionIdAsync(service.Id, cancellationToken);
        if (current is null || current == entity.ServiceVersionId)
        {
            return false;
        }

        var version = await cache.GetAsync(context, current.Value, cancellationToken);
        var old = JsonNode.Parse(entity.Data)!.AsObject();
        entity.Data = new JsonObject(old.Where(p => version.Questions.ContainsKey(p.Key)).Select(p => KeyValuePair.Create(p.Key, p.Value?.DeepClone()))).ToJsonString();
        AddEvent(entity, RequestEventTypes.VersionChanged, StatusCategory.Draft, StatusCategory.Draft, $"{entity.VersionNumber} → {version.Number}");
        entity.ServiceVersionId = current.Value;
        entity.VersionNumber = version.Number;
        entity.LevelCode = version.Definition.Levels.Any(l => l.Code == entity.LevelCode) ? entity.LevelCode : null;
        entity.CurrentStep = version.Definition.Steps.FirstOrDefault()?.Code;
        await context.SaveChangesAsync(cancellationToken);
        return false;
    }

    private async Task<(ServiceRequest Entity, ActingParty Acting)> LoadMineAsync(Guid id, CancellationToken cancellationToken)
    {
        var acting = await actingParty.GetAsync(cancellationToken);
        var entity = await context.Requests.SingleOrDefaultAsync(r => r.Id == id && r.BeneficiaryPartyId == acting.PartyId, cancellationToken);
        return entity is null || !acting.Covers(entity.ServiceCode)
            ? throw new RequestNotFoundException($"Request {id} does not exist.")
            : (entity, acting);
    }

    /// <summary>Drafts, and requests returned to the applicant for correction, can be edited.</summary>
    private static void RequireEditable(ServiceRequest entity)
    {
        if (entity.StatusCategory is not (StatusCategory.Draft or StatusCategory.PendingCustomer))
        {
            throw new RequestConflictException("Only drafts and requests returned for correction can be changed.");
        }
    }

    private static void RequireDraft(ServiceRequest entity)
    {
        if (entity.StatusCategory != StatusCategory.Draft)
        {
            throw new RequestConflictException("Only drafts can be changed.");
        }
    }

    private async Task<List<(Service Service, Guid VersionId)>> ListedAsync(CancellationToken cancellationToken)
    {
        var rows = await context.Services.AsNoTracking()
            .Where(s => s.IsActive && s.IsListed)
            .Select(s => new
            {
                Service = s,
                VersionId = context.Versions.Where(v => v.ServiceId == s.Id && v.IsActive && !v.IsRetired && v.Resolved != null).OrderByDescending(v => v.Number).Select(v => (Guid?)v.Id).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
        return [.. rows.Where(r => r.VersionId is not null).OrderBy(r => r.Service.DisplayOrder).ThenBy(r => r.Service.Code).Select(r => (r.Service, r.VersionId!.Value))];
    }

    private async Task<(Service Service, Guid VersionId)> CurrentAsync(string code, CancellationToken cancellationToken)
    {
        var service = await context.Services.AsNoTracking().SingleOrDefaultAsync(s => s.Code == code && s.IsActive, cancellationToken)
            ?? throw new RequestNotFoundException($"Service '{code}' does not exist.");
        var version = await CurrentVersionIdAsync(service.Id, cancellationToken) ?? throw new RequestNotFoundException($"Service '{code}' has no published version.");
        return (service, version);
    }

    private Task<Guid?> CurrentVersionIdAsync(Guid serviceId, CancellationToken cancellationToken) =>
        context.Versions.AsNoTracking()
            .Where(v => v.ServiceId == serviceId && v.IsActive && !v.IsRetired && v.Resolved != null)
            .OrderByDescending(v => v.Number)
            .Select(v => (Guid?)v.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<PartyFacts> FactsAsync(ActingParty acting, bool refresh, CancellationToken cancellationToken) =>
        await partyFacts.GetAsync(acting.PartyId, refresh, cancellationToken) ?? throw new RequestNotFoundException("The acting party does not exist.");

    private async Task<PartyFacts> FactsAsync(Guid partyId, CancellationToken cancellationToken) =>
        await partyFacts.GetAsync(partyId, false, cancellationToken) ?? throw new RequestNotFoundException("The applicant does not exist.");

    private JsonObject Context(PartyFacts facts, ActingParty acting, JsonObject data, string? level, DocumentFacts? document = null) =>
        RuleContext.Build(facts, acting, data, level, clock.GetUtcNow(), document);

    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    private IDocumentDirectory Documents => services.GetService<IDocumentDirectory>() ?? throw new RequestConflictException("Documents are not installed.");

    /// <summary>The beneficiary's documents a follow-up service can be requested for (none for other services).</summary>
    private async Task<IReadOnlyList<DocumentFacts>> HeldDocumentsAsync(Service service, ActingParty acting, CancellationToken cancellationToken) =>
        service.DocumentType is null ? [] : await Documents.OfHolderAsync(acting.PartyId, service.DocumentType, cancellationToken);

    private async Task<DocumentFacts?> SourceDocumentAsync(ServiceRequest entity, CancellationToken cancellationToken) =>
        entity.SourceDocumentId is { } id ? await Documents.GetAsync(id, cancellationToken) : null;

    /// <summary>The document a new follow-up request names: required, the beneficiary's own and of the service's type.</summary>
    private async Task<DocumentFacts?> RequestedDocumentAsync(Service service, Guid? documentId, ActingParty acting, CancellationToken cancellationToken)
    {
        if (service.DocumentType is null)
        {
            return documentId is null ? null : throw new RequestConflictException($"Service '{service.Code}' is not requested for a document.");
        }

        if (documentId is not { } id)
        {
            throw new RequestValidationException([new FieldError("document", "REQUIRED", "اختر الوثيقة التي تقدم الطلب عليها.", "Choose the document the request is for.", null)]);
        }

        var document = await Documents.GetAsync(id, cancellationToken);
        return document is null || document.HolderPartyId != acting.PartyId || document.TypeCode != service.DocumentType
            ? throw new RequestNotFoundException($"Document {id} does not exist.")
            : document;
    }

    /// <summary>Each document with the service's availability on it and any request already open for it.</summary>
    private async Task<List<DocumentOption>> OptionsAsync(Service service, ResolvedVersion version, IReadOnlyList<DocumentFacts> documents, CancellationToken cancellationToken)
    {
        if (documents.Count == 0)
        {
            return [];
        }

        var ids = documents.Select(d => (Guid?)d.Id).ToList();
        var open = await context.Requests.AsNoTracking()
            .Where(r => r.ServiceId == service.Id && ids.Contains(r.SourceDocumentId) && OpenStatuses.Contains(r.StatusCategory))
            .Select(r => new { r.Id, r.Number, r.SourceDocumentId })
            .ToListAsync(cancellationToken);
        var today = Today;
        return [.. documents.Select(d =>
        {
            var availability = DocumentAvailability.Check(version.Definition.Availability, d, today);
            var request = open.FirstOrDefault(o => o.SourceDocumentId == d.Id);
            List<EligibilityFailure> reasons = availability.Failure is { } failure ? [failure] : [];
            if (request is not null)
            {
                reasons.Add(new("OPEN_REQUEST", "يوجد طلب قائم لهذه الخدمة على هذه الوثيقة.", "A request for this service is already open on this document."));
            }

            return new DocumentOption(d.Id, d.Number, d.Status, DateOnly.FromDateTime(d.IssuedAt.UtcDateTime), d.ExpiresAt is { } expires ? DateOnly.FromDateTime(expires.UtcDateTime) : null,
                reasons.Count == 0, availability.OpensOn, availability.ClosesOn, reasons, request?.Id, request?.Number);
        })];
    }

    /// <summary>The document rules are shown with before one is chosen: the first available, else the newest.</summary>
    private async Task<DocumentFacts?> ChosenAsync(List<DocumentOption> options, CancellationToken cancellationToken) =>
        options.Count == 0 ? null : await Documents.GetAsync((options.FirstOrDefault(o => o.IsAvailable) ?? options[0]).Id, cancellationToken);

    /// <summary>Eligibility, then for a follow-up service the documents: one is needed and it must be available.</summary>
    private async Task<List<EligibilityFailure>> FailuresAsync(Service service, ResolvedVersion version, PartyFacts facts, ActingParty acting, JsonObject ruleContext, List<DocumentOption> options, CancellationToken cancellationToken)
    {
        var failures = eligibility.Check(service, version, facts, acting, ruleContext).ToList();
        if (service.DocumentType is null)
        {
            return failures;
        }

        if (options.Count == 0)
        {
            var (ar, en) = await Documents.TypeAsync(service.DocumentType, cancellationToken) ?? (service.DocumentType, service.DocumentType);
            failures.Add(new("NO_DOCUMENT", $"تتطلب هذه الخدمة وجود {ar}.", $"This service needs a {en}."));
        }
        else if (!options.Any(o => o.IsAvailable))
        {
            failures.AddRange(options[0].Reasons);
        }

        return failures;
    }

    private static JsonObject WithLevel(JsonObject context, string level)
    {
        var copy = context.DeepClone().AsObject();
        copy["level"] = level;
        return copy;
    }

    /// <summary>Initial answers from the party's data (and the document a follow-up request is for) for questions with a prefill path.</summary>
    private JsonObject Prefill(ResolvedVersion version, PartyFacts facts, ActingParty acting, DocumentFacts? document = null)
    {
        var context = Context(facts, acting, [], null, document);
        var data = new JsonObject();
        foreach (var question in version.Questions.Values.Where(q => q.Prefill is not null))
        {
            var value = JsonPaths.Read(context, question.Prefill);
            if (value is not null)
            {
                data[question.Code] = value;
            }
        }

        return data;
    }

    private async Task<string> NextNumberAsync(string prefix, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var year = now.UtcDateTime.Year.ToString(CultureInfo.InvariantCulture);
        var key = $"{prefix}-{year}";
        var value = await context.Database
            .SqlQuery<long>($"INSERT INTO svc.number_sequence (key, value) VALUES ({key}, 1) ON CONFLICT (key) DO UPDATE SET value = svc.number_sequence.value + 1 RETURNING value AS \"Value\"")
            .ToListAsync(cancellationToken);
        return $"{key}-{value[0].ToString("D6", CultureInfo.InvariantCulture)}";
    }

    private void AddEvent(ServiceRequest entity, string type, StatusCategory? from, StatusCategory? to, string? comment) =>
        context.RequestEvents.Add(new RequestEvent { RequestId = entity.Id, At = clock.GetUtcNow(), UserId = user.UserId, Type = type, FromStatus = from, ToStatus = to, Comment = comment });

    private async Task<Dictionary<Guid, (string NameAr, string NameEn)>> ServiceNamesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var list = ids.Distinct().ToList();
        return await context.Services.AsNoTracking().Where(s => list.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => (s.NameAr, s.NameEn), cancellationToken);
    }

    internal static RequestSummary Summary(ServiceRequest r, (string NameAr, string NameEn) service) =>
        new(r.Id, r.Number, r.ServiceCode, service.NameAr, service.NameEn, r.StatusCategory, r.DisplayStatus, r.BeneficiaryNameAr, r.BeneficiaryNameEn, r.FeeTotal, r.CreatedAt, r.SubmittedAt, r.DraftExpiresAt, r.SourceDocumentNumber);

    private static CatalogEntry Entry(Service service, ResolvedVersion version, List<EligibilityFailure> failures) =>
        new(service.Code, service.NameAr, service.NameEn, service.DescriptionAr, service.DescriptionEn, service.Archetype, service.CategoryCode, service.Icon, service.PartyKinds, version.Number, failures.Count == 0, failures, service.DocumentType);
}
