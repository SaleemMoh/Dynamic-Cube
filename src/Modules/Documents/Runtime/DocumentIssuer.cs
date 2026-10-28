using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Documents.Domain;
using DynamicCube.Modules.Documents.Persistence;
using DynamicCube.Platform.Documents;
using DynamicCube.Platform.Outbox;
using DynamicCube.Platform.Parties;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Documents.Runtime;

/// <summary>Asks the worker to print a version to PDF (also done on first download if the worker has not yet).</summary>
public sealed record RenderDocumentMessage(Guid VersionId) : IOutboxMessage
{
    public static string MessageType => "doc.render";
}

/// <summary>
/// Issues documents: a new number (<c>PREFIX-YYYY-000001</c>) and verification code, or — for types with one document per
/// holder — a new version of the holder's active document. The PDF is printed afterwards by the worker.
/// </summary>
internal sealed class DocumentIssuer(DocumentsDbContext context, DocumentRenderer renderer, IServiceProvider services, ICurrentUser user, TimeProvider clock) : IDocumentIssuer
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<IssuedDocumentInfo> IssueAsync(DocumentIssue issue, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(issue);
        var type = await context.DocumentTypes.AsNoTracking().SingleOrDefaultAsync(t => t.Code == issue.DocumentType, cancellationToken)
            ?? throw new DocumentIssueException($"Document type '{issue.DocumentType}' does not exist.");
        if (!type.IsActive || type.TemplateId is null)
        {
            throw new DocumentIssueException($"Document type '{type.Code}' is not active or has no template.");
        }

        // The Party Registry is resolved only when issuing, so hosts that never issue (the worker) need not load it.
        var parties = services.GetService<IPartyFacts>() ?? throw new InvalidOperationException("Issuing documents needs the Party Registry.");
        var holder = await parties.GetAsync(issue.HolderPartyId, refreshProfile: false, cancellationToken)
            ?? throw new DocumentIssueException($"Party {issue.HolderPartyId} does not exist.");
        var now = clock.GetUtcNow();

        var document = type.OnePerHolder
            ? await context.Documents.Where(d => d.DocumentTypeId == type.Id && d.HolderPartyId == holder.Id && (d.Status == DocumentStatus.Active || d.Status == DocumentStatus.Suspended))
                .OrderByDescending(d => d.IssuedAt).FirstOrDefaultAsync(cancellationToken)
            : null;
        var amended = document is not null;
        if (document is null)
        {
            document = new IssuedDocument
            {
                DocumentTypeId = type.Id,
                TypeCode = type.Code,
                Number = await NextNumberAsync(type.NumberPrefix, now, cancellationToken),
                HolderPartyId = holder.Id,
                Status = DocumentStatus.Active,
                IssuedAt = now,
                ExpiresAt = Expiry(type, now),
                VerificationCode = NewCode(),
            };
            context.Documents.Add(document);
        }
        else
        {
            if (document.Status == DocumentStatus.Suspended)
            {
                throw new DocumentIssueException($"Document {document.Number} is suspended; reinstate it before amending.");
            }

            if (type.AmendResetsValidity)
            {
                document.ExpiresAt = Expiry(type, now);
                document.AlertsSent = [];
            }
        }

        document.HolderNameAr = holder.NameAr;
        document.HolderNameEn = holder.NameEn;
        document.CurrentVersion++;
        var version = new DocumentVersion
        {
            DocumentId = document.Id,
            Version = document.CurrentVersion,
            Data = issue.Data.ToJsonString(),
            Lines = issue.Lines.ToJsonString(),
            Holder = HolderSnapshot(holder).ToJsonString(),
            IssuedAt = now,
            ExpiresAt = document.ExpiresAt,
            IssuedBy = user.UserName ?? user.UserId,
            SourceType = issue.SourceType,
            SourceId = issue.SourceId,
            SourceNumber = issue.SourceNumber,
            Reason = issue.Reason,
            TemplateId = type.TemplateId,
        };
        context.Versions.Add(version);
        context.Events.Add(new DocumentEvent
        {
            DocumentId = document.Id,
            At = now,
            Type = amended ? DocumentEventTypes.Amended : DocumentEventTypes.Issued,
            Actor = version.IssuedBy,
            Version = version.Version,
            Comment = issue.Reason,
        });
        context.Enqueue(new RenderDocumentMessage(version.Id), clock);
        DocumentNotifications.Raise(context, clock, amended ? DocumentNotifications.Amended : DocumentNotifications.Issued, document, type, renderer.VerifyUrl(document.VerificationCode), issue.Reason);
        await context.SaveChangesAsync(cancellationToken);
        return new IssuedDocumentInfo(document.Id, document.Number, version.Version, amended, document.ExpiresAt);
    }

    private static DateTimeOffset? Expiry(DocumentType type, DateTimeOffset from) => type.ValidityMonths is { } months ? from.AddMonths(months) : null;

    private static JsonObject HolderSnapshot(PartyFacts holder) => new()
    {
        ["id"] = holder.Id.ToString(),
        ["kind"] = holder.Kind == PartyKind.Individual ? "INDIVIDUAL" : "ESTABLISHMENT",
        ["nameAr"] = holder.NameAr,
        ["nameEn"] = holder.NameEn,
        ["identifiers"] = JsonSerializer.SerializeToNode(holder.Identifiers),
    };

    private async Task<string> NextNumberAsync(string prefix, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var key = $"{prefix}-{now.UtcDateTime.Year.ToString(CultureInfo.InvariantCulture)}";
        var value = await context.Database
            .SqlQuery<long>($"INSERT INTO doc.number_sequence (key, value) VALUES ({key}, 1) ON CONFLICT (key) DO UPDATE SET value = doc.number_sequence.value + 1 RETURNING value AS \"Value\"")
            .ToListAsync(cancellationToken);
        return $"{key}-{value[0].ToString("D6", CultureInfo.InvariantCulture)}";
    }

    /// <summary>20 characters from an unambiguous alphabet (about 100 bits).</summary>
    private static string NewCode() => RandomNumberGenerator.GetString(CodeAlphabet, 20);
}
