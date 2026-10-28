using System.Text.Json.Nodes;

namespace DynamicCube.Modules.Services.Definitions;

/// <summary>
/// What a service version asks and charges: service levels, wizard steps made of sections that use question-bank
/// questions by code, eligibility and validation rules (JSONLogic) and fee items. Authored in a change set; immutable
/// once published.
/// </summary>
public sealed class VersionDefinition
{
    public List<ServiceLevelDefinition> Levels { get; set; } = [];

    public List<StepDefinition> Steps { get; set; } = [];

    /// <summary>Rules over the applicant's facts; each must be truthy for the applicant to start or submit.</summary>
    public List<RuleDefinition> Eligibility { get; set; } = [];

    /// <summary>Cross-field rules over the answers; each must be truthy for the request to be submitted.</summary>
    public List<RuleDefinition> Validations { get; set; } = [];

    public List<FeeDefinition> Fees { get; set; } = [];

    /// <summary>The service card of the portal: steps, terms of use, required documents, audience, channels and contacts.</summary>
    public ServiceCardDefinition Card { get; set; } = new();

    /// <summary>
    /// Read only, from versions published before 1f: the workflow they named. Requests are now processed by the service's
    /// own workflow, so new versions leave it out.
    /// </summary>
    public string? Workflow { get; set; }
}

/// <summary>A tier such as Regular / Premium / Diamond with its own duration and fees.</summary>
public sealed class ServiceLevelDefinition
{
    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    /// <summary>Target processing time in working days.</summary>
    public int? SlaWorkingDays { get; set; }
}

public enum StepType
{
    /// <summary>Sections of questions.</summary>
    Form,

    /// <summary>Choose the service level (shows duration and fee per level).</summary>
    Level,

    /// <summary>Fee summary for the chosen level and answers.</summary>
    Fees,

    /// <summary>Read-only summary of the answers.</summary>
    Review,

    /// <summary>A pledge the applicant must accept before submitting.</summary>
    Declaration,
}

public sealed class StepDefinition
{
    public string Code { get; set; } = string.Empty;

    public StepType Type { get; set; }

    public string TitleAr { get; set; } = string.Empty;

    public string TitleEn { get; set; } = string.Empty;

    public List<SectionDefinition> Sections { get; set; } = [];

    /// <summary>Declaration text.</summary>
    public string? TextAr { get; set; }

    public string? TextEn { get; set; }
}

/// <summary>
/// A group of questions; shown when <see cref="VisibleWhen"/> (over the answers) is truthy, e.g. only for the chosen
/// sectors. A question used by several visible sections is asked once.
/// </summary>
public sealed class SectionDefinition
{
    public string Code { get; set; } = string.Empty;

    public string TitleAr { get; set; } = string.Empty;

    public string TitleEn { get; set; } = string.Empty;

    public JsonNode? VisibleWhen { get; set; }

    public List<QuestionUse> Questions { get; set; } = [];
}

/// <summary>A question-bank question placed in a section, with the rules of this use.</summary>
public sealed class QuestionUse
{
    public string Code { get; set; } = string.Empty;

    public bool Required { get; set; }

    public JsonNode? RequiredWhen { get; set; }

    public JsonNode? VisibleWhen { get; set; }

    /// <summary>Shown but not editable (typically prefilled from the party's data).</summary>
    public bool Readonly { get; set; }
}

/// <summary>A JSONLogic rule that must be truthy, with the bilingual message shown when it is not.</summary>
public sealed class RuleDefinition
{
    public string Code { get; set; } = string.Empty;

    public JsonNode? Rule { get; set; }

    public string MessageAr { get; set; } = string.Empty;

    public string MessageEn { get; set; } = string.Empty;

    /// <summary>For validations: the step whose answers the rule checks (where the message is shown).</summary>
    public string? Step { get; set; }
}

/// <summary>
/// A fee item: a fixed amount, a JSONLogic formula, or a bracket table looked up by a value (e.g. annual revenue).
/// Applies to the listed levels (all when empty) and when <see cref="When"/> is truthy.
/// </summary>
public sealed class FeeDefinition
{
    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public List<string> Levels { get; set; } = [];

    public JsonNode? When { get; set; }

    public decimal? Fixed { get; set; }

    public JsonNode? Formula { get; set; }

    public BracketDefinition? Bracket { get; set; }

    /// <summary>Value-added tax rate (0.15 in Saudi Arabia).</summary>
    public decimal VatRate { get; set; } = 0.15m;

    /// <summary>Whether the amount already includes VAT.</summary>
    public bool VatIncluded { get; set; } = true;
}

public sealed class BracketDefinition
{
    /// <summary>JSONLogic expression giving the value looked up, e.g. <c>{"var": "party.profile.ANNUAL_REVENUE"}</c>.</summary>
    public JsonNode? Of { get; set; }

    /// <summary>Rows with <c>from</c> inclusive and <c>to</c> exclusive (open-ended when empty).</summary>
    public List<BracketRow> Rows { get; set; } = [];
}

public sealed class BracketRow
{
    public decimal From { get; set; }

    public decimal? To { get; set; }

    public decimal Amount { get; set; }
}

/// <summary>Where a service can be used, shown on its card.</summary>
public enum ServiceChannel
{
    Portal,
    MobileApp,
    Branch,
    CallCenter,
}

/// <summary>
/// What the portal's service card says about the service before it is started. Duration comes from the service levels and
/// cost from the fees, so they are not repeated here.
/// </summary>
public sealed class ServiceCardDefinition
{
    /// <summary>Who the service is for, e.g. <em>City service providers</em> (the party kinds are shown when empty).</summary>
    public string? AudienceAr { get; set; }

    public string? AudienceEn { get; set; }

    /// <summary>The numbered steps the applicant goes through.</summary>
    public List<CardText> Steps { get; set; } = [];

    /// <summary>Terms of use, one paragraph per line.</summary>
    public string? TermsAr { get; set; }

    public string? TermsEn { get; set; }

    public List<RequiredDocument> RequiredDocuments { get; set; } = [];

    public List<ServiceChannel> Channels { get; set; } = [];

    public string? FaqUrl { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }
}

public sealed class CardText
{
    public string TextAr { get; set; } = string.Empty;

    public string TextEn { get; set; } = string.Empty;
}

public sealed class RequiredDocument
{
    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public string? NoteAr { get; set; }

    public string? NoteEn { get; set; }
}
