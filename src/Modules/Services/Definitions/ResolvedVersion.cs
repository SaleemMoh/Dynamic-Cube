using DynamicCube.Modules.Services.Domain;

namespace DynamicCube.Modules.Services.Definitions;

/// <summary>The question as it was when the version was published.</summary>
public sealed record QuestionSnapshot(
    string Code,
    string LabelAr,
    string LabelEn,
    string? HintAr,
    string? HintEn,
    QuestionType Type,
    string? Lov,
    string? LovParentQuestion,
    IReadOnlyList<QuestionOption> Options,
    bool Multiple,
    QuestionValidation Validation,
    string? Prefill,
    IntegrationFill? Integration = null,
    bool? AllowManualEntry = null,
    IReadOnlyList<RowColumn>? Columns = null,
    string? RecordList = null)
{
    /// <summary>The answer is always the filled value (the applicant may not type or change it).</summary>
    public bool Locked => AllowManualEntry == false && (Prefill is not null || Integration is not null);

    public static QuestionSnapshot Of(Question q) =>
        new(q.Code, q.NameAr, q.NameEn, q.HintAr, q.HintEn, q.Type, q.Lov, q.LovParentQuestion, q.Options, q.Multiple, q.Validation,
            q.Fill == QuestionFill.Party ? q.Prefill : null,
            q.Fill == QuestionFill.Integration ? q.Integration : null,
            q.Fill == QuestionFill.None ? null : q.AllowManualEntry,
            q.Type == QuestionType.Rows ? q.Columns : null,
            q.Type == QuestionType.Rows ? q.RecordList : null);

    /// <summary>The columns of a rows question (none for the other types).</summary>
    public IReadOnlyList<RowColumn> RowColumns => Columns ?? [];

    /// <summary>A column of this rows question as a question of its own (its answer checked like any other), coded <c>QUESTION/COLUMN</c>.</summary>
    public QuestionSnapshot Column(RowColumn column) =>
        new($"{Code}/{column.Code}", column.LabelAr, column.LabelEn, column.HintAr, column.HintEn, column.Type, column.Lov, null, column.Options, column.Multiple, column.Validation, null);

    /// <summary>The question a file belongs to: a file question, or a file column of a rows question (<c>QUESTION/COLUMN</c>).</summary>
    public static QuestionSnapshot? FileTarget(ResolvedVersion version, string code)
    {
        var parts = code.Split('/', 2);
        if (!version.Questions.TryGetValue(parts[0], out var question))
        {
            return null;
        }

        if (parts.Length == 1)
        {
            return question.Type == QuestionType.File ? question : null;
        }

        var column = question.RowColumns.FirstOrDefault(c => c.Code == parts[1] && c.Type == QuestionType.File);
        return column is null ? null : question.Column(column);
    }
}

/// <summary>A published version with snapshots of every question it uses: everything the wizard and the server need.</summary>
public sealed record ResolvedVersion(string ServiceCode, int Number, VersionDefinition Definition, IReadOnlyDictionary<string, QuestionSnapshot> Questions)
{
    /// <summary>Codes of the questions used by the version, in the order they first appear.</summary>
    public IEnumerable<string> QuestionCodes() =>
        Definition.Steps.SelectMany(s => s.Sections).SelectMany(s => s.Questions).Select(q => q.Code).Distinct();
}
