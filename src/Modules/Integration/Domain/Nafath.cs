namespace DynamicCube.Modules.Integration.Domain;

public enum NafathRequestKind
{
    /// <summary>Signing in through Nafath's web page (the sign-in bridge).</summary>
    SignIn,

    /// <summary>A request in the person's Nafath app, answered by picking the number shown.</summary>
    Verification,
}

public enum NafathRequestStatus
{
    Waiting,
    Verified,
    Rejected,
    Expired,

    /// <summary>A sign-in whose code Keycloak has redeemed.</summary>
    Used,
}

/// <summary>
/// One Nafath sign-in or verification (ADR-0043). The person's ID, Nafath's state and Nafath's answer are kept encrypted
/// (data protection) and only while they are needed.
/// </summary>
public sealed class NafathRequest
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public NafathRequestKind Kind { get; set; }

    public NafathRequestStatus Status { get; set; }

    /// <summary>Nafath's service (Login, OpenAccount…).</summary>
    public string? Service { get; set; }

    /// <summary>What it is for, e.g. <c>SERVICE_REQUEST</c>, or <c>SIGN_IN</c>.</summary>
    public string SubjectType { get; set; } = string.Empty;

    public Guid? SubjectId { get; set; }

    public string? ProtectedNationalId { get; set; }

    public string? TransId { get; set; }

    /// <summary>The number the person picks in the app.</summary>
    public string? Random { get; set; }

    public string? ProtectedState { get; set; }

    /// <summary>Base64 SHA-256 of Nafath's state: how its redirect back is matched.</summary>
    public string? HashedState { get; set; }

    public string? ClientRedirectUri { get; set; }

    public string? ClientState { get; set; }

    public string? ClientNonce { get; set; }

    /// <summary>SHA-256 of the code given to Keycloak.</summary>
    public string? CodeHash { get; set; }

    /// <summary>Nafath's answer (the person), encrypted.</summary>
    public string? ProtectedPerson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
