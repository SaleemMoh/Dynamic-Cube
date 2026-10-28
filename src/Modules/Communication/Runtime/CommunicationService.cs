using System.Text.Json.Nodes;
using DynamicCube.Modules.Communication.Changes;
using DynamicCube.Modules.Communication.Domain;
using DynamicCube.Modules.Communication.Persistence;
using DynamicCube.Platform.Communication;
using DynamicCube.Platform.Outbox;
using DynamicCube.Platform.Security;
using DynamicCube.Platform.Templates;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Communication.Runtime;

public sealed record EventTypeView(string Code, string NameAr, string NameEn, string Module, JsonObject Sample);

public sealed record MessageSummary(
    Guid Id, DateTimeOffset CreatedAt, string EventCode, string RuleCode, string Channel, string RecipientKind, string Recipient, string? RecipientName,
    string? Address, string SubjectType, Guid SubjectId, string? SubjectNumber, string? Title, MessageStatus Status, int Attempts, string? Error, DateTimeOffset? SentAt);

public sealed record MessageView(MessageSummary Message, string Body);

public sealed record MessagePage(IReadOnlyList<MessageSummary> Items, int Total);

public sealed record NotificationView(
    Guid Id, string EventCode, string TitleAr, string TitleEn, string BodyAr, string BodyEn, string? Link, string SubjectType, Guid SubjectId, string? SubjectNumber,
    DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);

public sealed record NotificationPage(IReadOnlyList<NotificationView> Items, int Unread);

/// <summary>A template's wording tried on an event type's sample facts.</summary>
public sealed record TemplatePreviewRequest(string EventCode, MessageTemplatePayload Template);

public sealed record ChannelPreview(string Channel, string? TitleAr, string? TitleEn, string BodyAr, string BodyEn);

/// <summary>Who notifications are for: a party in the portal, an employee in the internal system.</summary>
public sealed record NotificationOwner(Guid? PartyId, string? UserName);

public sealed class CommunicationNotFoundException(string message) : Exception(message);

public sealed class CommunicationConflictException(string message) : Exception(message);

/// <summary>The message log, template previews and the notifications people read.</summary>
internal sealed class CommunicationService(
    CommunicationDbContext context,
    IEnumerable<CommunicationEventType> events,
    LiquidRenderer liquid,
    ICurrentUser user,
    TimeProvider clock)
{
    private const int MaxTake = 200;

    public IReadOnlyList<EventTypeView> Events() =>
        [.. events.OrderBy(e => e.Module).ThenBy(e => e.Code).Select(e => new EventTypeView(e.Code, e.NameAr, e.NameEn, e.Module, Facts.Sample(e)))];

    public IReadOnlyList<ChannelPreview> Preview(TemplatePreviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var type = events.FirstOrDefault(e => e.Code == request.EventCode) ?? throw new CommunicationNotFoundException($"No module raises '{request.EventCode}'.");
        var model = Facts.Sample(type);
        model["recipient"] = new JsonObject { ["name"] = "مؤسسة المثال", ["kind"] = RecipientKinds.Party };
        var t = request.Template;
        var previews = new List<ChannelPreview>();
        foreach (var channel in t.ChannelsCovered())
        {
            previews.Add(channel switch
            {
                Changes.Channels.Email => new(channel, Text(t.EmailSubjectAr, model), Text(t.EmailSubjectEn, model), liquid.RenderHtml(t.EmailBodyAr!, model), liquid.RenderHtml(t.EmailBodyEn!, model)),
                Changes.Channels.Sms => new(channel, null, null, Text(t.SmsAr, model)!, Text(t.SmsEn, model)!),
                _ => new(channel, Text(t.InAppTitleAr, model), Text(t.InAppTitleEn, model), Text(t.InAppBodyAr, model)!, Text(t.InAppBodyEn, model)!),
            });
        }

        return previews;
    }

    public async Task<MessagePage> SearchAsync(string? search, MessageStatus? status, string? channel, string? eventCode, Guid? subjectId, int skip, int take, CancellationToken cancellationToken)
    {
        var query = context.Messages.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(m => m.SubjectNumber == term || m.Recipient == term || m.Address == term || EF.Functions.ILike(m.RecipientName ?? string.Empty, "%" + term + "%"));
        }

        if (status is { } s)
        {
            query = query.Where(m => m.Status == s);
        }

        if (!string.IsNullOrWhiteSpace(channel))
        {
            query = query.Where(m => m.Channel == channel);
        }

        if (!string.IsNullOrWhiteSpace(eventCode))
        {
            query = query.Where(m => m.EventCode == eventCode);
        }

        if (subjectId is { } subject)
        {
            query = query.Where(m => m.SubjectId == subject);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, MaxTake)).ToListAsync(cancellationToken);
        return new MessagePage([.. items.Select(Summary)], total);
    }

    public async Task<MessageView> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var message = await context.Messages.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, cancellationToken) ?? throw new CommunicationNotFoundException($"Message {id} does not exist.");
        return new MessageView(Summary(message), message.Body);
    }

    /// <summary>Sends a failed email or SMS again (e.g. after the mail server is back or the address was corrected upstream).</summary>
    public async Task<MessageView> ResendAsync(Guid id, CancellationToken cancellationToken)
    {
        var message = await context.Messages.SingleOrDefaultAsync(m => m.Id == id, cancellationToken) ?? throw new CommunicationNotFoundException($"Message {id} does not exist.");
        if (message.Status != MessageStatus.Failed)
        {
            throw new CommunicationConflictException("Only failed messages can be sent again.");
        }

        message.Status = MessageStatus.Pending;
        message.Error = null;
        context.Enqueue(new SendMessage(message.Id), clock);
        await context.SaveChangesAsync(cancellationToken);
        return new MessageView(Summary(message), message.Body);
    }

    public async Task<NotificationPage> NotificationsAsync(NotificationOwner owner, bool unreadOnly, int take, CancellationToken cancellationToken)
    {
        var mine = Mine(owner);
        var unread = await mine.CountAsync(n => n.ReadAt == null, cancellationToken);
        var items = await (unreadOnly ? mine.Where(n => n.ReadAt == null) : mine)
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Take(Math.Clamp(take, 1, MaxTake))
            .Select(n => new NotificationView(n.Id, n.EventCode, n.TitleAr, n.TitleEn, n.BodyAr, n.BodyEn, n.Link, n.SubjectType, n.SubjectId, n.SubjectNumber, n.CreatedAt, n.ReadAt))
            .ToListAsync(cancellationToken);
        return new NotificationPage(items, unread);
    }

    public Task<int> UnreadAsync(NotificationOwner owner, CancellationToken cancellationToken) => Mine(owner).CountAsync(n => n.ReadAt == null, cancellationToken);

    /// <summary>Marks one notification (or, without an id, all of them) as read.</summary>
    public async Task ReadAsync(NotificationOwner owner, Guid? id, CancellationToken cancellationToken)
    {
        var query = Mine(owner).Where(n => n.ReadAt == null);
        if (id is { } one)
        {
            query = query.Where(n => n.Id == one);
        }

        var now = clock.GetUtcNow();
        await query.ExecuteUpdateAsync(u => u.SetProperty(n => n.ReadAt, now), cancellationToken);
    }

    /// <summary>The signed-in employee's notifications.</summary>
    public NotificationOwner Employee() => new(null, user.UserName ?? throw new CommunicationNotFoundException("Not signed in."));

    private IQueryable<Notification> Mine(NotificationOwner owner) =>
        owner.PartyId is { } party ? context.Notifications.Where(n => n.PartyId == party)
        : owner.UserName is { } name ? context.Notifications.Where(n => n.UserName == name)
        : context.Notifications.Where(_ => false);

    private string? Text(string? source, JsonObject model) => source is null ? null : liquid.RenderText(source, model);

    private static MessageSummary Summary(OutboundMessage m) => new(
        m.Id, m.CreatedAt, m.EventCode, m.RuleCode, m.Channel, m.RecipientKind, m.Recipient, m.RecipientName, m.Address, m.SubjectType, m.SubjectId, m.SubjectNumber,
        m.Title, m.Status, m.Attempts, m.Error, m.SentAt);
}

public sealed class CommunicationValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
