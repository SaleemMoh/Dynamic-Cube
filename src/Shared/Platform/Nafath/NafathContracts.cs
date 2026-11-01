namespace DynamicCube.Platform.Nafath;

/// <summary>Where a Nafath verification stands.</summary>
public enum NafathStatus
{
    /// <summary>The person has not answered yet (or approved and the signed answer has not arrived).</summary>
    Waiting,

    /// <summary>Approved in the Nafath app; the signed answer was checked and names the person.</summary>
    Verified,
    Rejected,
    Expired,
}

/// <summary>Who Nafath says the person is, from its signed answer.</summary>
public sealed record NafathPerson(
    string Identifier,
    string? FullNameAr,
    string? FullNameEn,
    string? Nationality,
    string? DateOfBirth,
    string? Gender,
    string? DocumentExpiry);

/// <summary>A verification sent to the person's Nafath app: the number to show them and until when it can be answered.</summary>
public sealed record NafathChallenge(Guid Id, string Random, DateTimeOffset ExpiresAt);

/// <summary>A verification as it stands; <see cref="Person"/> is set once it is verified.</summary>
public sealed record NafathCheck(Guid Id, string SubjectType, Guid? SubjectId, NafathStatus Status, string Random, DateTimeOffset ExpiresAt, NafathPerson? Person, DateTimeOffset? CompletedAt);

/// <summary>
/// Nafath verifications in the app (ADR-0043): the person present on the screen is sent a request in the Nafath app, picks
/// the number shown and approves; Nafath's signed answer confirms who they are (implemented by the Integration Manager).
/// </summary>
public interface INafathVerifications
{
    /// <summary>False when Nafath is not set up on this installation.</summary>
    bool IsConfigured { get; }

    /// <summary>Sends the request to the person's app.</summary>
    /// <param name="purpose">Nafath's service the request is for (<c>Login</c>, <c>OpenAccount</c>…); null: the configured one.</param>
    /// <exception cref="NafathException">Nafath refused (e.g. a request for the person is already waiting).</exception>
    Task<NafathChallenge> StartAsync(string nationalId, string? purpose, string subjectType, Guid? subjectId, string? userIp, CancellationToken cancellationToken);

    /// <summary>Where it stands now (Nafath is asked while it waits); null when there is no such verification.</summary>
    Task<NafathCheck?> CheckAsync(Guid id, string? userIp, CancellationToken cancellationToken);
}

/// <summary>Nafath refused or cannot be reached; the message says why.</summary>
public sealed class NafathException : Exception
{
    public NafathException()
    {
    }

    public NafathException(string message)
        : base(message)
    {
    }

    public NafathException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public NafathException(string message, string? code)
        : base(message)
    {
        Code = code;
    }

    /// <summary>Nafath's error code (e.g. <c>400-034-050</c>: a request is already active).</summary>
    public string? Code { get; }
}
