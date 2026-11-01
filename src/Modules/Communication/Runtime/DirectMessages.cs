using DynamicCube.Modules.Communication.Changes;
using DynamicCube.Modules.Communication.Domain;
using DynamicCube.Modules.Communication.Persistence;
using DynamicCube.Platform.Communication;
using DynamicCube.Platform.Files;
using DynamicCube.Platform.Outbox;

namespace DynamicCube.Modules.Communication.Runtime;

/// <summary>
/// Emails no notification rule decides (ADR-0045): the attachments go to file storage, the message to the outbound log and
/// the outbox, which sends it like any other email and tries again on failure.
/// </summary>
internal sealed class DirectMessages(CommunicationDbContext context, IFileStorage files, TimeProvider clock) : IDirectMessages
{
    public const int MaxAttachmentBytes = 20 * 1024 * 1024;

    public async Task QueueEmailAsync(DirectEmail email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);
        if (email.Attachments.Sum(a => (long)a.Content.Length) > MaxAttachmentBytes)
        {
            throw new InvalidOperationException($"The attachments are larger than {MaxAttachmentBytes / 1024 / 1024} MB.");
        }

        var attachments = new List<MessageAttachmentRef>();
        foreach (var attachment in email.Attachments)
        {
            using var stream = new MemoryStream(attachment.Content);
            var stored = await files.SaveAsync(stream, attachment.FileName, attachment.ContentType, cancellationToken);
            attachments.Add(new MessageAttachmentRef { Key = stored.Key, FileName = attachment.FileName, ContentType = attachment.ContentType, Size = attachment.Content.Length });
        }

        var message = new OutboundMessage
        {
            EventId = Guid.CreateVersion7(),
            EventCode = email.Source,
            RuleCode = email.Source,
            Channel = Changes.Channels.Email,
            RecipientKind = RecipientKinds.User,
            Recipient = email.UserName,
            RecipientName = email.RecipientName,
            Address = email.Address,
            SubjectType = email.SubjectType,
            SubjectId = email.SubjectId,
            Title = email.Subject,
            Body = email.HtmlBody,
            Status = MessageStatus.Pending,
            CreatedAt = clock.GetUtcNow(),
            Attachments = attachments,
        };
        context.Messages.Add(message);
        context.Enqueue(new SendMessage(message.Id), clock);
        await context.SaveChangesAsync(cancellationToken);
    }
}
