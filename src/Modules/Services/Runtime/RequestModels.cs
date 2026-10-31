using System.Text.Json.Nodes;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Platform.Parties;
using DynamicCube.Platform.Payments;
using DynamicCube.Platform.Visits;

namespace DynamicCube.Modules.Services.Runtime;

public sealed record CatalogEntry(
    string Code,
    string NameAr,
    string NameEn,
    string? DescriptionAr,
    string? DescriptionEn,
    string Archetype,
    string? CategoryCode,
    string? Icon,
    IReadOnlyList<PartyKind> PartyKinds,
    int VersionNumber,
    bool IsEligible,
    IReadOnlyList<EligibilityFailure> Reasons,
    string? DocumentType = null);

/// <summary>
/// One of the applicant's documents for a follow-up service: whether a request can be made for it now (its status and the
/// service's window), the window's dates, and the request already open for it, if any.
/// </summary>
public sealed record DocumentOption(
    Guid Id,
    string Number,
    string Status,
    DateOnly IssuedOn,
    DateOnly? ExpiresOn,
    bool IsAvailable,
    DateOnly? OpensOn,
    DateOnly? ClosesOn,
    IReadOnlyList<EligibilityFailure> Reasons,
    Guid? OpenRequestId,
    string? OpenRequestNumber);

/// <summary>
/// A service as the applicant starts it: the current version, eligibility and the fee of each level; for a follow-up
/// service, the applicant's documents it can be requested for.
/// </summary>
public sealed record ServiceView(CatalogEntry Service, ResolvedVersion Version, IReadOnlyDictionary<string, IReadOnlyList<FeeLine>> FeesByLevel, IReadOnlyList<DocumentOption> Documents);

/// <summary>A follow-up service on one of the applicant's documents (the document page lists them).</summary>
public sealed record FollowUpService(CatalogEntry Service, DocumentOption Document);

public sealed record RequestSummary(
    Guid Id,
    string? Number,
    string ServiceCode,
    string ServiceNameAr,
    string ServiceNameEn,
    StatusCategory Status,
    string DisplayStatus,
    string BeneficiaryNameAr,
    string BeneficiaryNameEn,
    decimal FeeTotal,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? DraftExpiresAt,
    string? SourceDocumentNumber = null,
    string? CompanyCode = null,
    string? BranchCode = null);

public sealed record RequestView(
    RequestSummary Request,
    int VersionNumber,
    string? LevelCode,
    string? CurrentStep,
    JsonObject Data,
    IReadOnlyList<FeeLine> Fees,
    IReadOnlyDictionary<string, IReadOnlyList<FeeLine>> FeesByLevel,
    ResolvedVersion Version,
    IReadOnlyList<FieldError> StepErrors,
    IReadOnlyList<TimelineEntry> Timeline,
    IReadOnlyList<BillInfo>? Bills = null,
    IReadOnlyList<VisitInfo>? Visits = null,
    IReadOnlyList<RequestFileInfo>? Files = null);

public sealed record TimelineEntry(DateTimeOffset At, string UserId, string Type, StatusCategory? FromStatus, StatusCategory? ToStatus, string? Comment);

public sealed record RequestDetail(RequestView View, Guid SubmitterPartyId, string SubmitterName, Guid BeneficiaryPartyId, IReadOnlyList<TimelineEntry> Timeline);

/// <param name="DocumentId">For a follow-up service: the applicant's document the request is for.</param>
public sealed record CreateRequest(string ServiceCode, Guid? DocumentId = null);

/// <param name="ValidateStep">The step being left: its problems are returned and the request stays on it while there are any.</param>
public sealed record SaveRequest(JsonObject? Data, string? LevelCode, string? CurrentStep, string? ValidateStep = null);

public sealed record SubmitRequest(bool DeclarationAccepted);

public sealed class RequestNotFoundException(string message) : Exception(message);

public sealed class RequestConflictException(string message) : Exception(message);

/// <summary>The applicant may not use the service now.</summary>
public sealed class NotEligibleException(IReadOnlyList<EligibilityFailure> reasons) : Exception("Not eligible.")
{
    public IReadOnlyList<EligibilityFailure> Reasons { get; } = reasons;
}

/// <summary>The answers cannot be submitted as they are.</summary>
public sealed class RequestValidationException(IReadOnlyList<FieldError> errors) : Exception("The request is invalid.")
{
    public IReadOnlyList<FieldError> Errors { get; } = errors;
}
