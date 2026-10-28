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
}
