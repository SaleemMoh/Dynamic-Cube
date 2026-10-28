namespace DynamicCube.Platform.Expressions;

/// <summary>A JSONLogic rule is invalid (e.g. unknown operation).</summary>
public sealed class JsonLogicException : Exception
{
    public JsonLogicException()
    {
    }

    public JsonLogicException(string message)
        : base(message)
    {
    }

    public JsonLogicException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
