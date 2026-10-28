using DynamicCube.Modules.Documents.Domain;
using DynamicCube.Modules.Documents.Persistence;
using DynamicCube.Platform.Outbox;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Documents.Runtime;

/// <summary>Prints a version to PDF in the worker (idempotent: an already printed version is left alone).</summary>
internal sealed class RenderDocumentHandler(DocumentsDbContext context, DocumentRegistry registry) : IOutboxHandler<RenderDocumentMessage>
{
    public async Task HandleAsync(RenderDocumentMessage message, CancellationToken cancellationToken)
    {
        var version = await context.Versions.SingleOrDefaultAsync(v => v.Id == message.VersionId, cancellationToken);
        if (version is { FileKey: null })
        {
            await registry.RenderAsync(version, cancellationToken);
        }
    }
}

/// <summary>
/// Expires documents past their date and reminds holders on the reminder days of their type (run periodically by the
/// worker); returns how many documents expired or were reminded.
/// </summary>
internal sealed class DocumentDates(DocumentsDbContext context, DocumentRenderer renderer, TimeProvider clock)
{
    private const int BatchSize = 500;

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var types = await context.DocumentTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, cancellationToken);
        var expired = await context.Documents
            .Where(d => (d.Status == DocumentStatus.Active || d.Status == DocumentStatus.Suspended) && d.ExpiresAt <= now)
            .Take(BatchSize).ToListAsync(cancellationToken);
        foreach (var document in expired)
        {
            document.Status = DocumentStatus.Expired;
            document.StatusChangedAt = now;
            context.Events.Add(new DocumentEvent { DocumentId = document.Id, At = now, Type = DocumentEventTypes.Expired, Actor = "system", Version = document.CurrentVersion });
            DocumentNotifications.Raise(context, clock, DocumentNotifications.Expired, document, types[document.DocumentTypeId], renderer.VerifyUrl(document.VerificationCode), null);
        }

        var reminded = 0;
        foreach (var type in types.Values.Where(t => t.AlertDaysBefore.Length > 0))
        {
            var horizon = now.AddDays(type.AlertDaysBefore.Max());
            var due = await context.Documents
                .Where(d => d.DocumentTypeId == type.Id && d.Status == DocumentStatus.Active && d.ExpiresAt > now && d.ExpiresAt <= horizon)
                .OrderBy(d => d.ExpiresAt).Take(BatchSize).ToListAsync(cancellationToken);
            foreach (var document in due)
            {
                var daysLeft = (int)Math.Ceiling((document.ExpiresAt!.Value - now).TotalDays);
                var reached = type.AlertDaysBefore.Where(d => d >= daysLeft && !document.AlertsSent.Contains(d)).ToList();
                if (reached.Count == 0)
                {
                    continue;
                }

                // A late run sends only the nearest reminder; the earlier ones are not sent afterwards.
                var alert = reached.Min();
                document.AlertsSent = [.. document.AlertsSent.Concat(reached).Order()];
                context.Events.Add(new DocumentEvent { DocumentId = document.Id, At = now, Type = DocumentEventTypes.ExpiryReminder, Actor = "system", Version = document.CurrentVersion, Comment = $"{daysLeft}" });
                DocumentNotifications.Raise(context, clock, DocumentNotifications.Expiring, document, type, renderer.VerifyUrl(document.VerificationCode), null, (alert, daysLeft));
                reminded++;
            }
        }

        if (expired.Count + reminded > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return expired.Count + reminded;
    }
}
