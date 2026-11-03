using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Platform.Expressions;

namespace DynamicCube.Modules.Decisions.Runtime.Matrix;

/// <summary>Checks a matrix configuration: codes known and unique, cells on real rows and columns, rules that parse.</summary>
internal static class MatrixValidator
{
    private static readonly string[] Sources = ["applicant", "reviewer", "integration", "party", "derived", "projects", "given"];
    private static readonly string[] CellKinds = ["points", "worst", "formula", "same", "shared", "empty"];

    public static IReadOnlyDictionary<string, string> Validate(JsonNode? configuration, JsonLogicEvaluator logic)
    {
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        MatrixDefinition definition;
        try
        {
            definition = configuration is JsonObject ? MatrixDefinition.Read(configuration) : throw new JsonException("Not an object.");
        }
        catch (JsonException ex)
        {
            errors[string.Empty] = $"The configuration is a matrix definition: {ex.Message}";
            return errors;
        }

        void Rule(string path, JsonNode? rule)
        {
            if (rule is null)
            {
                return;
            }

            try
            {
                logic.Evaluate(rule, new JsonObject());
            }
            catch (JsonLogicException ex)
            {
                errors[path] = $"Invalid rule: {ex.Message}";
            }
        }

        void Unique<T>(string path, IEnumerable<T> items, Func<T, string> code)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var i = 0;
            foreach (var item in items)
            {
                var c = code(item);
                if (string.IsNullOrWhiteSpace(c))
                {
                    errors[$"{path}[{i}].code"] = "Give the code.";
                }
                else if (!seen.Add(c))
                {
                    errors[$"{path}[{i}].code"] = $"'{c}' is used twice.";
                }

                i++;
            }
        }

        Unique("grades", definition.Grades, g => g.Code);
        Unique("inputs", definition.Inputs, i => i.Code);
        Unique("rows", definition.Rows, r => r.Code);
        Unique("columns", definition.Columns, c => c.Code);
        Unique("scales", definition.Scales, s => s.Code);
        Unique("shared", definition.Shared, s => s.Code);
        Unique("optionSets", definition.OptionSets, s => s.Code);
        if (definition.Rows.Count == 0)
        {
            errors["rows"] = "Add at least one row.";
        }

        if (definition.Columns.Count == 0)
        {
            errors["columns"] = "Add at least one column.";
        }

        var inputs = definition.Inputs.Select(i => i.Code).ToHashSet(StringComparer.Ordinal);
        var rows = definition.Rows.Select(r => r.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var columns = definition.Columns.Select(c => c.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var scales = definition.Scales.Select(s => s.Code).ToHashSet(StringComparer.Ordinal);
        var sets = definition.OptionSets.Select(s => s.Code).ToHashSet(StringComparer.Ordinal);
        if (definition.RowsInput is { Length: > 0 } rowsInput && !inputs.Contains(rowsInput))
        {
            errors["rowsInput"] = $"Unknown input '{rowsInput}'.";
        }

        for (var i = 0; i < definition.Inputs.Count; i++)
        {
            var input = definition.Inputs[i];
            var at = $"inputs[{i}]";
            if (!Sources.Contains(input.Source.Kind))
            {
                errors[$"{at}.source.kind"] = $"One of {string.Join(", ", Sources)}.";
            }

            if (input.OptionSet is { } set && !sets.Contains(set))
            {
                errors[$"{at}.optionSet"] = $"Unknown option set '{set}'.";
            }

            if (input.Source.Kind == "derived" && input.Source.Rule is null)
            {
                errors[$"{at}.source.rule"] = "Give the formula.";
            }

            if (input.Source.Kind == "projects" && input.Source.Function is not ("sum" or "max" or "count" or "distinct"))
            {
                errors[$"{at}.source.function"] = "One of sum, max, count, distinct.";
            }

            Rule($"{at}.source.rule", input.Source.Rule);
            Rule($"{at}.source.where", input.Source.Where);
        }

        if (definition.Sizes is { } sizes && !inputs.Contains(sizes.Input))
        {
            errors["sizes.input"] = $"Unknown input '{sizes.Input}'.";
        }

        var sharedCodes = definition.Shared.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var all = definition.Cells.Select((c, i) => (Cell: c, At: $"cells[{i}]", IsShared: false))
            .Concat(definition.Shared.Select((x, i) => (Cell: x.Cell, At: $"shared[{i}].cell", IsShared: true)))
            .ToList();
        foreach (var (cell, at, isShared) in all)
        {
            if (!isShared && !rows.Contains(cell.Row))
            {
                errors[$"{at}.row"] = $"Unknown row '{cell.Row}'.";
            }

            if (!isShared && !columns.Contains(cell.Column))
            {
                errors[$"{at}.column"] = $"Unknown column '{cell.Column}'.";
            }

            if (isShared && cell.Kind is "shared" or "same")
            {
                errors[$"{at}.kind"] = "A shared calculation holds points, worst grade or a formula.";
            }

            if (!isShared && cell.Kind == "shared" && (cell.Shared is null || !sharedCodes.Contains(cell.Shared)))
            {
                errors[$"{at}.shared"] = $"Unknown shared calculation '{cell.Shared}'.";
            }

            if (!CellKinds.Contains(cell.Kind))
            {
                errors[$"{at}.kind"] = $"One of {string.Join(", ", CellKinds)}.";
            }

            if (cell.Kind == "points" && (cell.Scale is null || !scales.Contains(cell.Scale)))
            {
                errors[$"{at}.scale"] = "Choose the scale that turns points into a grade.";
            }

            if (cell.Kind == "same" && (cell.SameRow is null || !rows.Contains(cell.SameRow)))
            {
                errors[$"{at}.sameRow"] = "Choose the row whose cell is copied.";
            }

            if (cell.Kind == "formula" && cell.GradeRule is null)
            {
                errors[$"{at}.gradeRule"] = "Give the rule that gives the grade.";
            }

            Rule($"{at}.gradeRule", cell.GradeRule);
            Rule($"{at}.scoreRule", cell.ScoreRule);
            Rule($"{at}.detailsRule", cell.DetailsRule);
            for (var g = 0; g < cell.Groups.Count; g++)
            {
                for (var c = 0; c < cell.Groups[g].Criteria.Count; c++)
                {
                    var criterion = cell.Groups[g].Criteria[c];
                    var cp = $"{at}.groups[{g}].criteria[{c}]";
                    if (!inputs.Contains(criterion.Input))
                    {
                        errors[$"{cp}.input"] = $"Unknown input '{criterion.Input}'.";
                    }

                    if (criterion.Scoring is not ("bands" or "options" or "direct"))
                    {
                        errors[$"{cp}.scoring"] = "One of bands, options, direct.";
                    }

                    Rule($"{cp}.appliesWhen", criterion.AppliesWhen);
                }
            }
        }

        foreach (var (rules, path) in new[] { (definition.Gates.Select(g => (g.Rule, g.Rows)).ToList(), "gates"), (definition.Adjustments.Select(a => (a.Rule, a.Rows)).ToList(), "adjustments") })
        {
            for (var i = 0; i < rules.Count; i++)
            {
                Rule($"{path}[{i}].rule", rules[i].Rule);
                foreach (var row in rules[i].Rows.Where(r => !rows.Contains(r)))
                {
                    errors[$"{path}[{i}].rows"] = $"Unknown row '{row}'.";
                }
            }
        }

        return errors;
    }
}
