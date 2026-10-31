using System.Text.Json.Nodes;

namespace DynamicCube.Platform.Documents;

/// <summary>
/// A document to issue for a holder: its data and line items (e.g. one line per sector with its grade) and what it was
/// issued for. A type that allows one document per holder amends the holder's active document instead (same number,
/// new version).
/// </summary>
/// <param name="Document">Amend this document (of the type and holder given) instead of issuing: follow-up services such as a
/// renewal act on the document they were requested for. Empty <paramref name="Data"/> keeps the document's data (given
/// fields replace theirs) and null <paramref name="Lines"/> keeps its lines.</param>
/// <param name="Validity">How the expiry date changes; null follows the document type.</param>
/// <param name="CompanyCode">The company the document belongs to (that of the request it is issued for, ADR-0036 step 6).</param>
/// <param name="BranchCode">The branch of that company.</param>
public sealed record DocumentIssue(
    string DocumentType,
    Guid HolderPartyId,
    JsonObject Data,
    JsonArray? Lines,
    string SourceType,
    Guid SourceId,
    string? SourceNumber,
    string? Reason,
    Guid? Document = null,
    ValidityChange? Validity = null,
    string? CompanyCode = null,
    string? BranchCode = null);

public enum ValidityMode
{
    /// <summary>The expiry date stays.</summary>
    Keep,

    /// <summary>A new validity period from today.</summary>
    Reset,

    /// <summary>A new validity period from the current expiry, or from today when that has passed (renewing early loses no days).</summary>
    Renew,

    /// <summary>The current expiry moves by <see cref="ValidityChange.Months"/>.</summary>
    Extend,
}

/// <param name="Months">Length of the new period (Reset, Renew; default the type's validity) or of the extension (Extend).</param>
public sealed record ValidityChange(ValidityMode Mode, int? Months = null);

/// <param name="Amended">An existing document got a new version rather than a new document being issued.</param>
public sealed record IssuedDocumentInfo(Guid Id, string Number, int Version, bool Amended, DateTimeOffset? ExpiresAt);

/// <summary>Issues and amends documents (implemented by the Documents module); runs inside the caller's unit of work.</summary>
public interface IDocumentIssuer
{
    Task<IssuedDocumentInfo> IssueAsync(DocumentIssue issue, CancellationToken cancellationToken);
}

/// <summary>What services and their rules know about an issued document (rule context <c>document</c>).</summary>
public sealed record DocumentFacts(
    Guid Id,
    string TypeCode,
    string Number,
    Guid HolderPartyId,
    string Status,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ExpiresAt,
    int Version,
    JsonObject Data,
    JsonArray Lines)
{
    public JsonObject ToJson() => new()
    {
        ["id"] = Id.ToString(),
        ["type"] = TypeCode,
        ["number"] = Number,
        ["status"] = Status,
        ["issuedOn"] = DateOnly.FromDateTime(IssuedAt.UtcDateTime).ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        ["expiresOn"] = ExpiresAt is { } expires ? DateOnly.FromDateTime(expires.UtcDateTime).ToString("O", System.Globalization.CultureInfo.InvariantCulture) : null,
        ["version"] = Version,
        ["data"] = Data.DeepClone(),
        ["lines"] = Lines.DeepClone(),
    };
}

/// <summary>Reads issued documents for other modules (implemented by the Documents module).</summary>
public interface IDocumentDirectory
{
    Task<DocumentFacts?> GetAsync(Guid documentId, CancellationToken cancellationToken);

    /// <summary>The holder's documents of a type, newest first (cancelled ones left out).</summary>
    Task<IReadOnlyList<DocumentFacts>> OfHolderAsync(Guid holderPartyId, string documentType, CancellationToken cancellationToken);

    /// <summary>Names and whether a document type exists (for configuration checks and labels).</summary>
    Task<(string NameAr, string NameEn)?> TypeAsync(string documentType, CancellationToken cancellationToken);
}

/// <summary>The issue cannot be made as asked (unknown or inactive type, a suspended document to amend…).</summary>
public sealed class DocumentIssueException(string message) : Exception(message);
