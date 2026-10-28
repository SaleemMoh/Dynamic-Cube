namespace DynamicCube.Platform.ChangeSets;

/// <summary>A change item is invalid; <see cref="Errors"/> lists field-level problems for the maker.</summary>
public sealed class ChangeValidationException : Exception
{
    public ChangeValidationException()
        : this("The change is invalid.")
    {
    }

    public ChangeValidationException(string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ChangeValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ChangeValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("The change is invalid: " + string.Join("; ", errors.Select(e => $"{e.Key}: {string.Join(", ", e.Value)}")))
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public static ChangeValidationException For(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
