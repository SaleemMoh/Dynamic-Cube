namespace DynamicCube.Platform.Integration;

/// <summary>
/// What the integration calls made now are for (e.g. a service request), so the call log can show them as the
/// request's evidence. Set around the calls with <see cref="Use"/>; it flows to the calls awaited inside.
/// </summary>
public static class IntegrationSubject
{
    private static readonly AsyncLocal<SubjectRef?> Ambient = new();

    /// <summary>The subject the calls are for now, if any.</summary>
    public static SubjectRef? Current => Ambient.Value;

    /// <summary>Marks the calls made until the result is disposed as made for the subject.</summary>
    public static IDisposable Use(string type, Guid id)
    {
        var previous = Ambient.Value;
        Ambient.Value = new SubjectRef(type, id);
        return new Restore(previous);
    }

    public sealed record SubjectRef(string Type, Guid Id);

    private sealed class Restore(SubjectRef? previous) : IDisposable
    {
        public void Dispose() => Ambient.Value = previous;
    }
}

/// <summary>One integration call made for a subject, as kept in the call log (personal data masked).</summary>
public sealed record IntegrationEvidence(
    Guid Id,
    string OperationCode,
    string OperationNameAr,
    string OperationNameEn,
    DateTimeOffset At,
    int DurationMs,
    IntegrationOutcome Outcome,
    int? HttpStatus,
    string? Code,
    string? Message,
    bool FromCache,
    string Method,
    string Url,
    string? RequestBody,
    string? ResponseBody,
    string UserId);

/// <summary>The integration calls made for a subject, newest first (implemented by the Integration Manager).</summary>
public interface IIntegrationEvidence
{
    Task<IReadOnlyList<IntegrationEvidence>> OfSubjectAsync(string subjectType, Guid subjectId, CancellationToken cancellationToken);
}
