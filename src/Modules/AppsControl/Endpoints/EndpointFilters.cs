using DynamicCube.Modules.AppsControl.Access;
using DynamicCube.Modules.AppsControl.Lovs;
using DynamicCube.Platform.ChangeSets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.AppsControl.Endpoints;

/// <summary>Rejects locked or disabled accounts and maps Apps Control exceptions to problem responses.</summary>
internal static class AppsControlFilters
{
    public static async ValueTask<object?> ActiveUser(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var access = await context.HttpContext.RequestServices.GetRequiredService<AccessResolver>().GetAsync(context.HttpContext.RequestAborted);
        return access.IsActive ? await next(context) : Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: "The account is not active.");
    }

    public static async ValueTask<object?> Errors(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (ChangeValidationException ex)
        {
            return Results.ValidationProblem(ex.Errors.ToDictionary(e => e.Key, e => e.Value), title: "The change is invalid.");
        }
        catch (ChangeSetException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message);
        }
        catch (LovNotFoundException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
    }
}
