using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DynamicCube.Modules.Decisions.Domain;
using DynamicCube.Modules.Decisions.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Decisions;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Decisions.Changes;

internal abstract class DecisionApplierBase<TEntity, TPayload>(DecisionsDbContext context)
    : ConfigApplier<DecisionsDbContext, ConfigRefs<DecisionsDbContext>, TEntity, TPayload>(context)
    where TEntity : ConfigEntity, new()
    where TPayload : ConfigPayload, new()
{
    public override string Area => DecisionChangeTypes.Area;

    public override string ResourceCode => DecisionResources.Config;

    protected override Regex CodePattern => ValidationErrors.IdentifierCode();

    protected override ConfigRefs<DecisionsDbContext> CreateRefs(DecisionsDbContext context, ChangeSet? draft) => new(context, draft);
}

public sealed class RuleSetPayload : ConfigPayload
{
    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    /// <summary>Engine code, e.g. <c>RULES</c> (built in) or the code of an engine supplied separately.</summary>
    public string? Engine { get; set; }

    public bool IsPublic { get; set; }
}

internal sealed class RuleSetApplier(DecisionsDbContext db, IEnumerable<IDecisionEngine> engines) : DecisionApplierBase<RuleSet, RuleSetPayload>(db)
{
    public override string EntityType => DecisionChangeTypes.RuleSet;

    protected override IQueryable<RuleSet> Query(DecisionsDbContext context) => context.RuleSets;

    protected override async Task ApplyAsync(RuleSet entity, RuleSetPayload payload, ConfigRefs<DecisionsDbContext> refs, ValidationErrors errors, RuleSet? existing, CancellationToken cancellationToken)
    {
        errors.MaxLength("descriptionAr", payload.DescriptionAr, 4000);
        errors.MaxLength("descriptionEn", payload.DescriptionEn, 4000);
        errors.Required("engine", payload.Engine);
        var engine = payload.Engine?.Trim() ?? string.Empty;
        if (engine.Length > 0 && !engines.Any(e => e.Code == engine))
        {
            errors.Add("engine", $"No engine '{engine}' is installed (installed: {string.Join(", ", engines.Select(e => e.Code))}).");
        }

        if (existing is not null && existing.Engine != engine && await refs.Context.Versions.AnyAsync(v => v.RuleSetId == existing.Id, cancellationToken))
        {
            errors.Add("engine", "The engine of a rule set with versions cannot change; add a new rule set.");
        }

        entity.DescriptionAr = string.IsNullOrWhiteSpace(payload.DescriptionAr) ? null : payload.DescriptionAr.Trim();
        entity.DescriptionEn = string.IsNullOrWhiteSpace(payload.DescriptionEn) ? null : payload.DescriptionEn.Trim();
        entity.Engine = engine;
        entity.IsPublic = payload.IsPublic;
    }

    protected override Task<RuleSetPayload> ToPayloadAsync(DecisionsDbContext context, RuleSet entity, CancellationToken cancellationToken) =>
        Task.FromResult(new RuleSetPayload { DescriptionAr = entity.DescriptionAr, DescriptionEn = entity.DescriptionEn, Engine = entity.Engine, IsPublic = entity.IsPublic });

    protected override async Task BeforeDeleteAsync(DecisionsDbContext context, RuleSet entity, CancellationToken cancellationToken)
    {
        if (await context.Versions.AnyAsync(v => v.RuleSetId == entity.Id, cancellationToken))
        {
            throw new ChangeSetException($"Rule set '{entity.Code}' has versions; deactivate it instead.");
        }
    }
}

public sealed class RuleSetVersionPayload : ConfigPayload
{
    public bool IsRetired { get; set; }

    /// <summary>The engine's configuration.</summary>
    public JsonNode? Configuration { get; set; }

    /// <summary>Read-only: when the version was published.</summary>
    public DateTimeOffset? PublishedAt { get; set; }
}

/// <summary>
/// Rule-set versions, keyed <c>RULE_SET/N</c>, checked by their engine. A new version takes the next number; afterwards
/// only its names and the retired flag may change, so recorded evaluations always point at the configuration used.
/// </summary>
internal sealed partial class RuleSetVersionApplier(DecisionsDbContext db, IEnumerable<IDecisionEngine> engines, TimeProvider clock)
    : DecisionApplierBase<RuleSetVersion, RuleSetVersionPayload>(db)
{
    public override string EntityType => DecisionChangeTypes.Version;

    protected override Regex CodePattern => Key();

    protected override IQueryable<RuleSetVersion> Query(DecisionsDbContext context) => context.Versions.Include(v => v.RuleSet);

    protected override IQueryable<RuleSetVersion> ListQuery(DecisionsDbContext context, string? parent) =>
        (parent is null ? Query(context) : Query(context).Where(v => v.RuleSet!.Code == parent)).OrderBy(v => v.RuleSet!.Code).ThenByDescending(v => v.Number);

    protected override async Task ApplyAsync(RuleSetVersion entity, RuleSetVersionPayload payload, ConfigRefs<DecisionsDbContext> refs, ValidationErrors errors, RuleSetVersion? existing, CancellationToken cancellationToken)
    {
        var (code, number) = Parse(entity.Code);
        var configuration = payload.Configuration ?? new JsonObject();
        entity.IsRetired = payload.IsRetired;
        if (existing is not null)
        {
            if (!JsonNode.DeepEquals(configuration, JsonNode.Parse(existing.Configuration)))
            {
                errors.Add("configuration", $"Published versions cannot change; add version {code}/{number + 1} instead.");
            }

            return;
        }

        var ruleSetId = await refs.ResolveAsync(refs.Context.RuleSets, DecisionChangeTypes.RuleSet, code, "code", errors, cancellationToken);
        entity.RuleSetId = ruleSetId ?? Guid.Empty;
        entity.Number = number;
        entity.Configuration = configuration.ToJsonString();
        entity.PublishedAt = refs.IsValidating ? null : clock.GetUtcNow();

        var latest = await refs.Context.Versions.Where(v => v.RuleSet!.Code == code).MaxAsync(v => (int?)v.Number, cancellationToken) ?? 0;
        var pendingBefore = refs.IsValidating ? Enumerable.Range(1, Math.Max(0, number - 1)).Count(n => refs.IsPendingCreate(DecisionChangeTypes.Version, $"{code}/{n}")) : 0;
        if (number != latest + 1 + pendingBefore)
        {
            errors.Add("code", $"The next version of {code} is {latest + 1 + pendingBefore}.");
        }

        // The engine is the rule set's (a rule set created in the same change set is checked from its payload).
        var engineCode = ruleSetId is { } id
            ? await refs.Context.RuleSets.Where(r => r.Id == id).Select(r => r.Engine).SingleOrDefaultAsync(cancellationToken)
            : null;
        engineCode ??= PendingEngine(refs, code);
        var engine = engines.FirstOrDefault(e => e.Code == engineCode);
        if (engine is null)
        {
            errors.Add("configuration", $"Rule set '{code}' has no installed engine to check the configuration.");
            return;
        }

        foreach (var (field, message) in engine.Validate(configuration))
        {
            errors.Add(string.IsNullOrEmpty(field) ? "configuration" : $"configuration.{field}", message);
        }
    }

    protected override Task<RuleSetVersionPayload> ToPayloadAsync(DecisionsDbContext context, RuleSetVersion entity, CancellationToken cancellationToken) =>
        Task.FromResult(new RuleSetVersionPayload { IsRetired = entity.IsRetired, Configuration = JsonNode.Parse(entity.Configuration), PublishedAt = entity.PublishedAt });

    protected override async Task BeforeDeleteAsync(DecisionsDbContext context, RuleSetVersion entity, CancellationToken cancellationToken)
    {
        if (await context.Evaluations.AnyAsync(e => e.RuleSetCode == entity.RuleSet!.Code && e.Version == entity.Number, cancellationToken))
        {
            throw new ChangeSetException($"Version {entity.Code} has been used; retire it instead.");
        }
    }

    internal static (string RuleSet, int Number) Parse(string key)
    {
        var slash = key.LastIndexOf('/');
        return slash > 0 && int.TryParse(key[(slash + 1)..], System.Globalization.CultureInfo.InvariantCulture, out var number) ? (key[..slash], number) : (key, 0);
    }

    private static string? PendingEngine(ConfigRefs<DecisionsDbContext> refs, string code)
    {
        var item = refs.Draft?.Items.FirstOrDefault(i => i.EntityType == DecisionChangeTypes.RuleSet && i.EntityCode == code && i.After is not null);
        return item is null ? null : JsonSerializer.Deserialize<RuleSetPayload>(item.After!, ChangeJson.Options)?.Engine;
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_\\-]{0,50}/[1-9][0-9]{0,3}$")]
    private static partial Regex Key();
}
