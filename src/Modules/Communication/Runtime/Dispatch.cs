using System.Text.Json.Nodes;
using DynamicCube.Modules.Communication.Changes;
using DynamicCube.Modules.Communication.Domain;
using DynamicCube.Modules.Communication.Persistence;
using DynamicCube.Platform.Communication;
using DynamicCube.Platform.Expressions;
using DynamicCube.Platform.Outbox;
using DynamicCube.Platform.Parties;
using DynamicCube.Platform.Security;
using DynamicCube.Platform.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DynamicCube.Modules.Communication.Runtime;

/// <summary>Delivers one email or SMS message (written to the outbox with the message).</summary>
public sealed record SendMessage(Guid MessageId) : IOutboxMessage
{
    public static string MessageType => "comm.send";
}

/// <summary>A person a message goes to, with the addresses known for them.</summary>
internal sealed record Recipient(string Kind, string Key, string? Name, string? Email, string? Mobile, Guid? PartyId, string? UserName);

/// <summary>
/// Matches an event against the active rules of its type and creates the messages: in-app notifications at once, emails and
/// SMS through the outbox. An event delivered again creates nothing new.
/// </summary>
internal sealed class EventDispatcher(
    CommunicationDbContext context,
    IPartyFacts parties,
    IStaffDirectory staff,
    JsonLogicEvaluator evaluator,
    LiquidRenderer liquid,
    IEnumerable<IMessageChannel> channels,
    IOptions<CommunicationOptions> options,
    TimeProvider clock) : IOutboxHandler<CommunicationEvent>
{
    private const int MaxRecipients = 500;
    private readonly CommunicationOptions _options = options.Value;

    public async Task HandleAsync(CommunicationEvent message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (await context.Messages.AnyAsync(m => m.EventId == message.Id, cancellationToken))
        {
            return;
        }

        var rules = await context.Rules.AsNoTracking().Include(r => r.Template)
            .Where(r => r.EventCode == message.Code && r.IsActive && r.Template!.IsActive)
            .OrderBy(r => r.Code).ToListAsync(cancellationToken);
        if (rules.Count == 0)
        {
            return;
        }

        var now = clock.GetUtcNow();
        var party = message.PartyId is { } partyId ? await parties.GetAsync(partyId, refreshProfile: false, cancellationToken) : null;
        var facts = Facts.Of(message, party, now);
        foreach (var rule in rules)
        {
            List<Recipient> recipients;
            try
            {
                if (rule.Condition is { } condition && !evaluator.EvaluateTruthy(JsonNode.Parse(condition), facts))
                {
                    continue;
                }

                recipients = await RecipientsAsync(rule, facts, cancellationToken);
            }
            catch (JsonLogicException ex)
            {
                // Recorded in the log so the rule can be fixed; the event's other rules still run.
                context.Messages.Add(new OutboundMessage
                {
                    EventId = message.Id,
                    EventCode = message.Code,
                    RuleCode = rule.Code,
                    Channel = "-",
                    RecipientKind = "-",
                    Recipient = "-",
                    SubjectType = message.SubjectType,
                    SubjectId = message.SubjectId,
                    SubjectNumber = message.SubjectNumber,
                    CreatedAt = now,
                    Status = MessageStatus.Skipped,
                    Error = $"The rule cannot be evaluated: {ex.Message}",
                });
                continue;
            }

            foreach (var recipient in recipients)
            {
                foreach (var channel in rule.Channels)
                {
                    Create(message, rule, recipient, channel, facts, now);
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private void Create(CommunicationEvent e, NotificationRule rule, Recipient recipient, string channel, JsonObject facts, DateTimeOffset now)
    {
        var template = rule.Template!;
        var arabic = rule.Language != "EN";
        var model = (JsonObject)facts.DeepClone();
        model["recipient"] = new JsonObject { ["name"] = recipient.Name, ["kind"] = recipient.Kind };
        var entry = new OutboundMessage
        {
            EventId = e.Id,
            EventCode = e.Code,
            RuleCode = rule.Code,
            Channel = channel,
            RecipientKind = recipient.Kind,
            Recipient = recipient.Key,
            RecipientName = recipient.Name,
            SubjectType = e.SubjectType,
            SubjectId = e.SubjectId,
            SubjectNumber = e.SubjectNumber,
            CreatedAt = now,
            Status = MessageStatus.Pending,
        };
        context.Messages.Add(entry);

        try
        {
            switch (channel)
            {
                case Changes.Channels.InApp:
                    var notification = new Notification
                    {
                        MessageId = entry.Id,
                        PartyId = recipient.PartyId,
                        UserName = recipient.UserName,
                        EventCode = e.Code,
                        TitleAr = Text(template.InAppTitleAr, model),
                        TitleEn = Text(template.InAppTitleEn, model),
                        BodyAr = Text(template.InAppBodyAr, model),
                        BodyEn = Text(template.InAppBodyEn, model),
                        Link = template.Link is null ? null : Text(template.Link, model).Trim(),
                        SubjectType = e.SubjectType,
                        SubjectId = e.SubjectId,
                        SubjectNumber = e.SubjectNumber,
                        CreatedAt = now,
                    };
                    entry.Title = arabic ? notification.TitleAr : notification.TitleEn;
                    entry.Body = arabic ? notification.BodyAr : notification.BodyEn;
                    entry.Status = MessageStatus.Sent;
                    entry.SentAt = now;
                    context.Notifications.Add(notification);
                    return;
                case Changes.Channels.Email:
                    entry.Address = recipient.Email;
                    entry.Title = Text(arabic ? template.EmailSubjectAr : template.EmailSubjectEn, model).Trim();
                    entry.Body = Html(arabic ? template.EmailBodyAr : template.EmailBodyEn, model, arabic);
                    break;
                default:
                    entry.Address = recipient.Mobile;
                    entry.Body = Text(arabic ? template.SmsAr : template.SmsEn, model).Trim();
                    break;
            }
        }
        catch (TemplateException ex)
        {
            Skip(entry, $"The template cannot be rendered: {ex.Message}");
            return;
        }

        if (string.IsNullOrWhiteSpace(entry.Address))
        {
            Skip(entry, channel == Changes.Channels.Email ? "The recipient has no email address." : "The recipient has no mobile number.");
        }
        else if (!channels.Any(c => c.Code == channel && c.IsConfigured))
        {
            Skip(entry, $"{channel} is not configured on this installation.");
        }
        else
        {
            context.Enqueue(new SendMessage(entry.Id), clock);
        }
    }

    private static void Skip(OutboundMessage entry, string reason)
    {
        entry.Status = MessageStatus.Skipped;
        entry.Error = reason;
    }

    private string Text(string? source, JsonObject model) => source is null ? string.Empty : liquid.RenderText(source, model);

    private string Html(string? source, JsonObject model, bool arabic)
    {
        var body = source is null ? string.Empty : liquid.RenderHtml(source, model);
        return body.Contains("<html", StringComparison.OrdinalIgnoreCase)
            ? body
            : $"""<!doctype html><html lang="{(arabic ? "ar" : "en")}" dir="{(arabic ? "rtl" : "ltr")}"><head><meta charset="utf-8"></head><body style="font-family: 'IBM Plex Sans Arabic', Tahoma, sans-serif">{body}</body></html>""";
    }

    private async Task<List<Recipient>> RecipientsAsync(NotificationRule rule, JsonObject facts, CancellationToken cancellationToken)
    {
        var found = new List<Recipient>();
        foreach (var definition in NotificationRuleApplier.Recipients(rule.Recipients))
        {
            var rawValue = definition.Value ?? (definition.Kind == RecipientKinds.Party ? new JsonObject { ["var"] = "party.id" } : null);
            var keys = Values(evaluator.Evaluate(rawValue, facts));
            switch (definition.Kind)
            {
                case RecipientKinds.Party:
                    foreach (var key in keys)
                    {
                        if (Guid.TryParse(key, out var id) && await parties.GetAsync(id, refreshProfile: false, cancellationToken) is { } party)
                        {
                            var name = rule.Language == "EN" ? party.NameEn : party.NameAr;
                            found.Add(new Recipient(RecipientKinds.Party, id.ToString(), name, Profile(party, _options.PartyEmailField), Profile(party, _options.PartyMobileField), id, null));
                        }
                    }

                    break;
                case RecipientKinds.User:
                    foreach (var key in keys)
                    {
                        if (await staff.FindAsync(key, cancellationToken) is { } member)
                        {
                            found.Add(Staff(member, rule));
                        }
                    }

                    break;
                case RecipientKinds.Role:
                    // A task's role: its holders in the task's company and branch (ADR-0036 step 6).
                    var company = (facts["task"] as JsonObject)?["company"]?.GetValue<string>();
                    var branch = (facts["task"] as JsonObject)?["branch"]?.GetValue<string>();
                    foreach (var key in keys)
                    {
                        found.AddRange((await staff.SearchAsync(new StaffQuery(Role: key, Company: company, Branch: branch), cancellationToken)).Select(m => Staff(m, rule)));
                    }

                    break;
            }
        }

        return [.. found.DistinctBy(r => (r.Kind, r.Key)).Take(MaxRecipients)];
    }

    private static Recipient Staff(StaffMember member, NotificationRule rule) =>
        new(RecipientKinds.User, member.UserName, rule.Language == "EN" ? member.NameEn : member.NameAr, member.Email, member.Mobile, null, member.UserName);

    private static string? Profile(PartyFacts party, string field) =>
        party.Profile.TryGetValue(field, out var value) && value is JsonValue v && v.ToString() is { Length: > 0 } text ? text : null;

    private static IEnumerable<string> Values(JsonNode? node) => node switch
    {
        JsonArray array => array.SelectMany(Values),
        JsonValue value when value.ToString() is { Length: > 0 } text => [text],
        _ => [],
    };
}

/// <summary>Sends one email or SMS; a failure is recorded on the message and the outbox tries again later.</summary>
internal sealed class MessageSender(CommunicationDbContext context, IEnumerable<IMessageChannel> channels, TimeProvider clock) : IOutboxHandler<SendMessage>
{
    public async Task HandleAsync(SendMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var entry = await context.Messages.SingleOrDefaultAsync(m => m.Id == message.MessageId, cancellationToken);
        if (entry is null or { Status: MessageStatus.Sent or MessageStatus.Skipped })
        {
            return;
        }

        var channel = channels.FirstOrDefault(c => c.Code == entry.Channel && c.IsConfigured);
        if (channel is null)
        {
            entry.Status = MessageStatus.Skipped;
            entry.Error = $"{entry.Channel} is not configured on this installation.";
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        entry.Attempts++;
        try
        {
            await channel.SendAsync(entry, cancellationToken);
        }
        catch (MessageDeliveryException ex)
        {
            entry.Status = MessageStatus.Failed;
            entry.Error = ex.Message.Length > 4000 ? ex.Message[..4000] : ex.Message;
            await context.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        entry.Status = MessageStatus.Sent;
        entry.Error = null;
        entry.SentAt = clock.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken);
    }
}
