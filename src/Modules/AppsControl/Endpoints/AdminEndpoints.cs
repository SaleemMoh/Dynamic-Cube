using System.Text.Json;
using DynamicCube.Modules.AppsControl.Access;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Endpoints;

public sealed record EntityTypeInfo(string EntityType, string Area, string ResourceCode, IReadOnlyCollection<string> Actions, bool RequiresApproval);

public sealed record CreateChangeSetRequest(string Area, string Title);

public sealed record AddChangeItemRequest(string EntityType, string EntityCode, ChangeOperation Operation, JsonElement? Payload);

public sealed record ReviewRequest(string? Comment);

public sealed record ChangeSetSummary(
    Guid Id,
    string Area,
    string Title,
    ChangeSetStatus Status,
    bool RequiresApproval,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    string? SubmittedBy,
    DateTimeOffset? SubmittedAt,
    string? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? ReviewComment,
    string? PublishedBy,
    DateTimeOffset? PublishedAt,
    int ItemCount,
    bool IsMine);

public sealed record ChangeItemInfo(Guid Id, int Sequence, string EntityType, string EntityCode, ChangeOperation Operation, JsonElement? Before, JsonElement? After);

public sealed record ChangeSetDetail(ChangeSetSummary ChangeSet, IReadOnlyList<ChangeItemInfo> Items);

/// <summary>Configuration reads and change-set workflow (internal host only). Every edit goes through a change set.</summary>
internal static class AdminEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        var admin = group.MapGroup("/admin").AddEndpointFilter(AppsControlFilters.Errors);
        BrandingEndpoints.MapAdmin(admin);

        admin.MapGet("/types", async (IEnumerable<IConfigApplier> appliers, AccessResolver resolver, IChangeApprovalPolicy policy, CancellationToken ct) =>
        {
            var access = await resolver.GetAsync(ct);
            var approval = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var area in appliers.Select(a => a.Area).Distinct())
            {
                approval[area] = await policy.RequiresApprovalAsync(area, ct);
            }

            return TypedResults.Ok(appliers
                .Select(a => new EntityTypeInfo(
                    a.EntityType,
                    a.Area,
                    a.ResourceCode,
                    access.Privileges.TryGetValue(a.ResourceCode, out var actions) ? actions.Order().ToList() : [],
                    approval[a.Area]))
                .Where(t => t.Actions.Contains(PrivilegeActions.View))
                .OrderBy(t => t.EntityType)
                .ToList());
        }).WithName("GetAdminTypes");

        admin.MapGet("/{type}", async (string type, string? parent, IEnumerable<IConfigApplier> appliers, AccessResolver resolver, AppsControlDbContext db, CancellationToken ct) =>
        {
            var applier = Find(appliers, type);
            if (applier is null)
            {
                return Results.NotFound();
            }

            var access = await resolver.GetAsync(ct);
            return access.Can(applier.ResourceCode, PrivilegeActions.View)
                ? Results.Ok(await applier.ListAsync(parent, ct))
                : Forbidden(applier.ResourceCode, PrivilegeActions.View);
        }).WithName("ListConfiguration");

        admin.MapGet("/{type}/{**code}", async (string type, string code, IEnumerable<IConfigApplier> appliers, AccessResolver resolver, AppsControlDbContext db, CancellationToken ct) =>
        {
            var applier = Find(appliers, type);
            if (applier is null)
            {
                return Results.NotFound();
            }

            var access = await resolver.GetAsync(ct);
            if (!access.Can(applier.ResourceCode, PrivilegeActions.View))
            {
                return Forbidden(applier.ResourceCode, PrivilegeActions.View);
            }

            var row = await applier.GetAsync(code, ct);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("GetConfiguration");

        MapChangeSets(group.MapGroup("/change-sets").AddEndpointFilter(AppsControlFilters.Errors));
    }

    private static void MapChangeSets(RouteGroupBuilder sets)
    {
        sets.MapGet("/", async (string? status, string? area, bool? mine, AppsControlDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            var query = db.ChangeSets.AsNoTracking();
            if (status is not null)
            {
                // Statuses are written UPPER_SNAKE in the API (e.g. SUBMITTED).
                if (!Enum.TryParse<ChangeSetStatus>(status, ignoreCase: true, out var parsed))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = [$"Unknown status '{status}'."] });
                }

                query = query.Where(c => c.Status == parsed);
            }

            if (area is not null)
            {
                query = query.Where(c => c.Area == area);
            }

            if (mine == true)
            {
                query = query.Where(c => c.CreatedBy == user.UserId);
            }

            var rows = await query.OrderByDescending(c => c.CreatedAt).Take(200).Include(c => c.Items).ToListAsync(ct);
            var names = await UserNamesAsync(db, rows, ct);
            return Results.Ok(rows.Select(c => Summary(c, names, user.UserId)).ToList());
        }).RequirePrivilege(AppsControlResources.ChangeSets, PrivilegeActions.View).WithName("ListChangeSets");

        sets.MapPost("/", async (CreateChangeSetRequest request, ChangeSetRouter router, AppsControlDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            if (!router.IsKnownArea(request.Area))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["area"] = [$"Unknown area '{request.Area}'."] });
            }

            if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 500)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["title"] = ["Required, at most 500 characters."] });
            }

            var changeSet = await router.ForArea(request.Area).CreateAsync(request.Area, request.Title.Trim(), ct);
            return Results.Created($"/api/apps-control/v1/change-sets/{changeSet.Id}", await DetailAsync(db, changeSet, user.UserId, ct));
        }).RequirePrivilege(AppsControlResources.ChangeSets, PrivilegeActions.Create).WithName("CreateChangeSet");

        sets.MapGet("/{id:guid}", async (Guid id, ChangeSetRouter router, AppsControlDbContext db, ICurrentUser user, CancellationToken ct) =>
            TypedResults.Ok(await DetailAsync(db, await (await router.ForChangeSetAsync(id, ct)).GetAsync(id, ct), user.UserId, ct)))
            .RequirePrivilege(AppsControlResources.ChangeSets, PrivilegeActions.View).WithName("GetChangeSet");

        sets.MapPost("/{id:guid}/items", async (Guid id, AddChangeItemRequest request, ChangeSetRouter router, AccessResolver resolver, AppsControlDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            var service = await router.ForChangeSetAsync(id, ct);
            var appliers = router.Appliers;
            var changeSet = await RequireOwnAsync(service, id, user, ct);
            var (applier, payload, problem) = await AuthorizeChangeAsync(request, appliers, resolver, ct);
            if (problem is not null)
            {
                return problem;
            }

            if (applier!.Area != changeSet.Area)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["entityType"] = [$"{request.EntityType} belongs to area {applier.Area}, not {changeSet.Area}."] });
            }

            await service.AddItemAsync(id, applier.EntityType, request.EntityCode?.Trim() ?? string.Empty, request.Operation, payload, ct);
            return Results.Ok(await DetailAsync(db, await service.GetAsync(id, ct), user.UserId, ct));
        }).RequirePrivilege(AppsControlResources.ChangeSets, PrivilegeActions.Create).WithName("AddChangeItem");

        // Areas without approval: one change, saved and published at once (still recorded with before/after).
        sets.MapPost("/apply", async (AddChangeItemRequest request, ChangeSetRouter router, AccessResolver resolver, AppsControlDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            var (applier, payload, problem) = await AuthorizeChangeAsync(request, router.Appliers, resolver, ct);
            if (problem is not null)
            {
                return problem;
            }

            var service = router.ForEntityType(applier!.EntityType);

            var code = request.EntityCode?.Trim() ?? string.Empty;
            var title = $"{request.Operation.ToString().ToUpperInvariant()} {applier!.EntityType} {code}";
            var changeSet = await service.SaveAndPublishAsync(applier.Area, title, applier.EntityType, code, request.Operation, payload, ct);
            return Results.Ok(await DetailAsync(db, await service.GetAsync(changeSet.Id, ct), user.UserId, ct));
        }).RequirePrivilege(AppsControlResources.ChangeSets, PrivilegeActions.Create).WithName("ApplyChange");

        sets.MapDelete("/{id:guid}/items/{itemId:guid}", async (Guid id, Guid itemId, ChangeSetRouter router, AppsControlDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            var service = await router.ForChangeSetAsync(id, ct);
            await RequireOwnAsync(service, id, user, ct);
            await service.RemoveItemAsync(id, itemId, ct);
            return TypedResults.Ok(await DetailAsync(db, await service.GetAsync(id, ct), user.UserId, ct));
        }).RequirePrivilege(AppsControlResources.ChangeSets, PrivilegeActions.Create).WithName("RemoveChangeItem");

        sets.MapPost("/{id:guid}/submit", async (Guid id, ChangeSetRouter router, AppsControlDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            var service = await router.ForChangeSetAsync(id, ct);
            await RequireOwnAsync(service, id, user, ct);
            await service.SubmitAsync(id, ct);
            var changeSet = await service.GetAsync(id, ct);
            if (changeSet.Status == ChangeSetStatus.Approved)
            {
                // No approval needed for this area: publish right away.
                await service.PublishAsync(id, ct);
            }

            return TypedResults.Ok(await DetailAsync(db, await service.GetAsync(id, ct), user.UserId, ct));
        }).RequirePrivilege(AppsControlResources.ChangeSets, PrivilegeActions.Create).WithName("SubmitChangeSet");

        sets.MapPost("/{id:guid}/approve", async (Guid id, ReviewRequest? request, ChangeSetRouter router, AppsControlDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            var service = await router.ForChangeSetAsync(id, ct);
            await service.ApproveAsync(id, request?.Comment, ct);
            await service.PublishAsync(id, ct);
            return TypedResults.Ok(await DetailAsync(db, await service.GetAsync(id, ct), user.UserId, ct));
        }).RequirePrivilege(AppsControlResources.ChangeSets, PrivilegeActions.Approve).WithName("ApproveChangeSet");

        sets.MapPost("/{id:guid}/reject", async (Guid id, ReviewRequest request, ChangeSetRouter router, AppsControlDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            var service = await router.ForChangeSetAsync(id, ct);
            if (string.IsNullOrWhiteSpace(request.Comment))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["comment"] = ["Explain why the change set is rejected."] });
            }

            await service.RejectAsync(id, request.Comment, ct);
            return Results.Ok(await DetailAsync(db, await service.GetAsync(id, ct), user.UserId, ct));
        }).RequirePrivilege(AppsControlResources.ChangeSets, PrivilegeActions.Approve).WithName("RejectChangeSet");

        sets.MapPost("/{id:guid}/reopen", async (Guid id, ChangeSetRouter router, AppsControlDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            var service = await router.ForChangeSetAsync(id, ct);
            await RequireOwnAsync(service, id, user, ct);
            await service.ReopenAsync(id, ct);
            return TypedResults.Ok(await DetailAsync(db, await service.GetAsync(id, ct), user.UserId, ct));
        }).RequirePrivilege(AppsControlResources.ChangeSets, PrivilegeActions.Create).WithName("ReopenChangeSet");

        sets.MapPost("/{id:guid}/publish", async (Guid id, ChangeSetRouter router, AccessResolver resolver, AppsControlDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            var service = await router.ForChangeSetAsync(id, ct);
            // Retry for an approved change set whose publication failed: its maker or a checker may retry.
            var changeSet = await service.GetAsync(id, ct);
            var access = await resolver.GetAsync(ct);
            if (changeSet.CreatedBy != user.UserId && !access.Can(AppsControlResources.ChangeSets, PrivilegeActions.Approve))
            {
                return Forbidden(AppsControlResources.ChangeSets, PrivilegeActions.Approve);
            }

            await service.PublishAsync(id, ct);
            return Results.Ok(await DetailAsync(db, await service.GetAsync(id, ct), user.UserId, ct));
        }).RequirePrivilege(AppsControlResources.ChangeSets, PrivilegeActions.View).WithName("PublishChangeSet");

        sets.MapDelete("/{id:guid}", async (Guid id, ChangeSetRouter router, ICurrentUser user, CancellationToken ct) =>
        {
            var service = await router.ForChangeSetAsync(id, ct);
            await RequireOwnAsync(service, id, user, ct);
            await service.DiscardAsync(id, ct);
            return TypedResults.NoContent();
        }).RequirePrivilege(AppsControlResources.ChangeSets, PrivilegeActions.Create).WithName("DiscardChangeSet");
    }

    /// <summary>Checks the entity type and the user's privilege for the operation (and restricted SQL); returns the raw payload.</summary>
    private static async Task<(IConfigApplier? Applier, string? Payload, IResult? Problem)> AuthorizeChangeAsync(
        AddChangeItemRequest request, IReadOnlyList<IConfigApplier> appliers, AccessResolver resolver, CancellationToken ct)
    {
        var applier = Find(appliers, request.EntityType);
        if (applier is null)
        {
            return (null, null, Results.ValidationProblem(new Dictionary<string, string[]> { ["entityType"] = [$"Unknown entity type '{request.EntityType}'."] }));
        }

        var access = await resolver.GetAsync(ct);
        var action = request.Operation switch
        {
            ChangeOperation.Create => PrivilegeActions.Create,
            ChangeOperation.Delete => PrivilegeActions.Delete,
            _ => PrivilegeActions.Edit,
        };
        if (!access.Can(applier.ResourceCode, action))
        {
            return (applier, null, Forbidden(applier.ResourceCode, action));
        }

        var payload = request.Payload is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } p ? p.GetRawText() : null;
        if (applier.EntityType == AppsControlChangeTypes.Lov && payload is not null && UsesRestrictedSql(payload) && !access.Can(AppsControlResources.LovSql, PrivilegeActions.Edit))
        {
            return (applier, null, Forbidden(AppsControlResources.LovSql, PrivilegeActions.Edit));
        }

        return (applier, payload, null);
    }

    private static IConfigApplier? Find(IEnumerable<IConfigApplier> appliers, string type) =>
        appliers.FirstOrDefault(a => string.Equals(a.EntityType, type, StringComparison.OrdinalIgnoreCase));

    private static IResult Forbidden(string resource, string action) =>
        Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: $"Requires {action} on {resource}.");

    private static bool UsesRestrictedSql(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("sourceType", out var source)
            && string.Equals(source.GetString(), "SQL", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<ChangeSet> RequireOwnAsync(IChangeSetWorkflow service, Guid id, ICurrentUser user, CancellationToken ct)
    {
        var changeSet = await service.GetAsync(id, ct);
        return changeSet.CreatedBy == user.UserId
            ? changeSet
            : throw new ChangeSetException("Only the maker of a change set can edit, submit, reopen or discard it.");
    }

    private static async Task<ChangeSetDetail> DetailAsync(AppsControlDbContext db, ChangeSet changeSet, string userId, CancellationToken ct)
    {
        var names = await UserNamesAsync(db, [changeSet], ct);
        return new ChangeSetDetail(
            Summary(changeSet, names, userId),
            changeSet.Items.OrderBy(i => i.Sequence)
                .Select(i => new ChangeItemInfo(i.Id, i.Sequence, i.EntityType, i.EntityCode, i.Operation, ToElement(i.Before), ToElement(i.After)))
                .ToList());
    }

    private static ChangeSetSummary Summary(ChangeSet c, IReadOnlyDictionary<string, string> names, string userId)
    {
        string? Name(string? subject) => subject is null ? null : names.GetValueOrDefault(subject, subject);
        return new ChangeSetSummary(
            c.Id, c.Area, c.Title, c.Status, c.RequiresApproval,
            Name(c.CreatedBy)!, c.CreatedAt, Name(c.SubmittedBy), c.SubmittedAt, Name(c.ReviewedBy), c.ReviewedAt, c.ReviewComment,
            Name(c.PublishedBy), c.PublishedAt, c.Items.Count, c.CreatedBy == userId);
    }

    /// <summary>Change sets store identity-provider subjects; show usernames instead.</summary>
    private static async Task<IReadOnlyDictionary<string, string>> UserNamesAsync(AppsControlDbContext db, IEnumerable<ChangeSet> changeSets, CancellationToken ct)
    {
        var subjects = changeSets.SelectMany(c => new[] { c.CreatedBy, c.SubmittedBy, c.ReviewedBy, c.PublishedBy }).OfType<string>().Distinct().ToList();
        return await db.Users.AsNoTracking()
            .Where(u => u.IdpSubject != null && subjects.Contains(u.IdpSubject))
            .ToDictionaryAsync(u => u.IdpSubject!, u => u.Code, ct);
    }

    private static JsonElement? ToElement(string? json) => json is null ? null : JsonSerializer.Deserialize<JsonElement>(json);
}
