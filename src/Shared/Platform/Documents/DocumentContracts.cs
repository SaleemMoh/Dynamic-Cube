using System.Text.Json.Nodes;

namespace DynamicCube.Platform.Documents;

/// <summary>
/// A document to issue for a holder: its data and line items (e.g. one line per sector with its grade) and what it was
/// issued for. A type that allows one document per holder amends the holder's active document instead (same number,
/// new version).
/// </summary>
public sealed record DocumentIssue(
    string DocumentType,
    Guid HolderPartyId,
    JsonObject Data,
    JsonArray Lines,
    string SourceType,
    Guid SourceId,
    string? SourceNumber,
    string? Reason);

/// <param name="Amended">An existing document got a new version rather than a new document being issued.</param>
public sealed record IssuedDocumentInfo(Guid Id, string Number, int Version, bool Amended, DateTimeOffset? ExpiresAt);

/// <summary>Issues and amends documents (implemented by the Documents module); runs inside the caller's unit of work.</summary>
public interface IDocumentIssuer
{
    Task<IssuedDocumentInfo> IssueAsync(DocumentIssue issue, CancellationToken cancellationToken);
}

/// <summary>The issue cannot be made as asked (unknown or inactive type, a suspended document to amend…).</summary>
public sealed class DocumentIssueException(string message) : Exception(message);
