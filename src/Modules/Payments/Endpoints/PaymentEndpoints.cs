using DynamicCube.Modules.Payments.Changes;
using DynamicCube.Modules.Payments.Domain;
using DynamicCube.Modules.Payments.Runtime;
using DynamicCube.Platform.Payments;
using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DynamicCube.Modules.Payments.Endpoints;

internal static class PaymentEndpoints
{
    /// <summary>Largest payment notification read (providers send a few hundred bytes).</summary>
    private const int MaxCallbackBytes = 64 * 1024;

    /// <summary>Internal system: bills (search, details; recording a payment made elsewhere and cancelling need EDIT).</summary>
    public static void MapInternal(RouteGroupBuilder group)
    {
        var bills = group.MapGroup("/bills").RequirePrivilege(PaymentResources.Bills, CatalogActions.View);
        bills.MapGet("/", async (string? search, string? status, int? skip, int? take, BillRegistry registry, CancellationToken ct) =>
            TypedResults.Ok(await registry.SearchAsync(search, Enum.TryParse<BillStatus>(status, true, out var s) ? s : null, skip ?? 0, take ?? 50, ct))).WithName("SearchBills");
        bills.MapGet("/{id:guid}", async (Guid id, BillRegistry registry, CancellationToken ct) => TypedResults.Ok(await registry.GetAsync(id, ct))).WithName("GetBill");

        var changes = group.MapGroup("/bills/{id:guid}").RequirePrivilege(PaymentResources.Bills, CatalogActions.Edit);
        changes.MapPost("/mark-paid", async (Guid id, MarkPaidRequest request, BillRegistry registry, CancellationToken ct) => TypedResults.Ok(await registry.MarkPaidAsync(id, request, ct))).WithName("MarkBillPaid");
        changes.MapPost("/cancel", async (Guid id, CancelBillRequest request, BillRegistry registry, CancellationToken ct) => TypedResults.Ok(await registry.CancelAsync(id, request, ct))).WithName("CancelBill");
    }

    /// <summary>External host: payment notifications from providers (anonymous; each must be signed).</summary>
    public static void MapCallbacks(RouteGroupBuilder group)
    {
        group.MapPost("/callbacks/{provider}", async (string provider, HttpRequest request, PaymentCallbacks callbacks, CancellationToken ct) =>
        {
            if (request.ContentLength > MaxCallbackBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync(ct);
            if (body.Length > MaxCallbackBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            return await callbacks.HandleAsync(provider, body, request.Headers[PaymentCallbacks.SignatureHeader].ToString(), ct) switch
            {
                CallbackOutcome.Accepted => Results.Ok(new { received = true }),
                CallbackOutcome.Unauthorized => Results.Unauthorized(),
                CallbackOutcome.NotFound => Results.NotFound(),
                _ => Results.UnprocessableEntity(),
            };
        }).AllowAnonymous().DisableAntiforgery().WithName("PaymentCallback");
    }

    public static async ValueTask<object?> Errors(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (BillNotFoundException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (Exception ex) when (ex is BillConflictException or PaymentException)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message);
        }
        catch (BillValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }
    }
}
