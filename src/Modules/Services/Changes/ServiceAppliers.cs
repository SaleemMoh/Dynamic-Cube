using System.Text.Json;
using System.Text.RegularExpressions;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Expressions;
using DynamicCube.Platform.Integration;
using DynamicCube.Platform.Parties;
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

    public ServiceArchetype Archetype { get; set; }

    public string? CategoryCode { get; set; }

    public string? Icon { get; set; }

    public int DisplayOrder { get; set; }

    public List<PartyKind> PartyKinds { get; set; } = [];

    public bool IsListed { get; set; } = true;

    public bool DraftsAllowed { get; set; } = true;

    public int DraftExpiryDays { get; set; } = 30;

    public NewVersionPolicy OnNewVersion { get; set; }

    public string? NumberPrefix { get; set; }
}

internal sealed partial class ServiceDefinitionApplier(ServicesDbContext db) : ServiceApplier<Service, ServicePayload>(db)
{
    public override string EntityType => ServiceChangeTypes.Service;

    protected override IQueryable<Service> Query(ServicesDbContext context) => context.Services;

    protected override IQueryable<Service> ListQuery(ServicesDbContext context, string? parent) =>
        Query(context).OrderBy(s => s.DisplayOrder).ThenBy(s => s.Code);

    protected override Task ApplyAsync(Service entity, ServicePayload payload, ConfigRefs<ServicesDbContext> refs, ValidationErrors errors, Service? existing, CancellationToken cancellationToken)
    {
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

        var prefix = Blank(payload.NumberPrefix) ?? entity.Code;
        if (!Prefix().IsMatch(prefix))
        {
            errors.Add("numberPrefix", "Up to 20 upper-case letters, digits, _ or -.");
        }

        entity.DescriptionAr = Blank(payload.DescriptionAr);
        entity.DescriptionEn = Blank(payload.DescriptionEn);
        entity.Archetype = payload.Archetype;
        entity.CategoryCode = Blank(payload.CategoryCode);
        entity.Icon = Blank(payload.Icon);
        entity.DisplayOrder = payload.DisplayOrder;
        entity.PartyKinds = [.. payload.PartyKinds.Distinct()];
        entity.IsListed = payload.IsListed;
        entity.DraftsAllowed = payload.DraftsAllowed;
        entity.DraftExpiryDays = payload.DraftExpiryDays;
        entity.OnNewVersion = payload.OnNewVersion;
        entity.NumberPrefix = prefix;
        return Task.CompletedTask;
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

    public string? Prefill { get; set; }
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

        if (payload.Type != QuestionType.Select && (lov is not null || payload.Options.Count > 0 || payload.Multiple))
        {
            errors.Add("type", "Options, LOVs and multiple answers are for select questions.");
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

        var prefill = Blank(payload.Prefill);
        if (prefill is not null && !JsonPaths.IsValid(prefill))
        {
            errors.Add("prefill", "Use a path such as party.profile.CR_CAPITAL or party.identifiers.CR_NUMBER.");
        }

        entity.Type = payload.Type;
        entity.HintAr = Blank(payload.HintAr);
        entity.HintEn = Blank(payload.HintEn);
        entity.Lov = lov;
        entity.LovParentQuestion = parent;
        entity.Options = payload.Options;
        entity.Multiple = payload.Multiple;
        entity.Validation = validation;
        entity.Prefill = prefill;
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
            Prefill = entity.Prefill,
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

    protected override async Task ApplyAsync(ServiceVersion entity, ServiceVersionPayload payload, ConfigRefs<ServicesDbContext> refs, ValidationErrors errors, ServiceVersion? existing, CancellationToken cancellationToken)
    {
        var (serviceCode, number) = Parse(entity.Code);
        var definition = payload.Definition ?? new VersionDefinition();
        var serialized = ChangeJson.Serialize(definition);
        entity.IsRetired = payload.IsRetired;
        if (existing is not null)
        {
            if (!JsonElement.DeepEquals(JsonDocument.Parse(serialized).RootElement, JsonDocument.Parse(existing.Definition).RootElement))
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

        var known = await refs.Context.Questions.Select(q => q.Code).ToListAsync(cancellationToken);
        var available = new HashSet<string>(known, StringComparer.Ordinal);
        DefinitionValidator.Validate(definition, code => available.Contains(code) || refs.IsPendingCreate(ServiceChangeTypes.Question, code), evaluator, errors);

        // No version without a workflow to process its requests. A service created in the same change set gets its default
        // workflow when the change set is published.
        if (refs.IsValidating && !refs.IsPendingCreate(ServiceChangeTypes.Service, serviceCode) && services.GetService<IServiceWorkflows>() is { } serviceWorkflows)
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
