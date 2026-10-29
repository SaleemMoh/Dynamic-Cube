using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace DynamicCube.Hosting;

/// <summary>Turns <see cref="IdentityMismatchException"/> into a 403 problem the apps can show, instead of a 500.</summary>
internal sealed class IdentityMismatchHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not IdentityMismatchException mismatch)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = StatusCodes.Status403Forbidden, Title = "Account linked to another identity", Detail = mismatch.Message },
        });
    }
}
