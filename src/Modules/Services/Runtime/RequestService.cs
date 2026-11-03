using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Persistence;
using DynamicCube.Platform.Documents;
using DynamicCube.Platform.Files;
using DynamicCube.Platform.Integration;
using DynamicCube.Platform.Parties;
using DynamicCube.Platform.Payments;
using DynamicCube.Platform.Persistence;
using DynamicCube.Platform.Security;
using DynamicCube.Platform.Visits;
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
    IFileStorage storage,
    PortalChannel portal,
    RequestIntegrations integrations,
    Prerequisites prerequisites,
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
        // Eligibility is shown here, so profile data that is due (e.g. never fetched for this establishment) is fetched first.
        var facts = await FactsAsync(acting, refresh: true, cancellationToken);
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
            if (!await portal.OffersAsync(service, cancellationToken) || await CurrentVersionIdAsync(service.Id, cancellationToken) is not { } versionId)
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

        // Answers fetched from integrations (e.g. the capital from the commercial register); the calls are the request's evidence.
        var requestId = Guid.CreateVersion7();
        using (IntegrationSubject.Use(RequestSubject.Type, requestId))
        {
            await integrations.FillAsync(version, Context(facts, acting, data, null, document), data, lockedOnly: false, cancellationToken);
        }

        var individual = acting.IsSelf ? facts : await FactsAsync(acting.IndividualId, cancellationToken);
        var now = clock.GetUtcNow();
        var entity = new ServiceRequest
        {
            Id = requestId,
            ServiceId = service.Id,
            ServiceCode = service.Code,
            ServiceVersionId = versionId,
            VersionNumber = version.Number,
            SubmitterPartyId = acting.IndividualId,
            SubmitterName = individual.NameAr,
            BeneficiaryPartyId = acting.PartyId,
            ActingRelationship = acting.IsSelf ? null : acting.RelationshipCode,
            ActingAuthority = acting.IsSelf ? null : acting.Authority,
            BeneficiaryNameAr = facts.NameAr,
            BeneficiaryNameEn = facts.NameEn,
            StatusCategory = StatusCategory.Draft,
            DisplayStatus = "DRAFT",
            Data = data.ToJsonString(),
            LevelCode = version.Definition.Levels.Count == 1 ? version.Definition.Levels[0].Code : version.Definition.Levels.FirstOrDefault(l => l.IsDefault)?.Code,
            CurrentStep = version.Definition.Steps.FirstOrDefault()?.Code,
            DraftExpiresAt = now.AddDays(service.DraftsAllowed ? service.DraftExpiryDays : 1),
            SourceDocumentId = document?.Id,
            SourceDocumentNumber = document?.Number,
        };
        (entity.CompanyCode, entity.BranchCode) = await PlaceAsync(service, data, cancellationToken);
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

        // A draft reads its integrations again when opened: the providers' data may have changed since it was saved, and the
        // applicant is told which answers changed.
        IReadOnlyList<AnswerChange> changes = [];
        if (entity.StatusCategory == StatusCategory.Draft
            && (version.Questions.Values.Any(q => q.Locked && q.Integration is not null) || (JsonNode.Parse(entity.Integrations) as JsonObject)?.Count > 0))
        {
            var data = JsonNode.Parse(entity.Data)!.AsObject();
            var runs = JsonNode.Parse(entity.Integrations) as JsonObject ?? [];
            var document = await SourceDocumentAsync(entity, cancellationToken);
            using var evidence = IntegrationSubject.Use(RequestSubject.Type, entity.Id);
            changes = await integrations.RefreshAsync(version, Context(facts, acting, data, entity.LevelCode, document), data, runs, cancellationToken);
            entity.Data = data.ToJsonString();
            entity.Integrations = runs.ToJsonString();
            await context.SaveChangesAsync(cancellationToken);
        }

        return await ViewAsync(entity, service, version, facts, acting, null, cancellationToken, changes: changes);
    }

    /// <summary>
    /// The live result beside the form (ADR-0040): the version's result panel evaluated on the answers as they are on screen,
    /// while the request is being filled (a draft, or returned to the applicant). Nothing is saved or recorded.
    /// </summary>
    public async Task<LiveResultView> LiveResultAsync(Guid id, LiveResultRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (entity, _) = await LoadMineAsync(id, cancellationToken);
        if (entity.StatusCategory is not (StatusCategory.Draft or StatusCategory.PendingCustomer))
        {
            throw new RequestConflictException("The live result is shown only while the request is being filled.");
        }

        var version = await cache.GetAsync(context, entity.ServiceVersionId, cancellationToken);
        if (version.Definition.Panel?.Result is not { Enabled: true, Step: { Length: > 0 } step })
        {
            throw new RequestConflictException("This service shows no live result.");
        }

        var subject = services.GetRequiredService<RequestSubject>();
        var data = request.Data ?? JsonNode.Parse(entity.Data)!.AsObject();
        var facts = await subject.RuleContextAsync(entity, data, request.Data is null ? entity.LevelCode : request.LevelCode, cancellationToken);
        return await services.GetRequiredService<LiveResult>().EvaluateAsync(entity.ServiceCode, step, facts, cancellationToken);
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
            var answers = new JsonObject(request.Data.Where(p => known.Contains(p.Key)).Select(p => KeyValuePair.Create(p.Key, p.Value?.DeepClone())));
            RequestFiles.KeepOwnFiles(version, answers, await context.RequestFiles.AsNoTracking().Where(f => f.RequestId == entity.Id).ToListAsync(cancellationToken));
            entity.Data = answers.ToJsonString();
        }

        entity.LevelCode = request.LevelCode ?? entity.LevelCode;
        var facts = await FactsAsync(acting, refresh: false, cancellationToken);
        var document = await SourceDocumentAsync(entity, cancellationToken);
        IReadOnlyList<FieldError> stepErrors = [];

        // Answers the applicant may not change always hold the integration's value. An integration step calls its operation
        // when the applicant arrives on it (its result card shows the outcome) and, when leaving, again if it did not succeed
        // or the applicant asks to try again; the outputs go into the answers.
        var current = JsonNode.Parse(entity.Data)!.AsObject();
        using var evidence = IntegrationSubject.Use(RequestSubject.Type, entity.Id);
        await integrations.FillAsync(version, Context(facts, acting, current, entity.LevelCode, document), current, lockedOnly: true, cancellationToken);
        var runs = JsonNode.Parse(entity.Integrations) as JsonObject ?? [];
        if (request.ValidateStep is not null && version.Definition.Steps.FirstOrDefault(s => s.Code == request.ValidateStep) is { Type: StepType.Integration, Integration: { } call } step)
        {
            var last = runs[step.Code]?.Deserialize<IntegrationStepRun>(ChangeJson.Options);
            var run = last is { Success: true } && request.CurrentStep != request.ValidateStep
                ? last
                : await integrations.RunStepAsync(call, Context(facts, acting, current, entity.LevelCode, document), current, cancellationToken);
            runs[step.Code] = JsonSerializer.SerializeToNode(run, ChangeJson.Options);
            if (!run.Success && call.Required)
            {
                stepErrors = [IntegrationFailed(step, run)];
            }
        }

        entity.Data = current.ToJsonString();
        if (request.ValidateStep is not null && stepErrors.Count == 0)
        {
            var data = JsonNode.Parse(entity.Data)!.AsObject();
            stepErrors = (await validator.CheckAsync(version, data, Context(facts, acting, data, entity.LevelCode, document), request.ValidateStep, cancellationToken)).Errors;
            if (version.Definition.Steps.FirstOrDefault(s => s.Code == request.ValidateStep)?.Type == StepType.Level && entity.LevelCode is null)
            {
                stepErrors = [.. stepErrors, new FieldError("level", "REQUIRED", "اختر مستوى الخدمة.", "Choose a service level.", request.ValidateStep)];
            }
        }

        // Arriving on an integration step: its operation is called now, so the step shows the result.
        if (stepErrors.Count == 0 && request.CurrentStep is not null && request.CurrentStep != request.ValidateStep
            && version.Definition.Steps.FirstOrDefault(s => s.Code == request.CurrentStep) is { Type: StepType.Integration, Integration: { } arriving } target)
        {
            var arrived = JsonNode.Parse(entity.Data)!.AsObject();
            var run = await integrations.RunStepAsync(arriving, Context(facts, acting, arrived, entity.LevelCode, document), arrived, cancellationToken);
            runs[target.Code] = JsonSerializer.SerializeToNode(run, ChangeJson.Options);
            entity.Data = arrived.ToJsonString();
        }

        entity.Integrations = runs.ToJsonString();
        entity.CurrentStep = stepErrors.Count > 0 ? request.ValidateStep : request.CurrentStep ?? entity.CurrentStep;
        if (entity.StatusCategory == StatusCategory.Draft)
        {
            (entity.CompanyCode, entity.BranchCode) = await PlaceAsync(service, JsonNode.Parse(entity.Data)!.AsObject(), cancellationToken);
        }

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

        failures.AddRange(await prerequisites.CheckAsync(version.Definition, acting.PartyId, cancellationToken));

        // The services requested with this one: the required ones and those the applicant ticked, each open to them now.
        var bundled = new List<(Service Service, Guid VersionId, ResolvedVersion Version)>();
        foreach (var item in version.Definition.Bundle.Where(b => b.Required || (request.Bundle ?? []).Contains(b.Service, StringComparer.Ordinal)))
        {
            var (other, otherVersionId) = await CurrentAsync(item.Service, cancellationToken);
            var otherVersion = await cache.GetAsync(context, otherVersionId, cancellationToken);
            var otherFailures = eligibility.Check(other, otherVersion, facts, acting, Context(facts, acting, data, null)).ToList();
            otherFailures.AddRange(await prerequisites.CheckAsync(otherVersion.Definition, acting.PartyId, cancellationToken));
            if (!acting.Covers(other.Code))
            {
                otherFailures.Add(new("NOT_COVERED", "لا تغطي صلاحيتك هذه الخدمة.", "Your relationship does not cover this service."));
            }

            failures.AddRange(otherFailures.Select(f => f with { Code = $"BUNDLE_{other.Code}_{f.Code}", MessageAr = $"{other.NameAr}: {f.MessageAr}", MessageEn = $"{other.NameEn}: {f.MessageEn}" }));
            bundled.Add((other, otherVersionId, otherVersion));
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

        // Versions that ask for it: the applicant confirmed the request with Nafath, as themselves, a moment ago.
        Platform.Nafath.NafathCheck? nafath = null;
        if (version.Definition.ConfirmWithNafath)
        {
            nafath = await NafathConfirmationAsync(entity, acting, request.NafathCheckId, cancellationToken);
            if (nafath is null)
            {
                errors.Add(new FieldError("nafath", "REQUIRED", "يجب تأكيد الطلب عبر نفاذ.", "Confirm the request with Nafath.", null));
            }
        }

        // Required integration steps must have succeeded.
        var runs = JsonNode.Parse(entity.Integrations) as JsonObject;
        foreach (var step in version.Definition.Steps.Where(s => s is { Type: StepType.Integration, Integration.Required: true }))
        {
            var run = runs?[step.Code]?.Deserialize<IntegrationStepRun>(ChangeJson.Options);
            if (run is not { Success: true })
            {
                errors.Add(IntegrationFailed(step, run));
            }
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
        (entity.CompanyCode, entity.BranchCode) = await PlaceAsync(service, check.Data, cancellationToken);
        entity.StatusCategory = StatusCategory.Submitted;
        entity.DisplayStatus = "SUBMITTED";
        entity.SubmittedAt = now;
        entity.DraftExpiresAt = null;
        entity.CurrentStep = null;
        if (nafath is not null)
        {
            entity.NafathCheckId = nafath.Id;
            entity.NafathConfirmedAt = nafath.CompletedAt;
            AddEvent(entity, RequestEventTypes.NafathConfirmed, StatusCategory.Draft, StatusCategory.Draft, null);
        }

        AddEvent(entity, RequestEventTypes.Submitted, StatusCategory.Draft, StatusCategory.Submitted, null);
        RequestNotifications.Raise(context, clock, RequestNotifications.Submitted, entity, service.NameAr, service.NameEn, null);

        // The request and its workflow start together, or not at all.
        await using (var work = await unitOfWork.BeginAsync(cancellationToken))
        {
            await context.SaveChangesAsync(cancellationToken);
            var level = version.Definition.Levels.FirstOrDefault(l => l.Code == entity.LevelCode);
            await workflows.StartAsync(
                new WorkflowStart(workflow, RequestSubject.Type, entity.Id, entity.Number!, service.NameAr, service.NameEn, entity.BeneficiaryNameAr, entity.BeneficiaryNameEn,
                    service.Code, entity.LevelCode, level?.SlaWorkingDays, entity.CompanyCode, entity.BranchCode),
                cancellationToken);

            // The bundle, with the same answers: submitted when they are complete, otherwise drafts for the applicant.
            foreach (var (other, otherVersionId, otherVersion) in bundled)
            {
                await CreateLinkedAsync(other, otherVersionId, otherVersion, facts, acting, entity, RequestLinkKind.Bundled, null, [], submit: true, null, cancellationToken);
            }

            await work.CommitAsync(cancellationToken);
        }

        return await ViewAsync(entity, service, version, facts, acting, null, cancellationToken, document);
    }

    /// <summary>
    /// A request a workflow step asks for (3f): for the parent's applicant, with the parent's shared answers and the step's
    /// data; submitted at once when the step says so and its answers are complete, otherwise a draft for the applicant.
    /// </summary>
    public async Task<ServiceRequest> SpawnAsync(Platform.Services.SpawnRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var parent = await context.Requests.SingleOrDefaultAsync(r => r.Id == request.ParentId, cancellationToken)
            ?? throw new InvalidOperationException($"There is no request {request.ParentId}.");
        var service = await context.Services.AsNoTracking().SingleOrDefaultAsync(s => s.Code == request.ServiceCode && s.IsActive, cancellationToken)
            ?? throw new InvalidOperationException($"There is no active service {request.ServiceCode}.");
        var versionId = await CurrentVersionIdAsync(service.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Service {request.ServiceCode} has no published version.");
        var version = await cache.GetAsync(context, versionId, cancellationToken);
        var facts = await FactsAsync(parent.BeneficiaryPartyId, cancellationToken);
        var acting = new ActingParty(parent.SubmitterPartyId, parent.BeneficiaryPartyId, facts.Kind, facts.NameAr, facts.NameEn, null, []);
        return await CreateLinkedAsync(service, versionId, version, facts, acting, parent, RequestLinkKind.Spawned, request.Step, request.Data, request.Submit, request.RequestId, cancellationToken);
    }

    /// <summary>Whether steps may make requests of the service: it exists and is active.</summary>
    public Task<bool> ServiceExistsAsync(string code, CancellationToken cancellationToken) =>
        context.Services.AsNoTracking().AnyAsync(s => s.Code == code && s.IsActive, cancellationToken);

    /// <summary>
    /// A request made with or from another (3f), for the same applicant: the party's prefilled answers, the parent's answers
    /// to the questions both forms share, then <paramref name="extra"/>. With <paramref name="submit"/> it is submitted when
    /// nothing is left for the applicant (answers complete and valid, no level to choose, no declaration, no Nafath
    /// confirmation, no required integration step); otherwise it is a draft the applicant is told to complete.
    /// </summary>
    private async Task<ServiceRequest> CreateLinkedAsync(
        Service service,
        Guid versionId,
        ResolvedVersion version,
        PartyFacts facts,
        ActingParty acting,
        ServiceRequest parent,
        RequestLinkKind kind,
        string? step,
        JsonObject extra,
        bool submit,
        Guid? id,
        CancellationToken cancellationToken)
    {
        var data = Prefill(version, facts, acting);
        foreach (var (key, value) in JsonNode.Parse(parent.Data)!.AsObject().Where(p => version.Questions.ContainsKey(p.Key)))
        {
            data[key] = value?.DeepClone();
        }

        foreach (var (key, value) in extra)
        {
            data[key] = value?.DeepClone();
        }

        var newId = id ?? Guid.CreateVersion7();
        using (IntegrationSubject.Use(RequestSubject.Type, newId))
        {
            await integrations.FillAsync(version, Context(facts, acting, data, null), data, lockedOnly: false, cancellationToken);
        }

        var now = clock.GetUtcNow();
        var entity = new ServiceRequest
        {
            ServiceId = service.Id,
            ServiceCode = service.Code,
            ServiceVersionId = versionId,
            VersionNumber = version.Number,
            SubmitterPartyId = parent.SubmitterPartyId,
            SubmitterName = parent.SubmitterName,
            BeneficiaryPartyId = parent.BeneficiaryPartyId,
            BeneficiaryNameAr = parent.BeneficiaryNameAr,
            BeneficiaryNameEn = parent.BeneficiaryNameEn,
            StatusCategory = StatusCategory.Draft,
            DisplayStatus = "DRAFT",
            Data = data.ToJsonString(),
            LevelCode = version.Definition.Levels.Count == 1 ? version.Definition.Levels[0].Code : version.Definition.Levels.FirstOrDefault(l => l.IsDefault)?.Code,
            CurrentStep = version.Definition.Steps.FirstOrDefault()?.Code,
            DraftExpiresAt = now.AddDays(Math.Max(1, service.DraftExpiryDays)),
            ParentRequestId = parent.Id,
            LinkKind = kind,
            ParentStep = step,
        };
        entity.Id = newId;

        var (company, branch) = await PlaceAsync(service, data, cancellationToken);
        (entity.CompanyCode, entity.BranchCode) = (company ?? parent.CompanyCode, branch ?? parent.BranchCode);
        context.Requests.Add(entity);
        AddEvent(entity, RequestEventTypes.Created, null, StatusCategory.Draft, parent.Number);

        var ruleContext = Context(facts, acting, data, entity.LevelCode);
        var check = await validator.CheckAsync(version, data, ruleContext, null, cancellationToken);
        var complete = submit
            && check.Errors.Count == 0
            && (version.Definition.Levels.Count == 0 || entity.LevelCode is not null)
            && version.Definition.Steps.All(s => s.Type != StepType.Declaration && s is not { Type: StepType.Integration, Integration.Required: true })
            && !version.Definition.ConfirmWithNafath;
        var workflow = complete ? await serviceWorkflows.WorkflowOfAsync(service.Code, cancellationToken) : null;
        if (workflow is null || !await workflows.CanStartAsync(workflow, cancellationToken))
        {
            // The applicant completes it: it waits in their requests, and they are told.
            await context.SaveChangesAsync(cancellationToken);
            RequestNotifications.Raise(context, clock, RequestNotifications.Opened, entity, service.NameAr, service.NameEn, parent.Number);
            await context.SaveChangesAsync(cancellationToken);
            return entity;
        }

        var lines = fees.Compute(version.Definition, entity.LevelCode, Context(facts, acting, check.Data, entity.LevelCode));
        entity.Data = check.Data.ToJsonString();
        entity.Fees = [.. lines];
        entity.FeeTotal = lines.Sum(l => l.Total);
        entity.Number = await NextNumberAsync(service.NumberPrefix, now, cancellationToken);
        entity.StatusCategory = StatusCategory.Submitted;
        entity.DisplayStatus = "SUBMITTED";
        entity.SubmittedAt = now;
        entity.DraftExpiresAt = null;
        entity.CurrentStep = null;
        AddEvent(entity, RequestEventTypes.Submitted, StatusCategory.Draft, StatusCategory.Submitted, parent.Number);
        RequestNotifications.Raise(context, clock, RequestNotifications.Submitted, entity, service.NameAr, service.NameEn, null);
        await context.SaveChangesAsync(cancellationToken);
        var level = version.Definition.Levels.FirstOrDefault(l => l.Code == entity.LevelCode);
        await workflows.StartAsync(
            new WorkflowStart(workflow, RequestSubject.Type, entity.Id, entity.Number, service.NameAr, service.NameEn, entity.BeneficiaryNameAr, entity.BeneficiaryNameEn,
                service.Code, entity.LevelCode, level?.SlaWorkingDays, entity.CompanyCode, entity.BranchCode),
            cancellationToken);
        return entity;
    }

    /// <summary>Sends the applicant's Nafath app a request to confirm this draft (versions that ask for it, ADR-0043).</summary>
    public async Task<NafathChallengeView> StartNafathAsync(Guid id, string? userIp, CancellationToken cancellationToken)
    {
        var (entity, acting) = await LoadMineAsync(id, cancellationToken);
        RequireDraft(entity);
        var version = await cache.GetAsync(context, entity.ServiceVersionId, cancellationToken);
        if (!version.Definition.ConfirmWithNafath)
        {
            throw new RequestConflictException("This service does not ask for a Nafath confirmation.");
        }

        var nafath = Nafath();
        var challenge = await nafath.StartAsync(await ApplicantIdAsync(acting, cancellationToken), null, RequestSubject.Type, entity.Id, userIp, cancellationToken);
        return new NafathChallengeView(challenge.Id, challenge.Random, challenge.ExpiresAt);
    }

    /// <summary>Where the applicant's Nafath confirmation of this request stands.</summary>
    public async Task<NafathCheckView> NafathStatusAsync(Guid id, Guid checkId, string? userIp, CancellationToken cancellationToken)
    {
        var (entity, _) = await LoadMineAsync(id, cancellationToken);
        var check = await Nafath().CheckAsync(checkId, userIp, cancellationToken);
        return check is null || check.SubjectId != entity.Id || check.SubjectType != RequestSubject.Type
            ? throw new RequestNotFoundException($"There is no Nafath confirmation {checkId} of this request.")
            : new NafathCheckView(check.Id, check.Status.ToString().ToUpperInvariant(), check.Random, check.ExpiresAt);
    }

    /// <summary>The verification, when it confirms this request, by its applicant, within the last fifteen minutes.</summary>
    private async Task<Platform.Nafath.NafathCheck?> NafathConfirmationAsync(ServiceRequest entity, ActingParty acting, Guid? checkId, CancellationToken cancellationToken)
    {
        if (checkId is null || services.GetService<Platform.Nafath.INafathVerifications>() is not { } nafath)
        {
            return null;
        }

        var check = await nafath.CheckAsync(checkId.Value, null, cancellationToken);
        if (check is not { Status: Platform.Nafath.NafathStatus.Verified, Person: not null, CompletedAt: not null }
            || check.SubjectId != entity.Id || check.SubjectType != RequestSubject.Type
            || clock.GetUtcNow() - check.CompletedAt > TimeSpan.FromMinutes(15))
        {
            return null;
        }

        return string.Equals(check.Person.Identifier, await ApplicantIdAsync(acting, cancellationToken), StringComparison.Ordinal) ? check : null;
    }

    /// <summary>The national ID (or iqama) of the individual filling in the request.</summary>
    private async Task<string> ApplicantIdAsync(ActingParty acting, CancellationToken cancellationToken)
    {
        var individual = await partyFacts.GetAsync(acting.IndividualId, false, cancellationToken);
        return individual?.Identifiers.GetValueOrDefault("NATIONAL_ID") ?? individual?.Identifiers.GetValueOrDefault("IQAMA")
            ?? throw new RequestConflictException("A Nafath confirmation needs the applicant's national ID or iqama.");
    }

    private Platform.Nafath.INafathVerifications Nafath() =>
        services.GetService<Platform.Nafath.INafathVerifications>() is { IsConfigured: true } nafath
            ? nafath
            : throw new RequestConflictException("Nafath is not set up on this installation.");

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

    /// <summary>
    /// Uploads a file for a file question of an editable request and adds it to the answer (replacing it when the question
    /// takes one file). The question's extensions, size and number of files are checked.
    /// </summary>
    public async Task<RequestFileInfo> UploadFileAsync(Guid id, string questionCode, string fileName, long length, Stream content, CancellationToken cancellationToken)
    {
        var (entity, _) = await LoadMineAsync(id, cancellationToken);
        RequireEditable(entity);
        var version = await cache.GetAsync(context, entity.ServiceVersionId, cancellationToken);
        // A file question, or a file column of a rows question (QUESTION/COLUMN).
        var question = QuestionSnapshot.FileTarget(version, questionCode);
        var inRow = questionCode.Contains('/', StringComparison.Ordinal);
        if (question is null)
        {
            throw new RequestValidationException([new FieldError(questionCode, "INVALID", "هذا السؤال لا يقبل ملفات.", "This question does not take files.", null)]);
        }

        var name = Path.GetFileName(fileName ?? string.Empty).Trim();
        var extension = RequestFiles.ExtensionOf(name);
        var allowed = RequestFiles.Extensions(question);
        if (name.Length is 0 or > 255 || !allowed.Contains(extension))
        {
            throw new RequestValidationException([new FieldError(questionCode, "FILE_TYPE", $"الأنواع المقبولة: {string.Join("، ", allowed)}.", $"Accepted types: {string.Join(", ", allowed)}.", null)]);
        }

        var max = RequestFiles.MaxBytes(question);
        if (length <= 0 || length > max)
        {
            var mb = max / 1024 / 1024;
            throw new RequestValidationException([new FieldError(questionCode, "FILE_SIZE", $"الحد الأقصى لحجم الملف {mb} ميجابايت.", $"Files may be at most {mb} MB.", null)]);
        }

        var data = JsonNode.Parse(entity.Data)!.AsObject();
        var current = inRow ? [] : RequestFiles.Ids(data[questionCode]);
        var limit = RequestFiles.MaxFiles(question);
        if (!inRow && limit > 1 && current.Count >= limit)
        {
            throw new RequestValidationException([new FieldError(questionCode, "FILE_COUNT", $"الحد الأقصى {limit} ملفات.", $"At most {limit} files.", null)]);
        }

        var stored = await storage.SaveAsync(content, name, RequestFiles.ContentTypeOf(extension), cancellationToken);
        var file = new RequestFile
        {
            RequestId = entity.Id,
            QuestionCode = questionCode,
            FileName = name,
            ContentType = stored.ContentType,
            Length = stored.Length,
            Sha256 = stored.Sha256,
            StorageKey = stored.Key,
            UploadedAt = clock.GetUtcNow(),
            UploadedBy = user.UserId ?? string.Empty,
        };
        context.RequestFiles.Add(file);

        // A row's file: the row (kept by the browser until the row is saved) takes it; the answer is not changed here.
        if (inRow)
        {
            await context.SaveChangesAsync(cancellationToken);
            return new RequestFileInfo(file.Id, file.QuestionCode, file.FileName, file.ContentType, file.Length, file.UploadedAt);
        }

        // One file: the new one replaces the answer (the old file goes); several: it is added.
        if (limit == 1)
        {
            foreach (var old in await context.RequestFiles.Where(f => f.RequestId == entity.Id && f.QuestionCode == questionCode).ToListAsync(cancellationToken))
            {
                await RemoveAsync(old, cancellationToken);
            }

            current = [];
        }

        data[questionCode] = new JsonArray([.. current.Append(file.Id.ToString()).Select(v => (JsonNode)JsonValue.Create(v)!)]);
        entity.Data = data.ToJsonString();
        await context.SaveChangesAsync(cancellationToken);
        return new RequestFileInfo(file.Id, file.QuestionCode, file.FileName, file.ContentType, file.Length, file.UploadedAt);
    }

    /// <summary>Removes a file of an editable request and takes it out of the answer.</summary>
    public async Task DeleteFileAsync(Guid id, Guid fileId, CancellationToken cancellationToken)
    {
        var (entity, _) = await LoadMineAsync(id, cancellationToken);
        RequireEditable(entity);
        var file = await context.RequestFiles.SingleOrDefaultAsync(f => f.Id == fileId && f.RequestId == entity.Id, cancellationToken)
            ?? throw new RequestNotFoundException($"File {fileId} does not exist.");
        var data = JsonNode.Parse(entity.Data)!.AsObject();
        RequestFiles.Forget(data, file.QuestionCode, file.Id.ToString());
        entity.Data = data.ToJsonString();
        await RemoveAsync(file, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The content of a file of one of the applicant's requests.</summary>
    public async Task<(Stream Content, string ContentType, string FileName)> OpenFileAsync(Guid id, Guid fileId, CancellationToken cancellationToken)
    {
        var (entity, _) = await LoadMineAsync(id, cancellationToken);
        var file = await context.RequestFiles.AsNoTracking().SingleOrDefaultAsync(f => f.Id == fileId && f.RequestId == entity.Id, cancellationToken)
            ?? throw new RequestNotFoundException($"File {fileId} does not exist.");
        return (await storage.OpenReadAsync(file.StorageKey, cancellationToken), file.ContentType, file.FileName);
    }

    private async Task RemoveAsync(RequestFile file, CancellationToken cancellationToken)
    {
        context.RequestFiles.Remove(file);
        await storage.DeleteAsync(file.StorageKey, cancellationToken);
    }

    public async Task CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var (entity, _) = await LoadMineAsync(id, cancellationToken);
        RequireDraft(entity);
        entity.StatusCategory = StatusCategory.Cancelled;
        entity.DisplayStatus = "CANCELLED";
        entity.ClosedAt = clock.GetUtcNow();
        AddEvent(entity, RequestEventTypes.Cancelled, StatusCategory.Draft, StatusCategory.Cancelled, null);
        await using var work = await unitOfWork.BeginAsync(cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await RequestLinks.TellParentAsync(services, entity, clock, cancellationToken);
        await work.CommitAsync(cancellationToken);
    }

    private async Task<RequestView> ViewAsync(
        ServiceRequest entity, Service service, ResolvedVersion version, PartyFacts facts, ActingParty acting, string? step, CancellationToken cancellationToken,
        DocumentFacts? document = null, IReadOnlyList<AnswerChange>? changes = null)
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
        var timeline = await context.RequestEvents.AsNoTracking().Where(e => e.RequestId == entity.Id).OrderBy(e => e.At).ThenBy(e => e.Id)
            .Select(e => new TimelineEntry(e.At, e.UserId == me ? e.UserId : string.Empty, e.Type, e.FromStatus, e.ToStatus, e.Comment))
            .ToListAsync(cancellationToken);
        var bills = services.GetService<IPayments>() is { } payments && entity.StatusCategory != StatusCategory.Draft
            ? await payments.OfSubjectAsync(RequestSubject.Type, entity.Id, cancellationToken)
            : [];
        var visits = services.GetService<IVisits>() is { } registry && entity.StatusCategory != StatusCategory.Draft
            ? await registry.OfSubjectAsync(RequestSubject.Type, entity.Id, cancellationToken)
            : [];
        var files = await RequestFiles.ListAsync(context, entity.Id, cancellationToken);
        var runs = (JsonNode.Parse(entity.Integrations) as JsonObject ?? [])
            .Select(p => (p.Key, Run: p.Value?.Deserialize<IntegrationStepRun>(ChangeJson.Options)))
            .Where(p => p.Run is not null)
            .ToDictionary(p => p.Key, p => p.Run!, StringComparer.Ordinal);
        return new RequestView(Summary(entity, (service.NameAr, service.NameEn)), entity.VersionNumber, entity.LevelCode, entity.CurrentStep, data, lines, byLevel, version, stepErrors, timeline, bills, visits, files)
        {
            Integrations = runs,
            Changes = changes ?? [],
            Links = await LinksAsync(context, entity, cancellationToken),
            Bundle = entity.StatusCategory == StatusCategory.Draft ? await BundleOptionsAsync(version, facts, acting, data, cancellationToken) : [],
            ItemResults = ItemResults(entity),
        };
    }

    /// <summary>The items decided one by one at workflow tasks.</summary>
    internal static IReadOnlyList<SubjectItem> ItemResults(ServiceRequest entity) =>
        JsonSerializer.Deserialize<List<SubjectItem>>(entity.ItemResults, JsonSerializerOptions.Web) ?? [];

    /// <summary>The request it came from and the requests that came from it (3f), newest last.</summary>
    internal static async Task<IReadOnlyList<LinkedRequest>> LinksAsync(ServicesDbContext context, ServiceRequest entity, CancellationToken cancellationToken)
    {
        var rows = await context.Requests.AsNoTracking()
            .Where(r => r.Id == entity.ParentRequestId || r.ParentRequestId == entity.Id)
            .OrderBy(r => r.CreatedAt)
            .Join(context.Services, r => r.ServiceId, s => s.Id, (r, s) => new { Request = r, s.NameAr, s.NameEn })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(row => new LinkedRequest(
            row.Request.Id,
            row.Request.Number,
            row.Request.ServiceCode,
            row.NameAr,
            row.NameEn,
            row.Request.StatusCategory,
            row.Request.DisplayStatus,
            row.Request.Id == entity.ParentRequestId ? "PARENT" : row.Request.LinkKind == RequestLinkKind.Spawned ? "SPAWNED" : "BUNDLED",
            row.Request.Id == entity.ParentRequestId ? entity.ParentStep : row.Request.ParentStep))];
    }

    /// <summary>A draft's bundled services, each checked for the applicant as it will be on submitting.</summary>
    private async Task<IReadOnlyList<BundleOption>> BundleOptionsAsync(ResolvedVersion version, PartyFacts facts, ActingParty acting, JsonObject data, CancellationToken cancellationToken)
    {
        var options = new List<BundleOption>();
        foreach (var item in version.Definition.Bundle)
        {
            Service other;
            ResolvedVersion otherVersion;
            try
            {
                var (found, otherVersionId) = await CurrentAsync(item.Service, cancellationToken);
                (other, otherVersion) = (found, await cache.GetAsync(context, otherVersionId, cancellationToken));
            }
            catch (RequestNotFoundException)
            {
                continue;
            }

            var reasons = eligibility.Check(other, otherVersion, facts, acting, Context(facts, acting, data, null)).ToList();
            reasons.AddRange(await prerequisites.CheckAsync(otherVersion.Definition, acting.PartyId, cancellationToken));
            options.Add(new BundleOption(other.Code, other.NameAr, other.NameEn, item.Required, item.NoteAr, item.NoteEn, reasons.Count == 0, reasons));
        }

        return options;
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
            await using var work = await unitOfWork.BeginAsync(cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            await RequestLinks.TellParentAsync(services, entity, clock, cancellationToken);
            await work.CommitAsync(cancellationToken);
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
    /// <summary>The company and branch the request belongs to (ADR-0036 step 6).</summary>
    private Task<(string? Company, string? Branch)> PlaceAsync(Service service, JsonObject data, CancellationToken cancellationToken) =>
        RequestPlacement.PlaceAsync(services.GetService<IOrganizationDirectory>(), service, data, cancellationToken);

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
        var offered = await portal.OfferedAsync(rows.Where(r => r.VersionId is not null), r => r.Service, cancellationToken);
        return [.. offered.OrderBy(r => r.Service.DisplayOrder).ThenBy(r => r.Service.Code).Select(r => (r.Service, r.VersionId!.Value))];
    }

    private async Task<(Service Service, Guid VersionId)> CurrentAsync(string code, CancellationToken cancellationToken)
    {
        var service = await context.Services.AsNoTracking().SingleOrDefaultAsync(s => s.Code == code && s.IsActive, cancellationToken)
            ?? throw new RequestNotFoundException($"Service '{code}' does not exist.");
        if (!await portal.OffersAsync(service, cancellationToken))
        {
            // The service's apps are not on the portal (ADR-0036): to applicants it does not exist.
            throw new RequestNotFoundException($"Service '{code}' does not exist.");
        }

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
        failures.AddRange(await prerequisites.CheckAsync(version.Definition, acting.PartyId, cancellationToken));
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

    private static FieldError IntegrationFailed(StepDefinition step, IntegrationStepRun? run) =>
        new(
            null,
            "INTEGRATION",
            run?.MessageAr is { Length: > 0 } ar ? ar : $"لم تكتمل خطوة «{step.TitleAr}»؛ حاول مرة أخرى.",
            run?.MessageEn is { Length: > 0 } en ? en : $"The step \"{step.TitleEn}\" did not complete; try again.",
            step.Code);

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
        new(r.Id, r.Number, r.ServiceCode, service.NameAr, service.NameEn, r.StatusCategory, r.DisplayStatus, r.BeneficiaryNameAr, r.BeneficiaryNameEn, r.FeeTotal, r.CreatedAt, r.SubmittedAt, r.DraftExpiresAt, r.SourceDocumentNumber, r.CompanyCode, r.BranchCode);

    private static CatalogEntry Entry(Service service, ResolvedVersion version, List<EligibilityFailure> failures) =>
        new(service.Code, service.NameAr, service.NameEn, service.DescriptionAr, service.DescriptionEn, service.Archetype, service.CategoryCode, service.Icon, service.PartyKinds, version.Number, failures.Count == 0, failures, service.DocumentType);
}
