using System.Security.Claims;
using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.Http;

namespace DynamicCube.Hosting;

/// <summary>Current user taken from the validated Keycloak access token.</summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor, ApiHostOptions options) : ICurrentUser
{
    public const string Anonymous = "anonymous";

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public string UserId =>
        Principal?.FindFirstValue("sub") ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Anonymous;

    public string? UserName => Principal?.FindFirstValue("preferred_username") ?? Principal?.Identity?.Name;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public string AppCode => options.AppCode;

    public string? RequestedCompany =>
        accessor.HttpContext?.Request.Headers[WorkspaceHeaders.Company].FirstOrDefault() is { Length: > 0 and <= 100 } code ? code.Trim() : null;
}
