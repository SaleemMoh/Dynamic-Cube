using DynamicCube.Platform.Parties;

namespace DynamicCube.Modules.Services.Domain;

public enum ServiceArchetype
{
    Issuance,
    Amendment,
    VisitRequest,
    Attestation,
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

    public ServiceArchetype Archetype { get; set; }

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

/// <summary>
/// A question of the bank, used by code in service versions so a question shared by several sections is answered
/// once. The name is the label shown to the applicant.
/// </summary>
public sealed class Question : ConfigEntity
{
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

    /// <summary>Path in the applicant's facts used as the initial answer, e.g. <c>party.profile.CR_CAPITAL</c>.</summary>
    public string? Prefill { get; set; }
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
