using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DynamicCube.Platform.Expressions;

namespace DynamicCube.Modules.Services.Definitions;

/// <summary>Checks a version definition before it can be published: structure, codes, references and rules.</summary>
internal static partial class DefinitionValidator
{
    [GeneratedRegex("^[A-Z][A-Z0-9_\\-]{0,50}$")]
    private static partial Regex Code();

    /// <param name="questionExists">Whether a question code exists (published or created earlier in the same change set).</param>
    public static void Validate(VersionDefinition definition, Func<string, bool> questionExists, JsonLogicEvaluator evaluator, ValidationErrors errors)
    {
        Unique(definition.Levels.Select(l => l.Code), "definition.levels", errors);
        foreach (var (level, i) in definition.Levels.Select((l, i) => (l, i)))
        {
            var field = $"definition.levels[{i}]";
            CheckCode(level.Code, field + ".code", errors);
            errors.Required(field + ".nameAr", level.NameAr);
            errors.Required(field + ".nameEn", level.NameEn);
            if (level.SlaWorkingDays is < 1 or > 365)
            {
                errors.Add(field + ".slaWorkingDays", "Must be between 1 and 365.");
            }
        }

        if (definition.Steps.Count == 0)
        {
            errors.Add("definition.steps", "A version needs at least one step.");
        }

        Unique(definition.Steps.Select(s => s.Code), "definition.steps", errors);
        Unique(definition.Steps.SelectMany(s => s.Sections).Select(s => s.Code), "definition.sections", errors);
        foreach (var type in new[] { StepType.Level, StepType.Fees, StepType.Review, StepType.Declaration })
        {
            if (definition.Steps.Count(s => s.Type == type) > 1)
            {
                errors.Add("definition.steps", $"Only one {type.ToString().ToUpperInvariant()} step is allowed.");
            }
        }

        for (var i = 0; i < definition.Steps.Count; i++)
        {
            var step = definition.Steps[i];
            var field = $"definition.steps[{i}]";
            CheckCode(step.Code, field + ".code", errors);
            errors.Required(field + ".titleAr", step.TitleAr);
            errors.Required(field + ".titleEn", step.TitleEn);
            switch (step.Type)
            {
                case StepType.Form when step.Sections.Count == 0:
                    errors.Add(field + ".sections", "A form step needs at least one section.");
                    break;
                case StepType.Level when definition.Levels.Count == 0:
                    errors.Add(field + ".type", "A level step needs service levels.");
                    break;
                case StepType.Declaration:
                    errors.Required(field + ".textAr", step.TextAr);
                    errors.Required(field + ".textEn", step.TextEn);
                    break;
            }

            if (step.Type != StepType.Form && step.Sections.Count > 0)
            {
                errors.Add(field + ".sections", "Only form steps have sections.");
            }

            for (var j = 0; j < step.Sections.Count; j++)
            {
                var section = step.Sections[j];
                var sectionField = $"{field}.sections[{j}]";
                CheckCode(section.Code, sectionField + ".code", errors);
                errors.Required(sectionField + ".titleAr", section.TitleAr);
                errors.Required(sectionField + ".titleEn", section.TitleEn);
                CheckRule(section.VisibleWhen, sectionField + ".visibleWhen", evaluator, errors);
                if (section.Questions.Count == 0)
                {
                    errors.Add(sectionField + ".questions", "A section needs at least one question.");
                }

                Unique(section.Questions.Select(q => q.Code), sectionField + ".questions", errors);
                for (var k = 0; k < section.Questions.Count; k++)
                {
                    var use = section.Questions[k];
                    var useField = $"{sectionField}.questions[{k}]";
                    if (string.IsNullOrWhiteSpace(use.Code) || !questionExists(use.Code))
                    {
                        errors.Add(useField + ".code", $"Question '{use.Code}' does not exist.");
                    }

                    CheckRule(use.RequiredWhen, useField + ".requiredWhen", evaluator, errors);
                    CheckRule(use.VisibleWhen, useField + ".visibleWhen", evaluator, errors);
                }
            }
        }

        if (definition.Workflow is not null)
        {
            errors.Add("definition.workflow", "A version no longer names a workflow: requests are processed by the service's own workflow (add a version to it instead).");
        }

        Rules(definition.Eligibility, "definition.eligibility", evaluator, errors, null);
        Rules(definition.Validations, "definition.validations", evaluator, errors, definition.Steps.Select(s => s.Code).ToHashSet());
        Fees(definition, evaluator, errors);
        Card(definition.Card, errors);
    }

    private static void Card(ServiceCardDefinition card, ValidationErrors errors)
    {
        const string field = "definition.card";
        errors.MaxLength(field + ".audienceAr", card.AudienceAr, 500);
        errors.MaxLength(field + ".audienceEn", card.AudienceEn, 500);
        errors.MaxLength(field + ".termsAr", card.TermsAr, 8000);
        errors.MaxLength(field + ".termsEn", card.TermsEn, 8000);
        for (var i = 0; i < card.Steps.Count; i++)
        {
            errors.Required($"{field}.steps[{i}].textAr", card.Steps[i].TextAr);
            errors.Required($"{field}.steps[{i}].textEn", card.Steps[i].TextEn);
            errors.MaxLength($"{field}.steps[{i}].textAr", card.Steps[i].TextAr, 1000);
            errors.MaxLength($"{field}.steps[{i}].textEn", card.Steps[i].TextEn, 1000);
        }

        for (var i = 0; i < card.RequiredDocuments.Count; i++)
        {
            var document = card.RequiredDocuments[i];
            errors.Required($"{field}.requiredDocuments[{i}].nameAr", document.NameAr);
            errors.Required($"{field}.requiredDocuments[{i}].nameEn", document.NameEn);
            errors.MaxLength($"{field}.requiredDocuments[{i}].noteAr", document.NoteAr, 1000);
            errors.MaxLength($"{field}.requiredDocuments[{i}].noteEn", document.NoteEn, 1000);
        }

        if (card.Channels.Distinct().Count() != card.Channels.Count)
        {
            errors.Add(field + ".channels", "A channel is listed more than once.");
        }

        // Links open from the public portal, so only web pages (or paths of the portal itself) are allowed.
        if (card.FaqUrl is { Length: > 0 } url && !(WebUrl().IsMatch(url) || (url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal))))
        {
            errors.Add(field + ".faqUrl", "Use an http(s) address or a path starting with /.");
        }

        if (card.Phone is { Length: > 0 } phone && !Phone().IsMatch(phone))
        {
            errors.Add(field + ".phone", "Digits, spaces, + and - only.");
        }

        if (card.Email is { Length: > 0 } email && !Email().IsMatch(email))
        {
            errors.Add(field + ".email", "Not an email address.");
        }
    }

    [GeneratedRegex("^https?://[^\\s]{1,500}$", RegexOptions.IgnoreCase)]
    private static partial Regex WebUrl();

    [GeneratedRegex("^\\+?[0-9][0-9 \\-]{2,30}$")]
    private static partial Regex Phone();

    [GeneratedRegex("^[^@\\s]{1,100}@[^@\\s]{1,150}\\.[^@\\s]{2,30}$")]
    private static partial Regex Email();

    private static void Rules(List<RuleDefinition> rules, string field, JsonLogicEvaluator evaluator, ValidationErrors errors, HashSet<string>? steps)
    {
        Unique(rules.Select(r => r.Code), field, errors);
        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            CheckCode(rule.Code, $"{field}[{i}].code", errors);
            errors.Required($"{field}[{i}].messageAr", rule.MessageAr);
            errors.Required($"{field}[{i}].messageEn", rule.MessageEn);
            if (rule.Rule is null)
            {
                errors.Add($"{field}[{i}].rule", "Required.");
            }

            CheckRule(rule.Rule, $"{field}[{i}].rule", evaluator, errors);
            if (steps is not null && rule.Step is not null && !steps.Contains(rule.Step))
            {
                errors.Add($"{field}[{i}].step", $"Step '{rule.Step}' does not exist.");
            }
        }
    }

    private static void Fees(VersionDefinition definition, JsonLogicEvaluator evaluator, ValidationErrors errors)
    {
        var levels = definition.Levels.Select(l => l.Code).ToHashSet();
        Unique(definition.Fees.Select(f => f.Code), "definition.fees", errors);
        for (var i = 0; i < definition.Fees.Count; i++)
        {
            var fee = definition.Fees[i];
            var field = $"definition.fees[{i}]";
            CheckCode(fee.Code, field + ".code", errors);
            errors.Required(field + ".nameAr", fee.NameAr);
            errors.Required(field + ".nameEn", fee.NameEn);
            foreach (var level in fee.Levels.Where(l => !levels.Contains(l)))
            {
                errors.Add(field + ".levels", $"Level '{level}' does not exist.");
            }

            CheckRule(fee.When, field + ".when", evaluator, errors);
            var kinds = (fee.Fixed is not null ? 1 : 0) + (fee.Formula is not null ? 1 : 0) + (fee.Bracket is not null ? 1 : 0);
            if (kinds != 1)
            {
                errors.Add(field, "Give exactly one of fixed, formula or bracket.");
            }

            if (fee.Fixed < 0)
            {
                errors.Add(field + ".fixed", "Must not be negative.");
            }

            CheckRule(fee.Formula, field + ".formula", evaluator, errors);
            if (fee.Bracket is { } bracket)
            {
                if (bracket.Of is null)
                {
                    errors.Add(field + ".bracket.of", "Required.");
                }

                CheckRule(bracket.Of, field + ".bracket.of", evaluator, errors);
                if (bracket.Rows.Count == 0)
                {
                    errors.Add(field + ".bracket.rows", "Add at least one row.");
                }

                for (var r = 0; r < bracket.Rows.Count; r++)
                {
                    var row = bracket.Rows[r];
                    if (row.To is not null && row.To <= row.From)
                    {
                        errors.Add($"{field}.bracket.rows[{r}].to", "Must be greater than from.");
                    }

                    if (row.Amount < 0)
                    {
                        errors.Add($"{field}.bracket.rows[{r}].amount", "Must not be negative.");
                    }

                    if (r > 0 && (bracket.Rows[r - 1].To is null || bracket.Rows[r - 1].To > row.From))
                    {
                        errors.Add($"{field}.bracket.rows[{r}].from", "Rows must be in order and must not overlap.");
                    }
                }
            }

            if (fee.VatRate is < 0 or > 1)
            {
                errors.Add(field + ".vatRate", "Must be between 0 and 1.");
            }
        }
    }

    private static void CheckRule(JsonNode? rule, string field, JsonLogicEvaluator evaluator, ValidationErrors errors)
    {
        if (rule is null)
        {
            return;
        }

        try
        {
            evaluator.Evaluate(rule, SampleFacts);
        }
        catch (JsonLogicException ex)
        {
            errors.Add(field, $"Invalid rule: {ex.Message}");
        }
    }

    private static void CheckCode(string? code, string field, ValidationErrors errors)
    {
        if (string.IsNullOrEmpty(code) || !Code().IsMatch(code))
        {
            errors.Add(field, "Use upper-case letters, digits, _ and -, starting with a letter.");
        }
    }

    private static void Unique(IEnumerable<string> codes, string field, ValidationErrors errors)
    {
        foreach (var duplicate in codes.GroupBy(c => c).Where(g => g.Count() > 1))
        {
            errors.Add(field, $"'{duplicate.Key}' is used more than once.");
        }
    }

    /// <summary>An empty applicant: rules are evaluated once against it to catch unknown operations.</summary>
    private static readonly JsonObject SampleFacts = new()
    {
        ["data"] = new JsonObject(),
        ["level"] = null,
        ["today"] = "2026-01-01",
        ["party"] = new JsonObject { ["kind"] = "ESTABLISHMENT", ["identifiers"] = new JsonObject(), ["profile"] = new JsonObject(), ["sanctions"] = new JsonArray(), ["credentials"] = new JsonArray() },
        ["applicant"] = new JsonObject { ["relationship"] = null },
    };
}
