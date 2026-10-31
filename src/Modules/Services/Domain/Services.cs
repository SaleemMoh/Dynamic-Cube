using DynamicCube.Platform.Parties;

namespace DynamicCube.Modules.Services.Domain;

/// <summary>
/// A kind of service (issuance, amendment, visit request, attestation…) declared once with what a new service of that
/// kind starts from: its default workflow (given to the service when it is first published) and its service template
/// (the first version the Service Builder opens). Entity code: e.g. <c>ISSUANCE</c>.
/// </summary>
public sealed class Archetype : ConfigEntity
{
    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    public string? Icon { get; set; }

    public int DisplayOrder { get; set; }

    /// <summary>The version definition a new service starts from (JSON); none: the builder's blank start.</summary>
    public string? Template { get; set; }

    /// <summary>The workflow definition a new service's workflow starts from (JSON); none: completed without review.</summary>
    public string? Workflow { get; set; }
}

/// <summary>What happens to drafts when a newer version is published.</summary>
public enum NewVersionPolicy
{
    /// <summary>Drafts continue on the version they started on.</summary>
    Continue,

    /// <summary>Drafts move to the new version, keeping the answers to questions it still asks.</summary>
    Restart,
}

/// <summary>A service applicants can request; what it asks and charges lives in its published versions.</summary>
public sealed class Service : ConfigEntity
{
    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    /// <summary>The service's archetype (<see cref="Domain.Archetype"/> code).</summary>
    public string Archetype { get; set; } = "ISSUANCE";

    /// <summary>The category grouping the service in the catalog: a value of class <c>SVC_CATEGORY</c>.</summary>
    public string? CategoryCode { get; set; }

    public string? Icon { get; set; }

    public int DisplayOrder { get; set; }

    /// <summary>Who may apply: individuals, establishments or both.</summary>
    public List<PartyKind> PartyKinds { get; set; } = [];

    /// <summary>Shown in the portal catalog (unlisted services are reached only through links).</summary>
    public bool IsListed { get; set; } = true;

    public bool DraftsAllowed { get; set; } = true;

    public int DraftExpiryDays { get; set; } = 30;

    public NewVersionPolicy OnNewVersion { get; set; }

    /// <summary>Request numbers: <c>{prefix}-{year}-{sequence}</c>.</summary>
    public string NumberPrefix { get; set; } = string.Empty;

    /// <summary>
    /// The document type a follow-up service acts on (renewal, extension…): each request is made for one of the applicant's
    /// documents of this type, within the availability of the version.
    /// </summary>
    public string? DocumentType { get; set; }

    /// <summary>
    /// The company that handles the service's requests (ADR-0036 step 6); null for the installation's default company (its
    /// first company).
    /// </summary>
    public string? CompanyCode { get; set; }

    /// <summary>
    /// The question whose answer places a request in a branch of that company: the answer is matched against the branches'
    /// codes, regions and cities. None (or no match): the company's default branch.
    /// </summary>
    public string? BranchQuestion { get; set; }
}

public enum QuestionType
{
    Text,
    Textarea,
    Number,
    Date,
    Select,
    Checkbox,

    /// <summary>Files uploaded with the request (the answer is the list of file ids).</summary>
    File,
}

public sealed class QuestionOption
{
    public string Value { get; set; } = string.Empty;

    public string LabelAr { get; set; } = string.Empty;

    public string LabelEn { get; set; } = string.Empty;
}

public sealed class QuestionValidation
{
    public decimal? Min { get; set; }

    public decimal? Max { get; set; }

    public int? MaxLength { get; set; }

    public string? Pattern { get; set; }

    /// <summary>File questions: accepted extensions without the dot (e.g. pdf, jpg); empty accepts the usual documents and images.</summary>
    public List<string> Extensions { get; set; } = [];

    /// <summary>File questions: the largest file in megabytes (default 10).</summary>
    public int? MaxSizeMb { get; set; }

    /// <summary>File questions with several files: how many at most (default 5).</summary>
    public int? MaxFiles { get; set; }
}

/// <summary>Where a question's first answer comes from.</summary>
public enum QuestionFill
{
    /// <summary>The applicant answers.</summary>
    None,

    /// <summary>A field of the applicant's (beneficiary's) data: <see cref="Question.Prefill"/>.</summary>
    Party,

    /// <summary>An Integration Manager operation: <see cref="Question.Integration"/>.</summary>
    Integration,
}

/// <summary>A parameter passed to an integration operation and where its value comes from in the request.</summary>
public sealed class IntegrationInput
{
    public string Parameter { get; set; } = string.Empty;

    /// <summary>A path in the request's facts, e.g. <c>party.identifiers.CR_NUMBER</c> or <c>data.REGION</c>.</summary>
    public string From { get; set; } = string.Empty;
}

/// <summary>A call of an Integration Manager operation that gives a question its answer.</summary>
public sealed class IntegrationFill
{
    public string Operation { get; set; } = string.Empty;

    public List<IntegrationInput> Inputs { get; set; } = [];

    /// <summary>An output of the operation (its configured name) or a path in its result, e.g. <c>capital</c>.</summary>
    public string Output { get; set; } = string.Empty;
}

/// <summary>
/// A question of the bank, used by code in service versions so a question shared by several sections is answered
/// once. The name is the label shown to the applicant. A question with a <see cref="ServiceCode"/> belongs to that
/// service alone (a service-level question); the others are shared by every service.
/// </summary>
public sealed class Question : ConfigEntity
{
    /// <summary>The service the question belongs to (only its versions use it); none: a shared bank question.</summary>
    public string? ServiceCode { get; set; }

    public QuestionType Type { get; set; }

    public string? HintAr { get; set; }

    public string? HintEn { get; set; }

    /// <summary>Apps Control LOV supplying the options of a select question.</summary>
    public string? Lov { get; set; }

    /// <summary>Question whose answer is the LOV parent (cascading lists).</summary>
    public string? LovParentQuestion { get; set; }

    public List<QuestionOption> Options { get; set; } = [];

    public bool Multiple { get; set; }

    public QuestionValidation Validation { get; set; } = new();

    /// <summary>Where the first answer comes from: the applicant, the party's data or an integration.</summary>
    public QuestionFill Fill { get; set; }

    /// <summary>Path in the applicant's facts used as the initial answer, e.g. <c>party.profile.CR_CAPITAL</c>.</summary>
    public string? Prefill { get; set; }

    /// <summary>The integration call that answers the question (with <see cref="QuestionFill.Integration"/>).</summary>
    public IntegrationFill? Integration { get; set; }

    /// <summary>
    /// Whether the applicant may type or change a filled answer; when not, the answer is always the filled value (shown
    /// read-only and reset on the server).
    /// </summary>
    public bool AllowManualEntry { get; set; } = true;
}

/// <summary>
/// One numbered version of a service (entity code <c>SERVICE/N</c>). Published versions never change; requests stay on
/// the version they started on. <see cref="Resolved"/> is the definition with snapshots of its questions.
/// </summary>
public sealed class ServiceVersion : ConfigEntity
{
    public Guid ServiceId { get; set; }

    public Service? Service { get; set; }

    public int Number { get; set; }

    /// <summary>The authored <see cref="Definitions.VersionDefinition"/> (JSON).</summary>
    public string Definition { get; set; } = "{}";

    /// <summary>The compiled version served to the wizard (JSON); set when the change set is published.</summary>
    public string? Resolved { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Retired versions take no new requests; running ones finish on them.</summary>
    public bool IsRetired { get; set; }
}
