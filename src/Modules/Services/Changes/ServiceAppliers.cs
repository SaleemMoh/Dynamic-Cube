using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Persistence;
using DynamicCube.Modules.Services.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Documents;
using DynamicCube.Platform.Expressions;
using DynamicCube.Platform.Integration;
using DynamicCube.Platform.Parties;
using DynamicCube.Platform.Security;
using DynamicCube.Platform.Workflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Services.Changes;

internal abstract class ServiceApplier<TEntity, TPayload>(ServicesDbContext context)
    : ConfigApplier<ServicesDbContext, ConfigRefs<ServicesDbContext>, TEntity, TPayload>(context)
    where TEntity : ConfigEntity, new()
    where TPayload : ConfigPayload, new()
{
    public override string Area => ServiceChangeTypes.Area;

    public override string ResourceCode => ServiceResources.Config;

    protected override Regex CodePattern => ValidationErrors.IdentifierCode();

    protected override ConfigRefs<ServicesDbContext> CreateRefs(ServicesDbContext context, ChangeSet? draft) => new(context, draft);

    protected static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class ServicePayload : ConfigPayload
{
    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    /// <summary>The archetype's code (a declared <c>svc.archetype</c>).</summary>
    public string? Archetype { get; set; }

    public string? CategoryCode { get; set; }

    public string? Icon { get; set; }

    public int DisplayOrder { get; set; }

    public List<PartyKind> PartyKinds { get; set; } = [];

    public bool IsListed { get; set; } = true;

    public bool DraftsAllowed { get; set; } = true;

    public int DraftExpiryDays { get; set; } = 30;

    public NewVersionPolicy OnNewVersion { get; set; }

    public string? NumberPrefix { get; set; }

    /// <summary>The document type a follow-up service acts on.</summary>
    public string? DocumentType { get; set; }

    /// <summary>The company that handles the requests; empty for the default company.</summary>
    public string? CompanyCode { get; set; }

    /// <summary>The question whose answer picks the branch (branch code, region or city); empty for the default branch.</summary>
    public string? BranchQuestion { get; set; }
}

internal sealed partial class ServiceDefinitionApplier(ServicesDbContext db, IServiceProvider services) : ServiceApplier<Service, ServicePayload>(db)
{
    public override string EntityType => ServiceChangeTypes.Service;

    protected override IQueryable<Service> Query(ServicesDbContext context) => context.Services;

    protected override IQueryable<Service> ListQuery(ServicesDbContext context, string? parent) =>
        Query(context).OrderBy(s => s.DisplayOrder).ThenBy(s => s.Code);

    protected override async Task ApplyAsync(Service entity, ServicePayload payload, ConfigRefs<ServicesDbContext> refs, ValidationErrors errors, Service? existing, CancellationToken cancellationToken)
    {
        // The category is a value of class SVC_CATEGORY (Class/Domain); it may be published in another change set.
        errors.MaxLength("categoryCode", payload.CategoryCode, 100);
        errors.MaxLength("descriptionAr", payload.DescriptionAr, 4000);
        errors.MaxLength("descriptionEn", payload.DescriptionEn, 4000);
        if (payload.PartyKinds.Count == 0)
        {
            errors.Add("partyKinds", "Choose who may apply.");
        }

        if (payload.DraftExpiryDays is < 1 or > 365)
        {
            errors.Add("draftExpiryDays", "Must be between 1 and 365.");
        }

        var documentType = Blank(payload.DocumentType);
        if (documentType is not null && services.GetService<IDocumentDirectory>() is { } documents && await documents.TypeAsync(documentType, cancellationToken) is null)
        {
            errors.Add("documentType", $"Document type '{documentType}' does not exist.");
        }

        var prefix = Blank(payload.NumberPrefix) ?? entity.Code;
        if (!Prefix().IsMatch(prefix))
        {
            errors.Add("numberPrefix", "Up to 20 upper-case letters, digits, _ or -.");
        }

        // Where the requests go (ADR-0036 step 6): a company of the installation, and a question the service may ask.
        var company = Blank(payload.CompanyCode);
        if (company is not null && services.GetService<IOrganizationDirectory>() is { } organization
            && !(await organization.CompaniesAsync(cancellationToken)).Any(c => c.Code == company))
        {
            errors.Add("companyCode", $"Company '{company}' does not exist or is not active.");
        }

        var branchQuestion = Blank(payload.BranchQuestion);
        if (branchQuestion is not null && !refs.IsPendingCreate(ServiceChangeTypes.Question, branchQuestion)
            && !await refs.Context.Questions.AnyAsync(q => q.Code == branchQuestion && (q.ServiceCode == null || q.ServiceCode == entity.Code), cancellationToken))
        {
            errors.Add("branchQuestion", $"Question '{branchQuestion}' does not exist in the bank or for this service.");
        }

        // Packages from before archetypes were declared may leave it out: they were issuance services.
        var archetype = Blank(payload.Archetype)?.ToUpperInvariant() ?? "ISSUANCE";
        await refs.ResolveAsync(refs.Context.Archetypes, ServiceChangeTypes.Archetype, archetype, "archetype", errors, cancellationToken);

        entity.DescriptionAr = Blank(payload.DescriptionAr);
        entity.DescriptionEn = Blank(payload.DescriptionEn);
        entity.Archetype = archetype;
        entity.CategoryCode = Blank(payload.CategoryCode);
        entity.Icon = Blank(payload.Icon);
        entity.DisplayOrder = payload.DisplayOrder;
        entity.PartyKinds = [.. payload.PartyKinds.Distinct()];
        entity.IsListed = payload.IsListed;
        entity.DraftsAllowed = payload.DraftsAllowed;
        entity.DraftExpiryDays = payload.DraftExpiryDays;
        entity.OnNewVersion = payload.OnNewVersion;
        entity.NumberPrefix = prefix;
        entity.DocumentType = documentType;
        entity.CompanyCode = company;
        entity.BranchQuestion = branchQuestion;
    }

    protected override Task<ServicePayload> ToPayloadAsync(ServicesDbContext context, Service entity, CancellationToken cancellationToken) =>
        Task.FromResult(new ServicePayload
        {
            DescriptionAr = entity.DescriptionAr,
            DescriptionEn = entity.DescriptionEn,
            Archetype = entity.Archetype,
            CategoryCode = entity.CategoryCode,
            Icon = entity.Icon,
            DisplayOrder = entity.DisplayOrder,
            PartyKinds = entity.PartyKinds,
            IsListed = entity.IsListed,
            DraftsAllowed = entity.DraftsAllowed,
            DraftExpiryDays = entity.DraftExpiryDays,
            OnNewVersion = entity.OnNewVersion,
            NumberPrefix = entity.NumberPrefix,
            DocumentType = entity.DocumentType,
            CompanyCode = entity.CompanyCode,
            BranchQuestion = entity.BranchQuestion,
        });

    protected override async Task BeforeDeleteAsync(ServicesDbContext context, Service entity, CancellationToken cancellationToken)
    {
        if (await context.Versions.AnyAsync(v => v.ServiceId == entity.Id, cancellationToken))
        {
            throw new ChangeSetException($"Service '{entity.Code}' has versions; deactivate it instead.");
        }
    }

    [GeneratedRegex("^[A-Z0-9_\\-]{1,20}$")]
    private static partial Regex Prefix();
}

public sealed class ArchetypePayload : ConfigPayload
{
    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    public string? Icon { get; set; }

    public int DisplayOrder { get; set; }

    /// <summary>The service template: the version a new service of this archetype starts from.</summary>
    public VersionDefinition? Template { get; set; }

    /// <summary>On save, the template becomes a copy of this service's latest version.</summary>
    public string? TemplateFrom { get; set; }

    /// <summary>The default workflow: the definition a new service's workflow starts from.</summary>
    public JsonNode? Workflow { get; set; }

    /// <summary>On save, the default workflow becomes a copy of this workflow's latest published version.</summary>
    public string? WorkflowFrom { get; set; }
}

/// <summary>
/// Service archetypes (declarations): a kind of service with its service template and default workflow, designed in the
/// Service Builder and the Workflow Designer or copied from an existing service or workflow. A new service takes its
/// archetype's workflow when it is first published and the builder opens its template.
/// </summary>
internal sealed class ArchetypeApplier(ServicesDbContext db, IServiceProvider services) : ServiceApplier<Archetype, ArchetypePayload>(db)
{
    public override string EntityType => ServiceChangeTypes.Archetype;

    protected override IQueryable<Archetype> Query(ServicesDbContext context) => context.Archetypes;

    protected override IQueryable<Archetype> ListQuery(ServicesDbContext context, string? parent) =>
        Query(context).OrderBy(a => a.DisplayOrder).ThenBy(a => a.Code);

    protected override async Task ApplyAsync(Archetype entity, ArchetypePayload payload, ConfigRefs<ServicesDbContext> refs, ValidationErrors errors, Archetype? existing, CancellationToken cancellationToken)
    {
        errors.MaxLength("descriptionAr", payload.DescriptionAr, 2000);
        errors.MaxLength("descriptionEn", payload.DescriptionEn, 2000);
        entity.DescriptionAr = Blank(payload.DescriptionAr);
        entity.DescriptionEn = Blank(payload.DescriptionEn);
        entity.Icon = Blank(payload.Icon);
        entity.DisplayOrder = payload.DisplayOrder;

        // The service template: copied from a service's latest version, or as given.
        var template = payload.Template;
        if (Blank(payload.TemplateFrom) is { } from)
        {
            var latest = await refs.Context.Versions.Where(v => v.Service!.Code == from).OrderByDescending(v => v.Number).Select(v => v.Definition).FirstOrDefaultAsync(cancellationToken);
            if (latest is null)
            {
                errors.Add("templateFrom", $"Service '{from}' has no version to copy.");
            }
            else
            {
                template = JsonSerializer.Deserialize<VersionDefinition>(latest, ChangeJson.Options);
            }
        }

        if (template is not null)
        {
            template.Workflow = null;
            // A template serves any service: it may use shared questions only.
            var questions = template.Steps.SelectMany(s => s.Sections).SelectMany(s => s.Questions).Select(q => q.Code).Distinct().ToList();
            var shared = await refs.Context.Questions.Where(q => questions.Contains(q.Code) && q.ServiceCode == null).Select(q => q.Code).ToListAsync(cancellationToken);
            foreach (var code in questions.Except(shared).Where(c => !refs.IsPendingCreate(ServiceChangeTypes.Question, c)))
            {
                errors.Add("template", $"Question '{code}' is not a shared bank question; a template uses shared questions only.");
            }
        }

        entity.Template = template is null ? null : ChangeJson.Serialize(template);

        // The default workflow: copied from a workflow's latest published version, or as given; it must be able to run.
        var workflow = payload.Workflow?.ToJsonString();
        var workflows = services.GetService<IServiceWorkflows>();
        if (Blank(payload.WorkflowFrom) is { } source && workflows is not null)
        {
            workflow = await workflows.LatestDefinitionAsync(source, cancellationToken);
            if (workflow is null)
            {
                errors.Add("workflowFrom", $"Workflow '{source}' has no published version to copy.");
            }
        }

        if (workflow is not null && workflows is not null)
        {
            foreach (var (field, messages) in workflows.Check(workflow))
            {
                foreach (var message in messages)
                {
                    errors.Add("workflow." + field, message);
                }
            }
        }

        entity.Workflow = workflow;
    }

    protected override Task<ArchetypePayload> ToPayloadAsync(ServicesDbContext context, Archetype entity, CancellationToken cancellationToken) =>
        Task.FromResult(new ArchetypePayload
        {
            DescriptionAr = entity.DescriptionAr,
            DescriptionEn = entity.DescriptionEn,
            Icon = entity.Icon,
            DisplayOrder = entity.DisplayOrder,
            Template = entity.Template is null ? null : JsonSerializer.Deserialize<VersionDefinition>(entity.Template, ChangeJson.Options),
            Workflow = entity.Workflow is null ? null : JsonNode.Parse(entity.Workflow),
        });

    protected override async Task BeforeDeleteAsync(ServicesDbContext context, Archetype entity, CancellationToken cancellationToken)
    {
        if (await context.Services.AnyAsync(s => s.Archetype == entity.Code, cancellationToken))
        {
            throw new ChangeSetException($"Services of archetype '{entity.Code}' exist; deactivate it instead.");
        }
    }
}

public sealed class QuestionPayload : ConfigPayload
{
    public QuestionType Type { get; set; }

    public string? HintAr { get; set; }

    public string? HintEn { get; set; }

    public string? Lov { get; set; }

    public string? LovParentQuestion { get; set; }

    public List<QuestionOption> Options { get; set; } = [];

    public bool Multiple { get; set; }

    public QuestionValidation Validation { get; set; } = new();

    /// <summary>The service the question belongs to (a service-level question), or "*" for a shared bank question.</summary>
    public string? ServiceCode { get; set; }

    /// <summary>Where the first answer comes from (packages from before this field: PARTY when a prefill path is given).</summary>
    public QuestionFill? Fill { get; set; }

    public string? Prefill { get; set; }

    public IntegrationFill? Integration { get; set; }

    public bool AllowManualEntry { get; set; } = true;
}

internal sealed class QuestionApplier(ServicesDbContext db) : ServiceApplier<Question, QuestionPayload>(db)
{
    public override string EntityType => ServiceChangeTypes.Question;

    protected override int NameMaxLength => 1000;

    protected override IQueryable<Question> Query(ServicesDbContext context) => context.Questions;

    protected override async Task ApplyAsync(Question entity, QuestionPayload payload, ConfigRefs<ServicesDbContext> refs, ValidationErrors errors, Question? existing, CancellationToken cancellationToken)
    {
        errors.MaxLength("hintAr", payload.HintAr, 2000);
        errors.MaxLength("hintEn", payload.HintEn, 2000);
        var lov = Blank(payload.Lov);
        if (payload.Type == QuestionType.Select && lov is null && payload.Options.Count == 0)
        {
            errors.Add("options", "A select question needs options or an LOV.");
        }

        if (payload.Type != QuestionType.Select && (lov is not null || payload.Options.Count > 0 || (payload.Multiple && payload.Type != QuestionType.File)))
        {
            errors.Add("type", "Options and LOVs are for select questions; multiple answers for select and file questions.");
        }

        ChildSync.RequireUnique(payload.Options, o => o.Value, "options", errors);
        foreach (var (option, i) in payload.Options.Select((o, i) => (o, i)))
        {
            errors.Required($"options[{i}].value", option.Value);
            errors.Required($"options[{i}].labelAr", option.LabelAr);
            errors.Required($"options[{i}].labelEn", option.LabelEn);
        }

        var parent = Blank(payload.LovParentQuestion);
        if (parent is not null)
        {
            if (lov is null)
            {
                errors.Add("lovParentQuestion", "Only LOV questions have a parent question.");
            }

            await refs.ResolveAsync(refs.Context.Questions, ServiceChangeTypes.Question, parent, "lovParentQuestion", errors, cancellationToken);
        }

        var validation = payload.Validation ?? new QuestionValidation();
        if (payload.Type == QuestionType.File)
        {
            validation.Extensions = [.. validation.Extensions.Select(e => e.Trim().TrimStart('.').ToLowerInvariant()).Where(e => e.Length > 0).Distinct()];
            if (validation.Extensions.Any(e => !RequestFiles.ExtensionPattern().IsMatch(e)))
            {
                errors.Add("validation.extensions", "Use extensions such as pdf, jpg or docx.");
            }

            if (validation.MaxSizeMb is < 1 or > RequestFiles.LargestMb)
            {
                errors.Add("validation.maxSizeMb", $"Must be between 1 and {RequestFiles.LargestMb}.");
            }

            if (validation.MaxFiles is < 1 or > 20)
            {
                errors.Add("validation.maxFiles", "Must be between 1 and 20.");
            }
        }
        else
        {
            validation.Extensions = [];
            validation.MaxSizeMb = null;
            validation.MaxFiles = null;
        }

        if (validation.Min > validation.Max)
        {
            errors.Add("validation.max", "Must not be less than min.");
        }

        if (validation.MaxLength is < 1 or > 100_000)
        {
            errors.Add("validation.maxLength", "Must be between 1 and 100000.");
        }

        if (validation.Pattern is not null)
        {
            try
            {
                _ = new Regex(validation.Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
            }
            catch (ArgumentException)
            {
                errors.Add("validation.pattern", "Not a valid regular expression.");
            }
        }

        // The first answer: typed by the applicant, from the party's data, or from an integration call.
        var fill = payload.Fill ?? (Blank(payload.Prefill) is null ? QuestionFill.None : QuestionFill.Party);
        var prefill = fill == QuestionFill.Party ? Blank(payload.Prefill) : null;
        var integration = fill == QuestionFill.Integration ? payload.Integration ?? new IntegrationFill() : null;
        if (fill != QuestionFill.None && payload.Type == QuestionType.File)
        {
            errors.Add("fill", "File questions are answered by the applicant.");
        }

        if (fill == QuestionFill.Party && (prefill is null || !JsonPaths.IsValid(prefill)))
        {
            errors.Add("prefill", "Choose a field of the party's data, e.g. party.profile.CR_CAPITAL or party.identifiers.CR_NUMBER.");
        }

        if (integration is not null)
        {
            integration.Operation = integration.Operation?.Trim() ?? string.Empty;
            integration.Output = integration.Output?.Trim() ?? string.Empty;
            errors.Required("integration.operation", integration.Operation);
            if (!JsonPaths.IsValid(integration.Output))
            {
                errors.Add("integration.output", "Give an output of the operation, or a path in its result such as data.capital.");
            }

            IntegrationInputs.Check(integration.Inputs, "integration.inputs", errors);
        }

        // "*" (or none): a shared bank question.
        var serviceCode = Blank(payload.ServiceCode) is { } owner && owner != "*" ? owner : null;
        if (serviceCode is not null)
        {
            await refs.ResolveAsync(refs.Context.Services, ServiceChangeTypes.Service, serviceCode, "serviceCode", errors, cancellationToken);
        }

        if (existing is not null && existing.ServiceCode != serviceCode && await UsedByOtherServicesAsync(refs.Context, entity.Code, serviceCode, cancellationToken))
        {
            errors.Add("serviceCode", "Versions of other services use this question; it can only stay shared.");
        }

        entity.Type = payload.Type;
        entity.HintAr = Blank(payload.HintAr);
        entity.HintEn = Blank(payload.HintEn);
        entity.Lov = lov;
        entity.LovParentQuestion = parent;
        entity.Options = payload.Options;
        entity.Multiple = payload.Multiple;
        entity.Validation = validation;
        entity.ServiceCode = serviceCode;
        entity.Fill = fill;
        entity.Prefill = prefill;
        entity.Integration = integration;
        entity.AllowManualEntry = fill == QuestionFill.None || payload.AllowManualEntry;
    }

    /// <summary>Whether versions of a service other than <paramref name="owner"/> use the question (any when it would be shared).</summary>
    private static async Task<bool> UsedByOtherServicesAsync(ServicesDbContext context, string code, string? owner, CancellationToken cancellationToken)
    {
        if (owner is null)
        {
            return false;
        }

        var usage = $$"""{"steps":[{"sections":[{"questions":[{"code":"{{code}}"}]}]}]}""";
        return await context.Versions.AnyAsync(v => v.Service!.Code != owner && EF.Functions.JsonContains(v.Definition, usage), cancellationToken);
    }

    protected override Task<QuestionPayload> ToPayloadAsync(ServicesDbContext context, Question entity, CancellationToken cancellationToken) =>
        Task.FromResult(new QuestionPayload
        {
            Type = entity.Type,
            HintAr = entity.HintAr,
            HintEn = entity.HintEn,
            Lov = entity.Lov,
            LovParentQuestion = entity.LovParentQuestion,
            Options = entity.Options,
            Multiple = entity.Multiple,
            Validation = entity.Validation,
            ServiceCode = entity.ServiceCode ?? "*",
            Fill = entity.Fill,
            Prefill = entity.Prefill,
            Integration = entity.Integration,
            AllowManualEntry = entity.AllowManualEntry,
        });

    protected override async Task BeforeDeleteAsync(ServicesDbContext context, Question entity, CancellationToken cancellationToken)
    {
        // Published versions keep snapshots, but authors reading a version should still find its questions in the bank.
        var usage = $$"""{"steps":[{"sections":[{"questions":[{"code":"{{entity.Code}}"}]}]}]}""";
        if (await context.Versions.AnyAsync(v => EF.Functions.JsonContains(v.Definition, usage), cancellationToken))
        {
            throw new ChangeSetException($"Question '{entity.Code}' is used by service versions; deactivate it instead.");
        }
    }
}

public sealed class ServiceVersionPayload : ConfigPayload
{
    public bool IsRetired { get; set; }

    public VersionDefinition? Definition { get; set; }

    /// <summary>Read-only: when the version was published.</summary>
    public DateTimeOffset? PublishedAt { get; set; }
}

/// <summary>
/// Service versions, keyed <c>SERVICE/N</c>. A new version takes the next number; afterwards only its names and the
/// retired flag may change, so requests always run on the definition they started with.
/// </summary>
internal sealed partial class ServiceVersionApplier(ServicesDbContext db, JsonLogicEvaluator evaluator, IServiceProvider services) : ServiceApplier<ServiceVersion, ServiceVersionPayload>(db)
{
    public override string EntityType => ServiceChangeTypes.Version;

    protected override Regex CodePattern => Key();

    protected override IQueryable<ServiceVersion> Query(ServicesDbContext context) => context.Versions.Include(v => v.Service);

    protected override IQueryable<ServiceVersion> ListQuery(ServicesDbContext context, string? parent) =>
        (parent is null ? Query(context) : Query(context).Where(v => v.Service!.Code == parent)).OrderBy(v => v.Service!.Code).ThenByDescending(v => v.Number);

    // Oldest first: a package creates versions in number order.
    protected override IQueryable<ServiceVersion> ExportQuery(ServicesDbContext context) =>
        Query(context).OrderBy(v => v.Service!.Code).ThenBy(v => v.Number);

    protected override async Task ApplyAsync(ServiceVersion entity, ServiceVersionPayload payload, ConfigRefs<ServicesDbContext> refs, ValidationErrors errors, ServiceVersion? existing, CancellationToken cancellationToken)
    {
        var (serviceCode, number) = Parse(entity.Code);
        var definition = payload.Definition ?? new VersionDefinition();
        // A workflow named by the version (packages from before services owned their workflow) is copied into the service's
        // own workflow when the change set is published (see ServiceWorkflowProvisioner); the version keeps no name.
        var namedWorkflow = string.IsNullOrWhiteSpace(definition.Workflow) ? null : definition.Workflow.Trim();
        definition.Workflow = null;
        var serialized = ChangeJson.Serialize(definition);
        entity.IsRetired = payload.IsRetired;
        if (existing is not null)
        {
            // Compared as definitions: a version stored before a field existed has no such key, the payload has it empty.
            var before = JsonSerializer.Deserialize<VersionDefinition>(existing.Definition, ChangeJson.Options) ?? new VersionDefinition();
            before.Workflow = null;
            var stored = ChangeJson.Serialize(before);
            if (!JsonElement.DeepEquals(JsonDocument.Parse(serialized).RootElement, JsonDocument.Parse(stored).RootElement))
            {
                errors.Add("definition", $"Published versions cannot change; add version {serviceCode}/{number + 1} instead.");
            }

            return;
        }

        entity.ServiceId = await refs.ResolveAsync(refs.Context.Services, ServiceChangeTypes.Service, serviceCode, "code", errors, cancellationToken) ?? Guid.Empty;
        entity.Number = number;
        entity.Definition = serialized;
        entity.Resolved = null;

        var latest = await refs.Context.Versions.Where(v => v.Service!.Code == serviceCode).MaxAsync(v => (int?)v.Number, cancellationToken) ?? 0;
        // Version numbers created earlier in the same draft count as taken.
        var pendingBefore = refs.IsValidating ? Enumerable.Range(1, Math.Max(0, number - 1)).Count(n => refs.IsPendingCreate(ServiceChangeTypes.Version, $"{serviceCode}/{n}")) : 0;
        if (number != latest + 1 + pendingBefore)
        {
            errors.Add("code", $"The next version of {serviceCode} is {latest + 1 + pendingBefore}.");
        }

        // Shared bank questions and the service's own (service-level) questions; another service's questions are not usable.
        var known = await refs.Context.Questions.Select(q => new { q.Code, q.ServiceCode }).ToListAsync(cancellationToken);
        var available = known.Where(q => q.ServiceCode is null || q.ServiceCode == serviceCode).Select(q => q.Code).ToHashSet(StringComparer.Ordinal);
        DefinitionValidator.Validate(definition, code => available.Contains(code) || refs.IsPendingCreate(ServiceChangeTypes.Question, code), evaluator, errors);
        foreach (var other in known.Where(q => q.ServiceCode is not null && q.ServiceCode != serviceCode && definition.Steps.Any(s => s.Sections.Any(x => x.Questions.Any(u => u.Code == q.Code)))))
        {
            errors.Add("definition.steps", $"Question '{other.Code}' belongs to service {other.ServiceCode}.");
        }

        if (definition.Availability is not null && !await ActsOnDocumentAsync(refs, serviceCode, cancellationToken))
        {
            errors.Add("definition.availability", $"Service '{serviceCode}' does not act on a document; give it a document type first.");
        }

        // No version without a workflow to process its requests. A service created in the same change set gets its default
        // workflow when the change set is published; a named workflow becomes the service's when it is published. A named
        // workflow that does not exist yet may come in another change set of the same package: publishing checks it.
        if (refs.IsValidating && namedWorkflow is not null && services.GetService<IServiceWorkflows>() is { } adopter)
        {
            if (await adopter.ExistsAsync(namedWorkflow, cancellationToken) && !await services.GetRequiredService<IWorkflowRuntime>().CanStartAsync(namedWorkflow, cancellationToken))
            {
                errors.Add("definition.workflow", $"The workflow '{namedWorkflow}' has no published version; publish one first.");
            }
        }
        else if (refs.IsValidating && !refs.IsPendingCreate(ServiceChangeTypes.Service, serviceCode) && services.GetService<IServiceWorkflows>() is { } serviceWorkflows)
        {
            var workflow = await serviceWorkflows.WorkflowOfAsync(serviceCode, cancellationToken);
            if (workflow is null)
            {
                errors.Add("workflow", $"Service '{serviceCode}' has no workflow yet; it gets one when the service is published.");
            }
            else if (!await services.GetRequiredService<IWorkflowRuntime>().CanStartAsync(workflow, cancellationToken))
            {
                errors.Add("workflow", $"The workflow '{workflow}' of service '{serviceCode}' has no published version; publish one first.");
            }
        }
    }

    protected override Task<ServiceVersionPayload> ToPayloadAsync(ServicesDbContext context, ServiceVersion entity, CancellationToken cancellationToken) =>
        Task.FromResult(new ServiceVersionPayload
        {
            IsRetired = entity.IsRetired,
            Definition = JsonSerializer.Deserialize<VersionDefinition>(entity.Definition, ChangeJson.Options),
            PublishedAt = entity.PublishedAt,
        });

    /// <summary>Whether the service has a document type, as published or as created or changed in the same change set.</summary>
    private static async Task<bool> ActsOnDocumentAsync(ConfigRefs<ServicesDbContext> refs, string serviceCode, CancellationToken cancellationToken)
    {
        var pending = refs.Draft?.Items.LastOrDefault(i => i.EntityType == ServiceChangeTypes.Service && i.EntityCode == serviceCode && i.After is not null);
        if (pending is not null)
        {
            return !string.IsNullOrWhiteSpace(JsonSerializer.Deserialize<ServicePayload>(pending.After!, ChangeJson.Options)?.DocumentType);
        }

        return await refs.Context.Services.AnyAsync(s => s.Code == serviceCode && s.DocumentType != null, cancellationToken);
    }

    internal static (string Service, int Number) Parse(string key)
    {
        var slash = key.LastIndexOf('/');
        return slash > 0 && int.TryParse(key[(slash + 1)..], System.Globalization.CultureInfo.InvariantCulture, out var number)
            ? (key[..slash], number)
            : (key, 0);
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_\\-]{0,50}/[1-9][0-9]{0,3}$")]
    private static partial Regex Key();
}
