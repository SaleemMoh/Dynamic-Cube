namespace DynamicCube.Platform.ChangeSets;

/// <summary>A change set operation violated the lifecycle or maker-checker rules.</summary>
public sealed class ChangeSetException : Exception
{
    public ChangeSetException()
    {
    }

    public ChangeSetException(string message)
        : base(message)
    {
    }

    public ChangeSetException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
