using System.Text.RegularExpressions;
using DynamicCube.Platform.ChangeSets;

namespace DynamicCube.Platform.Configuration;

/// <summary>Collects field-level problems and throws them together.</summary>
public sealed partial class ValidationErrors
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

    public bool HasErrors => _errors.Count > 0;

    public void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var list))
        {
            _errors[field] = list = [];
        }

        list.Add(message);
    }

    public void Required(string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(field, "Required.");
        }
    }

    public void MaxLength(string field, string? value, int max)
    {
        if (value is not null && value.Length > max)
        {
            Add(field, $"At most {max} characters.");
        }
    }

    public void Code(string field, string? value, Regex pattern)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(field, "Required.");
        }
        else if (!pattern.IsMatch(value))
        {
            Add(field, $"'{value}' is not a valid code.");
        }
    }

    public void ThrowIfAny()
    {
        if (HasErrors)
        {
            throw new ChangeValidationException(_errors.ToDictionary(e => e.Key, e => e.Value.ToArray(), StringComparer.Ordinal));
        }
    }

    /// <summary>General configuration codes: letters, digits, <c>_ . -</c>.</summary>
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_.\\-]{0,99}$")]
    public static partial Regex GeneralCode();

    /// <summary>Upper-case codes: letters, digits, <c>_ -</c>; class and attribute codes name generated views and columns (<c>-</c> becomes <c>_</c>).</summary>
    [GeneratedRegex("^[A-Z][A-Z0-9_\\-]{0,50}$")]
    public static partial Regex IdentifierCode();

    /// <summary>Usernames as issued by the identity provider.</summary>
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_.@\\-]{0,255}$")]
    public static partial Regex UserName();

    /// <summary>PostgreSQL identifiers accepted for registered data objects and columns.</summary>
    [GeneratedRegex("^[a-z_][a-z0-9_]{0,62}$")]
    public static partial Regex SqlIdentifier();
}
