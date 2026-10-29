using DynamicCube.Modules.Content.Runtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DynamicCube.Modules.Content.Endpoints;

internal static class ContentEndpoints
{
    /// <summary>Portal, before or after sign-in: pages and frequently asked questions.</summary>
    public static void MapPublic(RouteGroupBuilder group)
    {
        group.MapGet("/pages", async (ContentService content, CancellationToken ct) => TypedResults.Ok(await content.PagesAsync(ct)))
            .AllowAnonymous().WithName("GetContentPages");
        group.MapGet("/pages/{slug}", async (string slug, ContentService content, CancellationToken ct) => TypedResults.Ok(await content.PageAsync(slug, ct)))
            .AllowAnonymous().WithName("GetContentPage");
        group.MapGet("/faqs", async (string? service, ContentService content, CancellationToken ct) => TypedResults.Ok(await content.FaqsAsync(service, ct)))
            .AllowAnonymous().WithName("GetFaqs");
    }

    public static async ValueTask<object?> Errors(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (ContentNotFoundException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
    }
}
