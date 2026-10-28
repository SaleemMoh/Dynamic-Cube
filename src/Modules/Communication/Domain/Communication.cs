namespace DynamicCube.Modules.Communication.Domain;

/// <summary>
/// The wording of a notification for each channel, in Arabic and English, as Liquid over the event's facts
/// (<c>party</c>, <c>subject</c>, <c>request</c>, <c>document</c>, <c>task</c>…). A channel without wording is not offered.
/// </summary>
public sealed class MessageTemplate : ConfigEntity
{
    public string? EmailSubjectAr { get; set; }

    public string? EmailSubjectEn { get; set; }

    /// <summary>HTML body; values are HTML-encoded.</summary>
    public string? EmailBodyAr { get; set; }

    public string? EmailBodyEn { get; set; }

    /// <summary>Plain text.</summary>
    public string? SmsAr { get; set; }

    public string? SmsEn { get; set; }

    public string? InAppTitleAr { get; set; }

    public string? InAppTitleEn { get; set; }

    public string? InAppBodyAr { get; set; }

    public string? InAppBodyEn { get; set; }

    /// <summary>Where an in-app notification leads, e.g. <c>/requests/{{ subject.id }}</c>.</summary>
    public string? Link { get; set; }
}

/// <summary>A recipient of a rule: a kind and a JSONLogic rule giving the party id, user names or role codes.</summary>
public sealed class RuleRecipient
{
    public string Kind { get; set; } = string.Empty;

    /// <summary>JSONLogic over the facts; a constant for a fixed role or user. Parties default to <c>party.id</c>.</summary>
    public System.Text.Json.Nodes.JsonNode? Value { get; set; }
}

/// <summary>When an event of a type happens and the condition holds, the template is sent to the recipients over the channels.</summary>
public sealed class NotificationRule : ConfigEntity
{
    public string EventCode { get; set; } = string.Empty;

    /// <summary>JSONLogic over the facts (null: always).</summary>
    public string? Condition { get; set; }

    public Guid TemplateId { get; set; }

    public MessageTemplate? Template { get; set; }

    public string[] Channels { get; set; } = [];

    /// <summary>JSON list of <see cref="RuleRecipient"/>.</summary>
    public string Recipients { get; set; } = "[]";

    /// <summary>AR or EN: the language of emails and SMS (in-app notifications keep both).</summary>
    public string Language { get; set; } = "AR";
}

public enum MessageStatus
{
    Pending,
    Sent,
    Failed,
    Skipped,
}

/// <summary>One message to one recipient over one channel, with what it said and how its delivery went.</summary>
public sealed class OutboundMessage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>The event occurrence it was created for.</summary>
    public Guid EventId { get; set; }

    public string EventCode { get; set; } = string.Empty;

    public string RuleCode { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public string RecipientKind { get; set; } = string.Empty;

    /// <summary>Party id or user name.</summary>
    public string Recipient { get; set; } = string.Empty;

    public string? RecipientName { get; set; }

    /// <summary>Email address or mobile number (null for in-app).</summary>
    public string? Address { get; set; }

    public string SubjectType { get; set; } = string.Empty;

    public Guid SubjectId { get; set; }

    public string? SubjectNumber { get; set; }

    public string? Title { get; set; }

    public string Body { get; set; } = string.Empty;

    public MessageStatus Status { get; set; }

    public int Attempts { get; set; }

    public string? Error { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }
}

/// <summary>A notification shown in the portal (to everyone acting for the party) or in the internal system (to the user).</summary>
public sealed class Notification
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid MessageId { get; set; }

    public Guid? PartyId { get; set; }

    public string? UserName { get; set; }

    public string EventCode { get; set; } = string.Empty;

    public string TitleAr { get; set; } = string.Empty;

    public string TitleEn { get; set; } = string.Empty;

    public string BodyAr { get; set; } = string.Empty;

    public string BodyEn { get; set; } = string.Empty;

    public string? Link { get; set; }

    public string SubjectType { get; set; } = string.Empty;

    public Guid SubjectId { get; set; }

    public string? SubjectNumber { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ReadAt { get; set; }
}
