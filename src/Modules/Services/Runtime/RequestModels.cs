using System.Text.Json.Nodes;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Platform.Integration;
using DynamicCube.Platform.Parties;
using DynamicCube.Platform.Payments;
using DynamicCube.Platform.Visits;
using DynamicCube.Platform.Workflow;

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
    IReadOnlyList<RequestFileInfo>? Files = null)
{
    /// <summary>The outcome of each integration step run so far, by step (for its result card).</summary>
    public IReadOnlyDictionary<string, IntegrationStepRun> Integrations { get; init; } = new Dictionary<string, IntegrationStepRun>();

    /// <summary>Answers that changed when the draft's integrations were read again on opening it (from → to).</summary>
    public IReadOnlyList<AnswerChange> Changes { get; init; } = [];

    /// <summary>The requests linked to this one (3f): the one it came from, and those bundled with it or made by its steps.</summary>
    public IReadOnlyList<LinkedRequest> Links { get; init; } = [];

    /// <summary>A draft's bundled services: required or offered, each open to the applicant now or not (with why).</summary>
    public IReadOnlyList<BundleOption> Bundle { get; init; } = [];

    /// <summary>The items decided one by one (e.g. each sector approved on its own), with the reasons.</summary>
    public IReadOnlyList<SubjectItem> ItemResults { get; init; } = [];

    /// <summary>Staff only: every integration call made for the request (masked), newest first.</summary>
    public IReadOnlyList<IntegrationEvidence> Evidence { get; init; } = [];
}

/// <summary>
/// A request linked to another (3f). <paramref name="Relation"/>: <c>PARENT</c> (the request it came from), <c>BUNDLED</c>
/// (requested with it) or <c>SPAWNED</c> (made by one of its steps, <paramref name="Step"/>).
/// </summary>
public sealed record LinkedRequest(Guid Id, string? Number, string ServiceCode, string ServiceNameAr, string ServiceNameEn, StatusCategory Status, string DisplayStatus, string Relation, string? Step);

/// <summary>A service bundled with a draft's: whether it is always requested, and whether the applicant may have it now.</summary>
public sealed record BundleOption(string Service, string NameAr, string NameEn, bool Required, string? NoteAr, string? NoteEn, bool IsEligible, IReadOnlyList<EligibilityFailure> Reasons);

public sealed record TimelineEntry(DateTimeOffset At, string UserId, string Type, StatusCategory? FromStatus, StatusCategory? ToStatus, string? Comment);

public sealed record RequestDetail(RequestView View, Guid SubmitterPartyId, string SubmitterName, Guid BeneficiaryPartyId, IReadOnlyList<TimelineEntry> Timeline);

/// <param name="DocumentId">For a follow-up service: the applicant's document the request is for.</param>
public sealed record CreateRequest(string ServiceCode, Guid? DocumentId = null);

/// <param name="ValidateStep">The step being left: its problems are returned and the request stays on it while there are any.</param>
public sealed record SaveRequest(JsonObject? Data, string? LevelCode, string? CurrentStep, string? ValidateStep = null);

/// <param name="NafathCheckId">The Nafath verification the applicant confirmed the request with, when the version asks for one.</param>
/// <param name="Bundle">The optional bundled services the applicant ticked (the required ones are always requested).</param>
public sealed record SubmitRequest(bool DeclarationAccepted, Guid? NafathCheckId = null, IReadOnlyList<string>? Bundle = null);

/// <summary>A Nafath request sent to the applicant's app: the number to pick and until when (ADR-0043).</summary>
public sealed record NafathChallengeView(Guid CheckId, string Random, DateTimeOffset ExpiresAt);

/// <summary>Where the applicant's Nafath confirmation stands: <c>WAITING</c>, <c>VERIFIED</c>, <c>REJECTED</c> or <c>EXPIRED</c>.</summary>
public sealed record NafathCheckView(Guid CheckId, string Status, string Random, DateTimeOffset ExpiresAt);

public sealed class RequestNotFoundException(string message) : Exception(message);

public sealed class RequestConflictException(string message) : Exception(message);

/// <summary>The answers and level as the applicant has them on screen (not saved), for the live result; none: the saved ones.</summary>
public sealed record LiveResultRequest(System.Text.Json.Nodes.JsonObject? Data, string? LevelCode);

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
