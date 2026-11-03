using System.Text.Json.Nodes;
using DynamicCube.Modules.Decisions.Changes;
using DynamicCube.Modules.Decisions.Runtime;
using DynamicCube.Platform.Decisions;
using DynamicCube.Platform.Parties;
using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DynamicCube.Modules.Decisions.Endpoints;

internal static class DecisionEndpoints
{
    /// <summary>Internal system: installed engines, trying a rule-set version out, recorded evaluations.</summary>
    public static void MapInternal(RouteGroupBuilder group)
    {
        group.MapGet("/engines", (DecisionService service) => TypedResults.Ok(service.Engines()))
            .RequirePrivilege(DecisionResources.Config, CatalogActions.View).WithName("ListDecisionEngines");
        group.MapPost("/rule-sets/{code}/try", async (string code, TryRequest request, DecisionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.TryAsync(code, request, ct))).RequirePrivilege(DecisionResources.Config, CatalogActions.View).WithName("TryRuleSet");
        group.MapGet("/evaluations", async (string? subjectType, Guid? subjectId, string? ruleSet, int? take, DecisionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListAsync(subjectType, subjectId, ruleSet, take ?? 50, ct))).RequirePrivilege(DecisionResources.Evaluations, CatalogActions.View).WithName("ListEvaluations");
    }

    /// <summary>External host: public calculators (no sign-in, nothing recorded).</summary>
    public static void MapPublic(RouteGroupBuilder group)
    {
        group.MapGet("/calculators", async (DecisionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.CalculatorsAsync(ct))).AllowAnonymous().WithName("ListCalculators");
        group.MapGet("/calculators/{code}", async (string code, DecisionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.CalculatorAsync(code, ct))).AllowAnonymous().WithName("GetCalculator");
        group.MapPost("/calculators/{code}", async (string code, JsonObject input, DecisionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.CalculateAsync(code, input, ct))).AllowAnonymous().WithName("Calculate");

        // Signed in: start from the acting party's latest data.
        group.MapGet("/calculators/{code}/prefill", async (string code, IActingPartyAccessor acting, IPartyFacts parties, DecisionService service, CancellationToken ct) =>
        {
            var party = await acting.GetAsync(ct);
            var facts = await parties.GetAsync(party.PartyId, refreshProfile: false, ct);
            return TypedResults.Ok(facts is null ? new JsonObject() : await service.PrefillAsync(code, facts.ToJson(), ct));
        }).RequireAuthorization().WithName("PrefillCalculator");
    }

    public static async ValueTask<object?> Errors(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (DecisionNotFoundException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (DecisionException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, detail: ex.Message);
        }
    }
}
