using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Platform.Security;

public static class PrivilegeEndpointExtensions
{
    /// <summary>Requires the current user to hold <paramref name="action"/> on <paramref name="resourceCode"/> (Apps Control privileges).</summary>
    public static TBuilder RequirePrivilege<TBuilder>(this TBuilder builder, string resourceCode, string action)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var checker = context.HttpContext.RequestServices.GetRequiredService<IAccessChecker>();
            return await checker.CanAsync(resourceCode, action, context.HttpContext.RequestAborted)
                ? await next(context)
                : Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: $"Requires {action} on {resourceCode}.");
        });
}
