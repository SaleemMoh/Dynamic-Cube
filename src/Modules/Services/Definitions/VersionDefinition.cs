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

    /// <summary>
    /// For services with a document type: which of the applicant's documents the service accepts (status) and when (a window
    /// of days around the issue or expiry date). Without it any active document is accepted at any time.
    /// </summary>
    public AvailabilityDefinition? Availability { get; set; }

    /// <summary>
    /// What the applicant must already have before starting or submitting (3f): a request of another service in some
    /// statuses (e.g. an approved classification), or a document of a type in some statuses — each with its own message and,
    /// optionally, the service to start to meet it.
    /// </summary>
    public List<PrerequisiteDefinition> Prerequisites { get; set; } = [];

    /// <summary>
    /// Services requested together with this one (3f): when the request is submitted, a request of each (the required ones,
    /// and the optional ones the applicant ticked) is made for the same applicant with the answers both share, linked to it.
    /// </summary>
    public List<BundledServiceDefinition> Bundle { get; set; } = [];

    /// <summary>The service card of the portal: steps, terms of use, required documents, audience, channels and contacts.</summary>
    public ServiceCardDefinition Card { get; set; } = new();

    /// <summary>
    /// The panel beside the applicant's form (ADR-0040): an assistant, the readiness check, the request summary and the live
    /// result. Each part is optional; none is shown when it is left out.
    /// </summary>
    public PanelDefinition? Panel { get; set; }

    /// <summary>
    /// The applicant confirms the request with Nafath before it is submitted (ADR-0043): a request in their Nafath app,
    /// answered by picking the number shown, whose signed answer must name them.
    /// </summary>
    public bool ConfirmWithNafath { get; set; }

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

    /// <summary>
    /// Calls an Integration Manager operation when the applicant moves on (e.g. verifying the commercial registration) and
    /// puts its outputs into answers; a required one must succeed to continue.
    /// </summary>
    Integration,
}

/// <summary>How a form step shows its sections.</summary>
public enum SectionDisplay
{
    /// <summary>One below the other.</summary>
    Stacked,

    /// <summary>One tab per section.</summary>
    Tabs,

    /// <summary>Collapsible panels, the first one open.</summary>
    Accordion,
}

/// <summary>How much of a section's row a question takes.</summary>
public enum FieldWidth
{
    Full,
    Half,
    Third,
}

public sealed class StepDefinition
{
    public string Code { get; set; } = string.Empty;

    public StepType Type { get; set; }

    public string TitleAr { get; set; } = string.Empty;

    public string TitleEn { get; set; } = string.Empty;

    public List<SectionDefinition> Sections { get; set; } = [];

    /// <summary>Form steps: how their sections are laid out (default: one below the other).</summary>
    public SectionDisplay? Display { get; set; }

    /// <summary>Declaration text, or what an integration step does (shown to the applicant).</summary>
    public string? TextAr { get; set; }

    public string? TextEn { get; set; }

    /// <summary>Integration steps: the operation called and where its outputs go.</summary>
    public IntegrationStepDefinition? Integration { get; set; }
}

/// <summary>The call of an integration step: operation, inputs from the request, outputs into answers.</summary>
public sealed class IntegrationStepDefinition
{
    public string Operation { get; set; } = string.Empty;

    public List<Domain.IntegrationInput> Inputs { get; set; } = [];

    public List<IntegrationOutput> Outputs { get; set; } = [];

    /// <summary>The applicant cannot continue (or submit) until the call succeeds.</summary>
    public bool Required { get; set; } = true;

    /// <summary>Show the applicant a result card: whether the check passed, the provider's message and <see cref="Display"/>.</summary>
    public bool ShowResult { get; set; } = true;

    /// <summary>Outputs of the result shown on the card, each with its label (whether or not it fills an answer).</summary>
    public List<IntegrationDisplay> Display { get; set; } = [];
}

/// <summary>An output of an integration step shown to the applicant on its result card.</summary>
public sealed class IntegrationDisplay
{
    /// <summary>An output of the operation (its configured name) or a path in its result.</summary>
    public string Output { get; set; } = string.Empty;

    public string LabelAr { get; set; } = string.Empty;

    public string LabelEn { get; set; } = string.Empty;
}

/// <summary>An output of an integration step and the question whose answer it becomes.</summary>
public sealed class IntegrationOutput
{
    /// <summary>An output of the operation (its configured name) or a path in its result.</summary>
    public string Output { get; set; } = string.Empty;

    public string Question { get; set; } = string.Empty;
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

    /// <summary>Columns of questions, 1 to 3 (default: as many as fit; phones always show one).</summary>
    public int? Columns { get; set; }

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

    /// <summary>Width in a section with columns (default one column; FULL also spans a section without columns).</summary>
    public FieldWidth? Width { get; set; }
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

/// <summary>A date of the document the availability window is counted from.</summary>
public enum DocumentDate
{
    Issued,
    Expires,
}

/// <summary>What a prerequisite asks for: a request of a service, or a document of a type.</summary>
public enum PrerequisiteKind
{
    Request,
    Document,
}

/// <summary>
/// One condition the applicant (the request's beneficiary) must meet: a request of <see cref="Service"/> in one of
/// <see cref="Statuses"/> (default APPROVED), made or closed in the last <see cref="WithinDays"/> days; or a document of
/// <see cref="DocumentType"/> in one of <see cref="Statuses"/> (default ACTIVE), valid at least <see cref="ValidForDays"/>
/// more days. When it is not met the applicant sees the message and, when there is one, a link to start the service that
/// meets it.
/// </summary>
public sealed class PrerequisiteDefinition
{
    public string Code { get; set; } = string.Empty;

    public PrerequisiteKind Kind { get; set; }

    /// <summary>Requests: the service whose request is needed.</summary>
    public string? Service { get; set; }

    /// <summary>Documents: the type of document needed.</summary>
    public string? DocumentType { get; set; }

    /// <summary>Request statuses (APPROVED, SUBMITTED, IN_PROGRESS…) or document statuses (ACTIVE, EXPIRED, SUSPENDED).</summary>
    public List<string> Statuses { get; set; } = [];

    /// <summary>Requests: made (or closed) no more than this many days ago.</summary>
    public int? WithinDays { get; set; }

    /// <summary>Documents: still valid at least this many days from today.</summary>
    public int? ValidForDays { get; set; }

    /// <summary>The service the applicant can start to meet it (requests: the required service unless another is given).</summary>
    public string? StartService { get; set; }

    public string MessageAr { get; set; } = string.Empty;

    public string MessageEn { get; set; } = string.Empty;
}

/// <summary>A service requested together with this one, required or offered to the applicant to tick.</summary>
public sealed class BundledServiceDefinition
{
    public string Service { get; set; } = string.Empty;

    /// <summary>Always requested; otherwise the applicant chooses it when submitting.</summary>
    public bool Required { get; set; }

    /// <summary>What the applicant reads beside it (the service's name when empty).</summary>
    public string? NoteAr { get; set; }

    public string? NoteEn { get; set; }
}

/// <summary>A number of days before (negative) or after a date of the document.</summary>
public sealed class DateBound
{
    public DocumentDate Of { get; set; }

    public int Days { get; set; }
}

/// <summary>
/// When a follow-up service can be used on a document, e.g. a renewal in the 90 days before expiry (<c>from</c> EXPIRES −90,
/// <c>to</c> EXPIRES 0) or an extension from 15 days before until 90 days after expiry, also for expired documents.
/// </summary>
public sealed class AvailabilityDefinition
{
    /// <summary>Document statuses accepted: ACTIVE, EXPIRED, SUSPENDED (default ACTIVE only).</summary>
    public List<string> Statuses { get; set; } = [];

    /// <summary>First day the service is open (open from the start when empty).</summary>
    public DateBound? From { get; set; }

    /// <summary>Last day the service is open (open for good when empty).</summary>
    public DateBound? To { get; set; }
}

/// <summary>The parts of the panel beside the applicant's form (ADR-0040), each switched on or off.</summary>
public sealed class PanelDefinition
{
    public AssistantPanelDefinition Assistant { get; set; } = new();

    /// <summary>The readiness check: every requirement of the form (questions, files, required integrations, the level) and the share met.</summary>
    public ReadinessPanelDefinition Readiness { get; set; } = new();

    public SummaryPanelDefinition Summary { get; set; } = new();

    public ResultPanelDefinition Result { get; set; } = new();
}

/// <summary>
/// The assistant: configured lines (a greeting, notes with the rule behind them, questions with their answers), shown on
/// their step when their condition holds, plus the notes the form itself works out (missing items, uploads, problems).
/// </summary>
public sealed class AssistantPanelDefinition
{
    public bool Enabled { get; set; }

    /// <summary>e.g. «وكيل التصنيف».</summary>
    public string? TitleAr { get; set; }

    public string? TitleEn { get; set; }

    public string? SubtitleAr { get; set; }

    public string? SubtitleEn { get; set; }

    /// <summary>Add the notes worked out from the form: what the step still lacks, its uploads and the problems found.</summary>
    public bool AutoNotes { get; set; } = true;

    public List<AssistantLineDefinition> Lines { get; set; } = [];
}

public enum AssistantLineKind
{
    /// <summary>The greeting of the step.</summary>
    Message,

    /// <summary>A requirement that always applies.</summary>
    Mandatory,

    /// <summary>A requirement that applies depending on an answer.</summary>
    Conditional,

    /// <summary>Something that weighs on the result.</summary>
    Impact,

    /// <summary>A hint.</summary>
    Advice,

    /// <summary>A question the applicant can ask, with its answer.</summary>
    Question,
}

/// <summary>A line of the assistant. Texts may use <c>{missing}</c>, <c>{readiness}</c> and <c>{service}</c>.</summary>
public sealed class AssistantLineDefinition
{
    /// <summary>The step it is shown on; empty: every step.</summary>
    public string? Step { get; set; }

    public AssistantLineKind Kind { get; set; } = AssistantLineKind.Advice;

    /// <summary>The message, note or answer.</summary>
    public string TextAr { get; set; } = string.Empty;

    public string TextEn { get; set; } = string.Empty;

    /// <summary>A note's rule, shown under «لماذا؟» (why?).</summary>
    public string? WhyAr { get; set; }

    public string? WhyEn { get; set; }

    /// <summary>The question of a question line.</summary>
    public string? QuestionAr { get; set; }

    public string? QuestionEn { get; set; }

    /// <summary>JSONLogic over <c>data</c> (answers), <c>level</c>, <c>step</c>, <c>readiness</c>, <c>missing</c> and <c>result</c> (the live result); empty: always.</summary>
    public JsonNode? When { get; set; }
}

public sealed class ReadinessPanelDefinition
{
    public bool Enabled { get; set; }
}

/// <summary>The request summary: the service, version, level, answers, files, fees and the live result's headline.</summary>
public sealed class SummaryPanelDefinition
{
    public bool Enabled { get; set; }

    /// <summary>Questions whose answers the summary also shows.</summary>
    public List<string> Questions { get; set; } = [];
}

/// <summary>
/// The live result: the decision step of the service's workflow evaluated on the answers as they are (not recorded, not the
/// official decision), shown as a headline, a list of values and, for rule sets with items (e.g. one per sector), a card per
/// item with its values on the scale.
/// </summary>
public sealed class ResultPanelDefinition
{
    public bool Enabled { get; set; }

    public string? TitleAr { get; set; }

    public string? TitleEn { get; set; }

    /// <summary>The code of a decision step of the service's workflow: its rule set and input are used.</summary>
    public string? Step { get; set; }

    /// <summary>The main result, e.g. the best grade.</summary>
    public ResultValueDefinition? Headline { get; set; }

    /// <summary>The values a result may take, best first (e.g. grades 1 to 5) with their names; drawn as a ladder.</summary>
    public List<ResultScaleValue> Scale { get; set; } = [];

    /// <summary>What an empty result is called, e.g. «غير مصنف».</summary>
    public string? UnknownAr { get; set; }

    public string? UnknownEn { get; set; }

    public List<ResultValueDefinition> Values { get; set; } = [];

    public ResultItemsDefinition? Items { get; set; }
}

/// <summary>An output of the rule set shown with a label; <see cref="OutputEn"/> names the output holding the English text, when different.</summary>
public sealed class ResultValueDefinition
{
    public string Output { get; set; } = string.Empty;

    public string? OutputEn { get; set; }

    public string LabelAr { get; set; } = string.Empty;

    public string LabelEn { get; set; } = string.Empty;

    /// <summary>The value is one of the scale's (shown with its name and place).</summary>
    public bool OnScale { get; set; }
}

public sealed class ResultScaleValue
{
    public string Value { get; set; } = string.Empty;

    public string LabelAr { get; set; } = string.Empty;

    public string LabelEn { get; set; } = string.Empty;
}

/// <summary>A list output of the rule set (e.g. <c>items</c>, one per sector): a card per element with its title, result and columns.</summary>
public sealed class ResultItemsDefinition
{
    public string Output { get; set; } = "items";

    public string TitleOutput { get; set; } = string.Empty;

    public string? TitleOutputEn { get; set; }

    /// <summary>The element's result on the scale, e.g. its grade.</summary>
    public string? ResultOutput { get; set; }

    public List<ResultValueDefinition> Columns { get; set; } = [];
}
