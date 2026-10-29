using System.Text.Json.Nodes;
using DynamicCube.Modules.Documents.Domain;
using DynamicCube.Modules.Documents.Persistence;
using DynamicCube.Platform.Documents;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Documents.Runtime;

/// <summary>Issued documents as other modules see them: status, dates and the data and lines of the current version.</summary>
internal sealed class DocumentDirectory(DocumentsDbContext context) : IDocumentDirectory
{
    public async Task<DocumentFacts?> GetAsync(Guid documentId, CancellationToken cancellationToken) =>
        (await QueryAsync(context.Documents.Where(d => d.Id == documentId), cancellationToken)).SingleOrDefault();

    public async Task<IReadOnlyList<DocumentFacts>> OfHolderAsync(Guid holderPartyId, string documentType, CancellationToken cancellationToken) =>
        await QueryAsync(context.Documents.Where(d => d.HolderPartyId == holderPartyId && d.TypeCode == documentType && d.Status != DocumentStatus.Cancelled), cancellationToken);

    public async Task<(string NameAr, string NameEn)?> TypeAsync(string documentType, CancellationToken cancellationToken)
    {
        var type = await context.DocumentTypes.AsNoTracking().Where(t => t.Code == documentType).Select(t => new { t.NameAr, t.NameEn }).SingleOrDefaultAsync(cancellationToken);
        return type is null ? null : (type.NameAr, type.NameEn);
    }

    private async Task<List<DocumentFacts>> QueryAsync(IQueryable<IssuedDocument> documents, CancellationToken cancellationToken)
    {
        var rows = await documents.AsNoTracking()
            .OrderByDescending(d => d.IssuedAt)
            .Join(context.Versions.AsNoTracking(), d => new { d.Id, Version = d.CurrentVersion }, v => new { Id = v.DocumentId, v.Version }, (d, v) => new { Document = d, v.Data, v.Lines })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(r => new DocumentFacts(
            r.Document.Id,
            r.Document.TypeCode,
            r.Document.Number,
            r.Document.HolderPartyId,
            r.Document.Status.ToString().ToUpperInvariant(),
            r.Document.IssuedAt,
            r.Document.ExpiresAt,
            r.Document.CurrentVersion,
            JsonNode.Parse(r.Data)!.AsObject(),
            JsonNode.Parse(r.Lines)!.AsArray()))];
    }
}
