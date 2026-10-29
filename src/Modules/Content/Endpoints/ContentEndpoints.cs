using DynamicCube.Modules.Content.Changes;
using DynamicCube.Modules.Content.Runtime;
using DynamicCube.Platform.Security;
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

        // "Was this page helpful?": anyone may answer; visitors who are not signed in send the id their browser keeps.
        group.MapGet("/feedback", async (string page, Guid? visitor, FeedbackService feedback, CancellationToken ct) => TypedResults.Ok(await feedback.PageAsync(page, visitor, ct)))
            .AllowAnonymous().WithName("GetPageFeedback");
        group.MapPost("/feedback", async (FeedbackRequest request, FeedbackService feedback, CancellationToken ct) => TypedResults.Ok(await feedback.AnswerAsync(request, ct)))
            .AllowAnonymous().WithName("AnswerPageFeedback");

        // Service ratings: anyone reads the average; signed-in users rate.
        group.MapGet("/ratings/{service}", async (string service, FeedbackService feedback, CancellationToken ct) => TypedResults.Ok(await feedback.RatingAsync(service, ct)))
            .AllowAnonymous().WithName("GetServiceRating");
        group.MapPut("/ratings/{service}", async (string service, RatingRequest request, FeedbackService feedback, CancellationToken ct) =>
            TypedResults.Ok(await feedback.RateAsync(service, request.Stars, ct)))
            .RequireAuthorization().WithName("RateService");
    }

    /// <summary>Internal system: what visitors said, for the content team.</summary>
    public static void MapInternal(RouteGroupBuilder group)
    {
        group.MapGet("/feedback/report", async (FeedbackService feedback, CancellationToken ct) => TypedResults.Ok(await feedback.ReportAsync(ct)))
            .RequirePrivilege(ContentResources.Config, CatalogActions.View).WithName("GetFeedbackReport");
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
        catch (FeedbackValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }
    }
}
