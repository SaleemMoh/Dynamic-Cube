using System.Text.Json.Nodes;
using DynamicCube.Modules.Parties.Changes;
using DynamicCube.Modules.Parties.Persistence;
using DynamicCube.Modules.Parties.Services;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Integration;
using DynamicCube.Platform.Parties;
using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Parties.Endpoints;

public sealed record PartyPage(IReadOnlyList<PartySummary> Items, int Total);

public sealed record CreatedResponse(Guid Id);

public sealed record SetProfileValueRequest(JsonNode? Value);

internal static class PartyEndpoints
{
    /// <summary>Portal: the applicant's acting contexts and the validated acting party.</summary>
    public static void MapExternal(RouteGroupBuilder group)
    {
        group.MapGet("/me/contexts", async (bool? refresh, ContextService contexts, CancellationToken ct) =>
            TypedResults.Ok(await contexts.GetAsync(refresh ?? false, ct))).WithName("GetActingContexts");

        group.MapGet("/me/acting", async (IActingPartyAccessor acting, CancellationToken ct) =>
            TypedResults.Ok(await acting.GetAsync(ct))).WithName("GetActingParty");
    }

    /// <summary>Internal system: the registry for employees.</summary>
    public static void MapInternal(RouteGroupBuilder group)
    {
        // For the question bank and integration inputs: what a party's data offers (no registry rights needed).
        group.MapGet("/fact-fields", async (IPartyFactFields fields, CancellationToken ct) => TypedResults.Ok(await fields.ListAsync(ct)))
            .WithName("GetPartyFactFields");

        // The kind as the API writes it (ESTABLISHMENT, INDIVIDUAL); query binding of the enum itself only takes its C# name.
        group.MapGet("/parties", async Task<IResult> (string? search, string? kind, string? app, int? skip, int? take, PartyAdminService service, CancellationToken ct) =>
        {
            PartyKind? partyKind = null;
            if (!string.IsNullOrWhiteSpace(kind))
            {
                if (!Enum.TryParse<PartyKind>(kind, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["kind"] = ["Use INDIVIDUAL or ESTABLISHMENT."] });
                }

                partyKind = parsed;
            }

            var (items, total) = await service.SearchAsync(search, partyKind, skip ?? 0, take ?? 50, ct, app);
            return TypedResults.Ok(new PartyPage(items, total));
        }).RequirePrivilege(PartyResources.Parties, CatalogActions.View).WithName("SearchParties");

        group.MapGet("/parties/{id:guid}", async (Guid id, PartyAdminService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(id, ct))).RequirePrivilege(PartyResources.Parties, CatalogActions.View).WithName("GetParty");

        group.MapPost("/parties", async (RegisterPartyRequest request, PartyAdminService service, CancellationToken ct) =>
        {
            var id = await service.RegisterAsync(request, ct);
            return TypedResults.Created($"/api/party/v1/parties/{id}", new CreatedResponse(id));
        }).RequirePrivilege(PartyResources.Parties, CatalogActions.Create).WithName("RegisterParty");

        group.MapPost("/parties/{id:guid}/identifiers", async (Guid id, AddIdentifierRequest request, PartyAdminService service, CancellationToken ct) =>
        {
            await service.AddIdentifierAsync(id, request, ct);
            return TypedResults.NoContent();
        }).RequirePrivilege(PartyResources.Parties, CatalogActions.Edit).WithName("AddPartyIdentifier");

        group.MapPost("/parties/{id:guid}/profile/refresh", async (Guid id, bool? force, ProfileService profiles, CancellationToken ct) =>
            TypedResults.Ok(await profiles.RefreshAsync(id, force ?? true, ct))).RequirePrivilege(PartyResources.Parties, CatalogActions.Edit).WithName("RefreshPartyProfile");

        group.MapPut("/parties/{id:guid}/profile/{attribute}", async (Guid id, string attribute, SetProfileValueRequest request, ProfileService profiles, CancellationToken ct) =>
        {
            await profiles.SetAsync(id, attribute, request.Value, ct);
            return TypedResults.NoContent();
        }).RequirePrivilege(PartyResources.Parties, CatalogActions.Edit).WithName("SetPartyProfileValue");

        group.MapPost("/parties/{id:guid}/contexts/refresh", async (Guid id, PartiesDbContext db, ContextService contexts, CancellationToken ct) =>
        {
            var individual = await db.Parties.Include(p => p.Identifiers).SingleOrDefaultAsync(p => p.Id == id && p.Kind == PartyKind.Individual, ct)
                ?? throw new PartyNotFoundException($"Individual {id} does not exist.");
            var warnings = await contexts.RefreshIfDueAsync(individual, force: true, ct);
            return TypedResults.Ok(new ContextsResult(ContextService.Self(individual), await contexts.ListAsync(id, ct), individual.ContextsRefreshedAt, warnings));
        }).RequirePrivilege(PartyResources.Parties, CatalogActions.Edit).WithName("RefreshPartyContexts");

        // A person's links by national ID or iqama number: used where staff manage a portal account before or after its first sign-in.
        group.MapGet("/people/{number}/links", async (string number, PartyAdminService service, CancellationToken ct) =>
            TypedResults.Ok(await service.PersonLinksAsync(number, ct)))
            .RequirePrivilege(PartyResources.Parties, CatalogActions.View).WithName("GetPersonLinks");

        group.MapPost("/people/{number}/links", async (string number, LinkEstablishmentRequest request, PartyAdminService service, CancellationToken ct) =>
            TypedResults.Ok(new CreatedResponse(await service.LinkEstablishmentAsync(number, request, ct))))
            .RequirePrivilege(PartyResources.Parties, CatalogActions.Edit).WithName("LinkPersonToEstablishment");

        group.MapPost("/people/{number}/links/{relationshipId:guid}/end", async (string number, Guid relationshipId, PartyAdminService service, CancellationToken ct) =>
        {
            await service.EndPersonLinkAsync(number, relationshipId, ct);
            return TypedResults.NoContent();
        }).RequirePrivilege(PartyResources.Parties, CatalogActions.Edit).WithName("EndPersonLink");

        group.MapPost("/parties/{id:guid}/relationships", async (Guid id, AddRelationshipRequest request, PartyAdminService service, CancellationToken ct) =>
            TypedResults.Ok(new CreatedResponse(await service.AddRelationshipAsync(id, request, ct))))
            .RequirePrivilege(PartyResources.Parties, CatalogActions.Edit).WithName("AddPartyRelationship");

        group.MapPost("/parties/{id:guid}/relationships/{relationshipId:guid}/end", async (Guid id, Guid relationshipId, PartyAdminService service, CancellationToken ct) =>
        {
            await service.EndRelationshipAsync(id, relationshipId, ct);
            return TypedResults.NoContent();
        }).RequirePrivilege(PartyResources.Parties, CatalogActions.Edit).WithName("EndPartyRelationship");

        group.MapPost("/parties/{id:guid}/credentials", async (Guid id, AddCredentialRequest request, PartyAdminService service, CancellationToken ct) =>
            TypedResults.Ok(new CreatedResponse(await service.AddCredentialAsync(id, request, ct))))
            .RequirePrivilege(PartyResources.Parties, CatalogActions.Edit).WithName("AddPartyCredential");

        group.MapPost("/parties/{id:guid}/projects", async (Guid id, AddProjectRequest request, PartyAdminService service, CancellationToken ct) =>
            TypedResults.Ok(new CreatedResponse(await service.AddProjectAsync(id, request, ct))))
            .RequirePrivilege(PartyResources.Parties, CatalogActions.Edit).WithName("AddPartyProject");

        group.MapPost("/parties/{id:guid}/sanctions", async (Guid id, AddSanctionRequest request, PartyAdminService service, CancellationToken ct) =>
            TypedResults.Ok(new CreatedResponse(await service.AddSanctionAsync(id, request, ct))))
            .RequirePrivilege(PartyResources.Sanctions, CatalogActions.Create).WithName("AddPartySanction");

        group.MapPost("/parties/{id:guid}/sanctions/{sanctionId:guid}/lift", async (Guid id, Guid sanctionId, LiftSanctionRequest request, PartyAdminService service, CancellationToken ct) =>
        {
            await service.LiftSanctionAsync(id, sanctionId, request, ct);
            return TypedResults.NoContent();
        }).RequirePrivilege(PartyResources.Sanctions, CatalogActions.Edit).WithName("LiftPartySanction");
    }

    public static async ValueTask<object?> Errors(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (ChangeValidationException ex)
        {
            return Results.ValidationProblem(ex.Errors.ToDictionary(e => e.Key, e => e.Value), title: "The request is invalid.");
        }
        catch (PartyNotFoundException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (PartyConflictException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message);
        }
        catch (ActingPartyException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: ex.Message);
        }
        catch (IntegrationOperationNotFoundException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, detail: $"Configuration error: {ex.Message}");
        }
    }
}
