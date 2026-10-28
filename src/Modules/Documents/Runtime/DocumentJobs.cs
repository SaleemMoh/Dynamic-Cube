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

/// <summary>Expires documents past their date (run periodically by the worker); returns how many expired.</summary>
internal sealed class DocumentDates(DocumentsDbContext context, TimeProvider clock)
{
    private const int BatchSize = 500;

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var expired = await context.Documents
            .Where(d => (d.Status == DocumentStatus.Active || d.Status == DocumentStatus.Suspended) && d.ExpiresAt <= now)
            .Take(BatchSize).ToListAsync(cancellationToken);
        foreach (var document in expired)
        {
            document.Status = DocumentStatus.Expired;
            document.StatusChangedAt = now;
            context.Events.Add(new DocumentEvent { DocumentId = document.Id, At = now, Type = DocumentEventTypes.Expired, Actor = "system", Version = document.CurrentVersion });
        }

        if (expired.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return expired.Count;
    }
}
