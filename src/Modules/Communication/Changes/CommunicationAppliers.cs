using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DynamicCube.Modules.Communication.Domain;
using DynamicCube.Modules.Communication.Persistence;
using DynamicCube.Modules.Communication.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Communication;
using DynamicCube.Platform.Expressions;
using DynamicCube.Platform.Templates;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Communication.Changes;

internal abstract class CommunicationApplierBase<TEntity, TPayload>(CommunicationDbContext context)
    : ConfigApplier<CommunicationDbContext, ConfigRefs<CommunicationDbContext>, TEntity, TPayload>(context)
    where TEntity : ConfigEntity, new()
    where TPayload : ConfigPayload, new()
{
    public override string Area => CommunicationChangeTypes.Area;

    public override string ResourceCode => CommunicationResources.Config;

    protected override Regex CodePattern => ValidationErrors.IdentifierCode();

    protected override ConfigRefs<CommunicationDbContext> CreateRefs(CommunicationDbContext context, ChangeSet? draft) => new(context, draft);
}

public sealed class MessageTemplatePayload : ConfigPayload
{
    public string? EmailSubjectAr { get; set; }

    public string? EmailSubjectEn { get; set; }

    public string? EmailBodyAr { get; set; }

    public string? EmailBodyEn { get; set; }

    public string? SmsAr { get; set; }

    public string? SmsEn { get; set; }

    public string? InAppTitleAr { get; set; }

    public string? InAppTitleEn { get; set; }

    public string? InAppBodyAr { get; set; }

    public string? InAppBodyEn { get; set; }

    public string? Link { get; set; }

    /// <summary>The channels the wording covers: a channel needs all of its Arabic and English fields.</summary>
    internal IEnumerable<string> ChannelsCovered()
    {
        if (Filled(EmailSubjectAr, EmailSubjectEn, EmailBodyAr, EmailBodyEn))
        {
            yield return Channels.Email;
        }

        if (Filled(SmsAr, SmsEn))
        {
            yield return Channels.Sms;
        }

        if (Filled(InAppTitleAr, InAppTitleEn, InAppBodyAr, InAppBodyEn))
        {
            yield return Channels.InApp;
        }
    }

    private static bool Filled(params string?[] values) => values.All(v => !string.IsNullOrWhiteSpace(v));
}

internal sealed class MessageTemplateApplier(CommunicationDbContext db) : CommunicationApplierBase<MessageTemplate, MessageTemplatePayload>(db)
{
    public override string EntityType => CommunicationChangeTypes.Template;

    protected override IQueryable<MessageTemplate> Query(CommunicationDbContext context) => context.Templates;

    protected override Task ApplyAsync(MessageTemplate entity, MessageTemplatePayload payload, ConfigRefs<CommunicationDbContext> refs, ValidationErrors errors, MessageTemplate? existing, CancellationToken cancellationToken)
    {
        var fields = new (string Name, string? Value, int Max)[]
        {
            ("emailSubjectAr", payload.EmailSubjectAr, 1000), ("emailSubjectEn", payload.EmailSubjectEn, 1000),
            ("emailBodyAr", payload.EmailBodyAr, 200_000), ("emailBodyEn", payload.EmailBodyEn, 200_000),
            ("smsAr", payload.SmsAr, 2000), ("smsEn", payload.SmsEn, 2000),
            ("inAppTitleAr", payload.InAppTitleAr, 1000), ("inAppTitleEn", payload.InAppTitleEn, 1000),
            ("inAppBodyAr", payload.InAppBodyAr, 4000), ("inAppBodyEn", payload.InAppBodyEn, 4000),
            ("link", payload.Link, 1000),
        };
        foreach (var (name, value, max) in fields)
        {
            errors.MaxLength(name, value, max);
            if (!string.IsNullOrWhiteSpace(value) && !LiquidRenderer.TryParse(value, out var error))
            {
                errors.Add(name, $"The template does not parse: {error}");
            }
        }

        if (!payload.ChannelsCovered().Any())
        {
            errors.Add("channels", "Word the message for at least one channel: email (subject and body), SMS, or in-app (title and body), each in Arabic and English.");
        }

        entity.EmailSubjectAr = Clean(payload.EmailSubjectAr);
        entity.EmailSubjectEn = Clean(payload.EmailSubjectEn);
        entity.EmailBodyAr = Clean(payload.EmailBodyAr);
        entity.EmailBodyEn = Clean(payload.EmailBodyEn);
        entity.SmsAr = Clean(payload.SmsAr);
        entity.SmsEn = Clean(payload.SmsEn);
        entity.InAppTitleAr = Clean(payload.InAppTitleAr);
        entity.InAppTitleEn = Clean(payload.InAppTitleEn);
        entity.InAppBodyAr = Clean(payload.InAppBodyAr);
        entity.InAppBodyEn = Clean(payload.InAppBodyEn);
        entity.Link = Clean(payload.Link);
        return Task.CompletedTask;
    }

    protected override Task<MessageTemplatePayload> ToPayloadAsync(CommunicationDbContext context, MessageTemplate entity, CancellationToken cancellationToken) =>
        Task.FromResult(ToPayload(entity));

    protected override async Task BeforeDeleteAsync(CommunicationDbContext context, MessageTemplate entity, CancellationToken cancellationToken)
    {
        if (await context.Rules.AnyAsync(r => r.TemplateId == entity.Id, cancellationToken))
        {
            throw new ChangeSetException($"Template '{entity.Code}' is used by a notification rule.");
        }
    }

    internal static MessageTemplatePayload ToPayload(MessageTemplate entity) => new()
    {
        EmailSubjectAr = entity.EmailSubjectAr,
        EmailSubjectEn = entity.EmailSubjectEn,
        EmailBodyAr = entity.EmailBodyAr,
        EmailBodyEn = entity.EmailBodyEn,
        SmsAr = entity.SmsAr,
        SmsEn = entity.SmsEn,
        InAppTitleAr = entity.InAppTitleAr,
        InAppTitleEn = entity.InAppTitleEn,
        InAppBodyAr = entity.InAppBodyAr,
        InAppBodyEn = entity.InAppBodyEn,
        Link = entity.Link,
    };

    // Liquid is whitespace-sensitive, so only an all-blank value is dropped.
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

public sealed class RuleRecipientPayload
{
    public string Kind { get; set; } = string.Empty;

    public JsonNode? Value { get; set; }
}

public sealed class NotificationRulePayload : ConfigPayload
{
    public string? EventCode { get; set; }

    public JsonNode? Condition { get; set; }

    public string? TemplateCode { get; set; }

    public List<string> Channels { get; set; } = [];

    public List<RuleRecipientPayload> Recipients { get; set; } = [];

    public string? Language { get; set; }
}

internal sealed class NotificationRuleApplier(CommunicationDbContext db, IEnumerable<CommunicationEventType> events, JsonLogicEvaluator evaluator)
    : CommunicationApplierBase<NotificationRule, NotificationRulePayload>(db)
{
    public override string EntityType => CommunicationChangeTypes.Rule;

    protected override IQueryable<NotificationRule> Query(CommunicationDbContext context) => context.Rules.Include(r => r.Template);

    protected override async Task ApplyAsync(NotificationRule entity, NotificationRulePayload payload, ConfigRefs<CommunicationDbContext> refs, ValidationErrors errors, NotificationRule? existing, CancellationToken cancellationToken)
    {
        errors.Required("eventCode", payload.EventCode);
        var eventCode = payload.EventCode?.Trim() ?? string.Empty;
        var type = events.FirstOrDefault(e => e.Code == eventCode);
        if (eventCode.Length > 0 && type is null)
        {
            errors.Add("eventCode", $"No module raises '{eventCode}'.");
        }

        var sample = type is null ? [] : Facts.Sample(type);
        var condition = payload.Condition;
        if (condition is not null)
        {
            Try("condition", () => evaluator.Evaluate(condition, sample), errors);
        }

        var channels = payload.Channels.Select(c => c.Trim().ToUpperInvariant()).Distinct().ToList();
        if (channels.Count == 0)
        {
            errors.Add("channels", "Choose at least one channel.");
        }

        foreach (var channel in channels.Where(c => !Changes.Channels.All.Contains(c)))
        {
            errors.Add("channels", $"Unknown channel '{channel}' (use {string.Join(", ", Changes.Channels.All)}).");
        }

        if (payload.Recipients.Count == 0)
        {
            errors.Add("recipients", "Add at least one recipient.");
        }

        foreach (var (recipient, i) in payload.Recipients.Select((r, i) => (r, i)))
        {
            var kind = recipient.Kind?.Trim().ToUpperInvariant() ?? string.Empty;
            recipient.Kind = kind;
            if (!RecipientKinds.All.Contains(kind))
            {
                errors.Add($"recipients[{i}].kind", $"Use {string.Join(", ", RecipientKinds.All)}.");
            }
            else if (kind != RecipientKinds.Party && recipient.Value is null)
            {
                errors.Add($"recipients[{i}].value", "Give the user names or role codes (a value or a rule over the facts).");
            }

            if (recipient.Value is not null)
            {
                Try($"recipients[{i}].value", () => evaluator.Evaluate(recipient.Value, sample), errors);
            }
        }

        var language = string.IsNullOrWhiteSpace(payload.Language) ? "AR" : payload.Language.Trim().ToUpperInvariant();
        if (language is not ("AR" or "EN"))
        {
            errors.Add("language", "Use AR or EN.");
        }

        errors.Required("templateCode", payload.TemplateCode);
        var templateId = await refs.ResolveAsync(refs.Context.Templates, CommunicationChangeTypes.Template, payload.TemplateCode, "templateCode", errors, cancellationToken);
        if (templateId is not null && await WordingAsync(refs, templateId.Value, payload.TemplateCode!, cancellationToken) is { } wording)
        {
            var covered = wording.ChannelsCovered().ToList();
            foreach (var channel in channels.Where(c => Changes.Channels.All.Contains(c) && !covered.Contains(c)))
            {
                errors.Add("channels", $"Template '{payload.TemplateCode}' has no {channel} wording.");
            }
        }

        entity.EventCode = eventCode;
        entity.Condition = condition?.ToJsonString();
        entity.Channels = [.. channels];
        entity.Recipients = JsonSerializer.Serialize(payload.Recipients.Select(r => new RuleRecipient { Kind = r.Kind, Value = r.Value?.DeepClone() }).ToList(), ChangeJson.Options);
        entity.Language = language;
        entity.TemplateId = templateId ?? Guid.Empty;
    }

    protected override Task<NotificationRulePayload> ToPayloadAsync(CommunicationDbContext context, NotificationRule entity, CancellationToken cancellationToken) =>
        Task.FromResult(new NotificationRulePayload
        {
            EventCode = entity.EventCode,
            Condition = entity.Condition is null ? null : JsonNode.Parse(entity.Condition),
            TemplateCode = entity.Template?.Code,
            Channels = [.. entity.Channels],
            Recipients = [.. Recipients(entity.Recipients).Select(r => new RuleRecipientPayload { Kind = r.Kind, Value = r.Value })],
            Language = entity.Language,
        });

    internal static List<RuleRecipient> Recipients(string json) => JsonSerializer.Deserialize<List<RuleRecipient>>(json, ChangeJson.Options) ?? [];

    private static async Task<MessageTemplatePayload?> WordingAsync(ConfigRefs<CommunicationDbContext> refs, Guid id, string code, CancellationToken cancellationToken)
    {
        if (id != Guid.Empty && await refs.Context.Templates.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, cancellationToken) is { } stored)
        {
            return MessageTemplateApplier.ToPayload(stored);
        }

        var item = refs.Draft?.Items.LastOrDefault(i => i.EntityType == CommunicationChangeTypes.Template && i.EntityCode == code && i.After is not null);
        return item is null ? null : JsonSerializer.Deserialize<MessageTemplatePayload>(item.After!, ChangeJson.Options);
    }

    private static void Try(string field, Func<JsonNode?> evaluate, ValidationErrors errors)
    {
        try
        {
            evaluate();
        }
        catch (JsonLogicException ex)
        {
            errors.Add(field, $"The rule cannot be evaluated: {ex.Message}");
        }
    }
}
