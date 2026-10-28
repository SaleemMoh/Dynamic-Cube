using System.Text.Json.Nodes;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Platform.Parties;

namespace DynamicCube.Modules.Services.Runtime;

public sealed record CatalogEntry(
    string Code,
    string NameAr,
    string NameEn,
    string? DescriptionAr,
    string? DescriptionEn,
    ServiceArchetype Archetype,
    string? CategoryCode,
    string? Icon,
    IReadOnlyList<PartyKind> PartyKinds,
    int VersionNumber,
    bool IsEligible,
    IReadOnlyList<EligibilityFailure> Reasons);

/// <summary>A service as the applicant starts it: the current version, eligibility and the fee of each level.</summary>
public sealed record ServiceView(CatalogEntry Service, ResolvedVersion Version, IReadOnlyDictionary<string, IReadOnlyList<FeeLine>> FeesByLevel);

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
    DateTimeOffset? DraftExpiresAt);

public sealed record RequestView(
    RequestSummary Request,
    int VersionNumber,
    string? LevelCode,
    string? CurrentStep,
    JsonObject Data,
    IReadOnlyList<FeeLine> Fees,
    ResolvedVersion Version,
    IReadOnlyList<FieldError> StepErrors);

public sealed record TimelineEntry(DateTimeOffset At, string UserId, string Type, StatusCategory? FromStatus, StatusCategory? ToStatus, string? Comment);

public sealed record RequestDetail(RequestView View, Guid SubmitterPartyId, string SubmitterName, Guid BeneficiaryPartyId, IReadOnlyList<TimelineEntry> Timeline);

public sealed record CreateRequest(string ServiceCode);

public sealed record SaveRequest(JsonObject? Data, string? LevelCode, string? CurrentStep);

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
