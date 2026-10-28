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
    string? Prefill)
{
    public static QuestionSnapshot Of(Question q) =>
        new(q.Code, q.NameAr, q.NameEn, q.HintAr, q.HintEn, q.Type, q.Lov, q.LovParentQuestion, q.Options, q.Multiple, q.Validation, q.Prefill);
}

/// <summary>A published version with snapshots of every question it uses: everything the wizard and the server need.</summary>
public sealed record ResolvedVersion(string ServiceCode, int Number, VersionDefinition Definition, IReadOnlyDictionary<string, QuestionSnapshot> Questions)
{
    /// <summary>Codes of the questions used by the version, in the order they first appear.</summary>
    public IEnumerable<string> QuestionCodes() =>
        Definition.Steps.SelectMany(s => s.Sections).SelectMany(s => s.Questions).Select(q => q.Code).Distinct();
}
