using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DynamicCube.Modules.Decisions.Runtime.Matrix;

/// <summary>
/// The configuration of a <c>MATRIX</c> rule set (ADR-0050): a table whose rows are the things classified (sectors) and
/// whose columns are the assessments (tracks such as technical, performance, governance, financial). Each cell says how
/// the row is scored on the column. Around the table: the inputs the cells read and where each comes from, sizes, grades,
/// scales, gates, adjustments and the projects that count.
/// </summary>
public sealed class MatrixDefinition
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    /// <summary>Grades from best (rank 1) to worst.</summary>
    public List<MatrixGrade> Grades { get; set; } = [];

    public List<MatrixInput> Inputs { get; set; } = [];

    public List<MatrixOptionSet> OptionSets { get; set; } = [];

    /// <summary>Optional size categories (e.g. by employees): a criterion's bands may differ per size.</summary>
    public MatrixSizes? Sizes { get; set; }

    public List<MatrixScale> Scales { get; set; } = [];

    /// <summary>The rows (e.g. sectors).</summary>
    public List<MatrixRow> Rows { get; set; } = [];

    /// <summary>
    /// The input whose value lists the rows applied for (e.g. the sectors question); each answer selects the row whose
    /// <see cref="MatrixRow.Answer"/> (or code) it equals. Without it, or without an answer, every active row is graded.
    /// </summary>
    public string? RowsInput { get; set; }

    /// <summary>The columns (e.g. tracks).</summary>
    public List<MatrixColumn> Columns { get; set; } = [];

    /// <summary>One per row and column; a missing cell is empty (the column does not apply to the row).</summary>
    public List<MatrixCell> Cells { get; set; } = [];

    public List<MatrixGate> Gates { get; set; } = [];

    public List<MatrixAdjustment> Adjustments { get; set; } = [];

    /// <summary>Which of the firm's projects count towards project totals.</summary>
    public MatrixProjects? Projects { get; set; }

    /// <summary>What a demotion below the worst grade gives: <c>stay</c> (the worst grade) or <c>notGraded</c>.</summary>
    public string BelowWorst { get; set; } = "stay";

    public static MatrixDefinition Read(JsonNode configuration) =>
        configuration.Deserialize<MatrixDefinition>(Json) ?? new MatrixDefinition();

    public JsonNode ToJson() => JsonSerializer.SerializeToNode(this, Json)!;
}

public sealed class MatrixGrade
{
    public int Rank { get; set; }

    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;
}

/// <summary>Where an input's value comes from. The engine computes <c>derived</c> and <c>projects</c>; the others are given.</summary>
public sealed class MatrixSource
{
    /// <summary><c>applicant</c>, <c>reviewer</c>, <c>integration</c>, <c>party</c>, <c>derived</c>, <c>projects</c> or <c>given</c>.</summary>
    public string Kind { get; set; } = "applicant";

    /// <summary>applicant: the question that asks for it.</summary>
    public string? Question { get; set; }

    /// <summary>reviewer: the workflow step (task) and its field.</summary>
    public string? Step { get; set; }

    public string? Field { get; set; }

    /// <summary>integration: the operation, its output and the operation's inputs (parameter → JSONLogic over the request).</summary>
    public string? Operation { get; set; }

    public string? Output { get; set; }

    public JsonObject? Parameters { get; set; }

    /// <summary>party: the path in the party's facts (e.g. <c>profile.CR_CAPITAL</c>).</summary>
    public string? Path { get; set; }

    /// <summary>derived: a JSONLogic rule over the other inputs by code.</summary>
    public JsonNode? Rule { get; set; }

    /// <summary>projects: <c>sum</c>, <c>max</c>, <c>count</c> or <c>distinct</c> of a column over the projects that count.</summary>
    public string? Function { get; set; }

    public string? Column { get; set; }

    /// <summary>projects: only the projects meeting this rule (over a project's fields; a date field <c>x</c> also gives <c>x_yearsAgo</c>).</summary>
    public JsonNode? Where { get; set; }

    /// <summary>The value may be changed by staff even when it comes from elsewhere.</summary>
    public bool AllowOverride { get; set; }
}

public sealed class MatrixInput
{
    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    /// <summary><c>number</c>, <c>text</c>, <c>option</c>, <c>boolean</c>, <c>date</c> or <c>list</c>.</summary>
    public string Type { get; set; } = "number";

    public string? Unit { get; set; }

    /// <summary>For <c>option</c> inputs: the option set of its answers.</summary>
    public string? OptionSet { get; set; }

    public decimal? Min { get; set; }

    public decimal? Max { get; set; }

    public MatrixSource Source { get; set; } = new();
}

public sealed class MatrixOptionSet
{
    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public List<MatrixOption> Items { get; set; } = [];
}

public sealed class MatrixOption
{
    public string Code { get; set; } = string.Empty;

    public string LabelAr { get; set; } = string.Empty;

    public string LabelEn { get; set; } = string.Empty;

    /// <summary>Its order (1 = first); bands can score the ordinal instead of the answer.</summary>
    public int Ordinal { get; set; }
}

public sealed class MatrixSizes
{
    /// <summary>The input sizes are taken from (e.g. total employees).</summary>
    public string Input { get; set; } = string.Empty;

    public List<MatrixSize> Categories { get; set; } = [];
}

public sealed class MatrixSize
{
    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public decimal? Min { get; set; }

    public decimal? Max { get; set; }
}

/// <summary>Points to a grade: the first step whose range holds the score (min inclusive, max exclusive).</summary>
public sealed class MatrixScale
{
    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public List<MatrixScaleStep> Steps { get; set; } = [];
}

public sealed class MatrixScaleStep
{
    public decimal? Min { get; set; }

    public decimal? Max { get; set; }

    public int Grade { get; set; }
}

public sealed class MatrixRow
{
    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public bool Active { get; set; } = true;

    /// <summary>The answer of the <see cref="MatrixDefinition.RowsInput"/> that selects this row (its code when empty).</summary>
    public string? Answer { get; set; }

    /// <summary>How the row's grade is made from its columns: <c>worst</c> (default), <c>weighted</c> or <c>formula</c>.</summary>
    public string Final { get; set; } = "worst";

    /// <summary>For <c>formula</c>: JSONLogic over the inputs and <c>columns.CODE.grade</c> / <c>columns.CODE.points</c>.</summary>
    public JsonNode? FinalRule { get; set; }

    public string? NotesAr { get; set; }

    public string? NotesEn { get; set; }

    /// <summary>Free settings kept with the row (e.g. the issuing authority).</summary>
    public JsonObject? Settings { get; set; }
}

public sealed class MatrixColumn
{
    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    /// <summary>Counted in the row's grade.</summary>
    public bool IncludeInFinal { get; set; } = true;

    public decimal? Weight { get; set; }
}

/// <summary>How a row is scored on a column.</summary>
public sealed class MatrixCell
{
    public string Row { get; set; } = string.Empty;

    public string Column { get; set; } = string.Empty;

    /// <summary>
    /// <c>points</c> (criteria give points, groups add up, the total goes through <see cref="Scale"/>), <c>worst</c> (criteria
    /// give grades, the worst counts), <c>formula</c> (a rule gives the grade), <c>same</c> (as another cell) or <c>empty</c>.
    /// </summary>
    public string Kind { get; set; } = "points";

    public string? Scale { get; set; }

    public List<MatrixGroup> Groups { get; set; } = [];

    /// <summary>For <c>same</c>: the cell copied.</summary>
    public string? SameRow { get; set; }

    public string? SameColumn { get; set; }

    /// <summary>For <c>formula</c>: JSONLogic over the inputs giving the grade (a rank), or null when not graded.</summary>
    public JsonNode? GradeRule { get; set; }

    /// <summary>For <c>formula</c>: optional rule giving the score shown with the grade.</summary>
    public JsonNode? ScoreRule { get; set; }

    /// <summary>For <c>formula</c>: optional rule giving details shown with the result (e.g. a category).</summary>
    public JsonNode? DetailsRule { get; set; }

    public string? NoteAr { get; set; }

    public string? NoteEn { get; set; }
}

public sealed class MatrixGroup
{
    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    /// <summary>The most points the group gives.</summary>
    public decimal? MaxPoints { get; set; }

    /// <summary>The group must reach its minimum, or the cell is not graded.</summary>
    public bool Mandatory { get; set; }

    /// <summary>The minimum of a mandatory group: more than this many points (default: more than none).</summary>
    public decimal MinPoints { get; set; }

    public List<MatrixCriterion> Criteria { get; set; } = [];
}

public sealed class MatrixCriterion
{
    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public string Input { get; set; } = string.Empty;

    /// <summary><c>bands</c> (ranges of a number), <c>options</c> (an answer) or <c>direct</c> (the value is the points).</summary>
    public string Scoring { get; set; } = "bands";

    /// <summary><c>points</c> or <c>grade</c>.</summary>
    public string Output { get; set; } = "points";

    /// <summary>Bands read the answer's ordinal in its option set instead of the value.</summary>
    public bool UseOrdinal { get; set; }

    public decimal? MaxPoints { get; set; }

    /// <summary>JSONLogic over the inputs; when false the criterion gives nothing (its maximum still counts).</summary>
    public JsonNode? AppliesWhen { get; set; }

    public List<MatrixBand> Bands { get; set; } = [];

    public List<MatrixOptionScore> Options { get; set; } = [];

    /// <summary>Criteria sharing a key are alternatives for a mandatory group's minimum.</summary>
    public string? AlternativeKey { get; set; }

    public string? Note { get; set; }
}

public sealed class MatrixBand
{
    /// <summary>The size category the band is for; null for every size.</summary>
    public string? Size { get; set; }

    public decimal? Min { get; set; }

    public bool MinInclusive { get; set; } = true;

    public decimal? Max { get; set; }

    public bool MaxInclusive { get; set; }

    public decimal? Points { get; set; }

    public int? Grade { get; set; }

    public string? Label { get; set; }
}

public sealed class MatrixOptionScore
{
    public string Value { get; set; } = string.Empty;

    public decimal? Points { get; set; }

    public int? Grade { get; set; }

    /// <summary>The attachment type this answer asks for (the service form shows it).</summary>
    public string? Attachment { get; set; }
}

public sealed class MatrixGate
{
    public string Code { get; set; } = string.Empty;

    /// <summary>The rows it is for; empty for every row.</summary>
    public List<string> Rows { get; set; } = [];

    /// <summary>JSONLogic over the inputs; the gate passes when true.</summary>
    public JsonNode? Rule { get; set; }

    public string MessageAr { get; set; } = string.Empty;

    public string MessageEn { get; set; } = string.Empty;

    /// <summary>A failed blocking gate makes the row not eligible; others only warn.</summary>
    public bool Blocking { get; set; } = true;
}

public sealed class MatrixAdjustment
{
    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public List<string> Rows { get; set; } = [];

    public JsonNode? Rule { get; set; }

    /// <summary>Grades to demote by when the rule holds.</summary>
    public int Demote { get; set; } = 1;
}

/// <summary>The projects that count: the input holding the firm's projects and the rules they must meet.</summary>
public sealed class MatrixProjects
{
    /// <summary>The input (a list) holding the projects.</summary>
    public string Input { get; set; } = "projects";

    /// <summary>A project counts when it matches a rule and meets its limits; no rules: every project counts.</summary>
    public List<MatrixProjectRule> Rules { get; set; } = [];

    /// <summary>Project fields: the value, the completion percentage and the date age is counted from.</summary>
    public string ValueField { get; set; } = "value";

    public string? CertifiedValueField { get; set; }

    public string? CompletionField { get; set; }

    public string? DateField { get; set; }

    /// <summary>The project fields the firm's projects carry (for the designer).</summary>
    public List<MatrixInput> Fields { get; set; } = [];

    /// <summary>Kept from imported guides; not applied by the engine.</summary>
    public decimal? SplitThreshold { get; set; }
}

public sealed class MatrixProjectRule
{
    public string Code { get; set; } = string.Empty;

    /// <summary>JSONLogic over a project's fields: the projects this rule is for.</summary>
    public JsonNode? Matches { get; set; }

    public decimal? MinCompletion { get; set; }

    public decimal? MaxAgeYears { get; set; }

    public decimal? MinValue { get; set; }

    /// <summary>Count the certified value instead of the contract value.</summary>
    public bool UseCertifiedValue { get; set; }
}
