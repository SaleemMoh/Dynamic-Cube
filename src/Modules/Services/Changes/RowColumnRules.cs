using System.Text.RegularExpressions;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Runtime;
using DynamicCube.Platform.Configuration;

namespace DynamicCube.Modules.Services.Changes;

/// <summary>Checks the columns of a rows question as it is saved, and tidies them (codes trimmed, unused settings cleared).</summary>
internal static class RowColumnRules
{
    /// <summary>No rows question takes more rows than this.</summary>
    public const int MostRows = 200;

    /// <summary>No rows question has more columns than this.</summary>
    public const int MostColumns = 30;

    public static List<RowColumn> Check(IReadOnlyList<RowColumn> columns, ValidationErrors errors)
    {
        if (columns.Count == 0)
        {
            errors.Add("columns", "A rows question needs at least one column.");
        }

        if (columns.Count > MostColumns)
        {
            errors.Add("columns", $"At most {MostColumns} columns.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<RowColumn>();
        foreach (var (column, i) in columns.Select((c, i) => (c, i)))
        {
            var at = $"columns[{i}]";
            column.Code = column.Code?.Trim() ?? string.Empty;
            errors.Code($"{at}.code", column.Code, ValidationErrors.IdentifierCode());
            if (column.Code.Length > 0 && !seen.Add(column.Code))
            {
                errors.Add($"{at}.code", $"Column '{column.Code}' appears twice.");
            }

            errors.Required($"{at}.labelAr", column.LabelAr);
            errors.Required($"{at}.labelEn", column.LabelEn);
            errors.MaxLength($"{at}.labelAr", column.LabelAr, 500);
            errors.MaxLength($"{at}.labelEn", column.LabelEn, 500);
            if (column.Type == QuestionType.Rows)
            {
                errors.Add($"{at}.type", "A column cannot hold rows of its own.");
            }

            column.Lov = string.IsNullOrWhiteSpace(column.Lov) ? null : column.Lov.Trim();
            if (column.Type == QuestionType.Select)
            {
                if (column.Lov is null && column.Options.Count == 0)
                {
                    errors.Add($"{at}.options", "A choice column needs options or a list of values.");
                }

                foreach (var (option, j) in column.Options.Select((o, j) => (o, j)))
                {
                    errors.Required($"{at}.options[{j}].value", option.Value);
                    errors.Required($"{at}.options[{j}].labelAr", option.LabelAr);
                    errors.Required($"{at}.options[{j}].labelEn", option.LabelEn);
                }
            }
            else
            {
                column.Lov = null;
                column.Options = [];
                column.Multiple = column.Multiple && column.Type == QuestionType.File;
            }

            var validation = column.Validation ?? new QuestionValidation();
            validation.MinRows = null;
            validation.MaxRows = null;
            if (column.Type == QuestionType.File)
            {
                validation.Extensions = [.. validation.Extensions.Select(e => e.Trim().TrimStart('.').ToLowerInvariant()).Where(e => e.Length > 0).Distinct()];
                if (validation.Extensions.Any(e => !RequestFiles.ExtensionPattern().IsMatch(e)))
                {
                    errors.Add($"{at}.validation.extensions", "Use extensions such as pdf, jpg or docx.");
                }

                if (validation.MaxSizeMb is < 1 or > RequestFiles.LargestMb)
                {
                    errors.Add($"{at}.validation.maxSizeMb", $"Must be between 1 and {RequestFiles.LargestMb}.");
                }
            }
            else
            {
                validation.Extensions = [];
                validation.MaxSizeMb = null;
                validation.MaxFiles = null;
            }

            if (validation.Min > validation.Max)
            {
                errors.Add($"{at}.validation.max", "Must not be less than min.");
            }

            if (validation.Pattern is { Length: > 0 } pattern)
            {
                try
                {
                    _ = new Regex(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
                }
                catch (ArgumentException)
                {
                    errors.Add($"{at}.validation.pattern", "Not a valid regular expression.");
                }
            }

            column.Validation = validation;
            result.Add(column);
        }

        return result;
    }
}
