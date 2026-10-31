namespace DynamicCube.Platform.Security;

/// <summary>The identity on whose behalf the current operation runs.</summary>
public interface ICurrentUser
{
    /// <summary>Stable user identifier (Keycloak subject), or a system identifier for background work.</summary>
    string UserId { get; }

    string? UserName { get; }

    bool IsAuthenticated { get; }

    /// <summary>Code of the app the request came through (e.g. <c>INTERNAL</c>, <c>EXTERNAL</c>, <c>WORKER</c>).</summary>
    string AppCode { get; }

    /// <summary>
    /// The company the user chose to work in (header <see cref="WorkspaceHeaders.Company"/>), if any. Only a request: Apps
    /// Control checks it against the companies the user may work in (ADR-0036 step 5).
    /// </summary>
    string? RequestedCompany => null;
}

/// <summary>Request headers that carry the user's workspace.</summary>
public static class WorkspaceHeaders
{
    /// <summary>The company the user works in, chosen after sign-in or in the header of the internal system.</summary>
    public const string Company = "X-Company";
}
