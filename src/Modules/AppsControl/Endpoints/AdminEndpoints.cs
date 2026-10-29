using System.Text.Json;
using DynamicCube.Modules.AppsControl.Access;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Persistence;
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

/// <summary>
/// One change of a package. <c>UPSERT</c>, or no operation, creates the row where it does not exist and updates it
/// where it does, so an exported package imports into any environment.
/// </summary>
public sealed record PackageItemRequest(string? EntityType, string? EntityCode, string? Operation, JsonElement? Payload);

/// <summary>A configuration package: changes of any modules, drafted as one change set per area.</summary>
public sealed record ImportPackageRequest(string Title, List<PackageItemRequest> Items);

/// <summary>
/// One item of a package as the dry run found it: the operation it resolves to, whether its row exists, the row as it
/// is (<see cref="Current"/>) and as the package would leave it (<see cref="Incoming"/>), and what is wrong with it.
/// Updates that change nothing are <see cref="Unchanged"/> and skipped by the import.
/// </summary>
public sealed record PackageItemCheck(
    int Index,
    string? EntityType,
    string? EntityCode,
    string? Operation,
    string? Area,
    bool Exists,
    JsonElement? Payload,
    IReadOnlyDictionary<string, string[]> Errors,
    bool Unchanged = false,
    JsonElement? Current = null,
    JsonElement? Incoming = null);

/// <summary>A package checked without saving anything: problems of the package itself and of each item.</summary>
public sealed record PackageCheck(string? Title, bool IsValid, IReadOnlyDictionary<string, string[]> Errors, IReadOnlyList<PackageItemCheck> Items);

/// <summary>Rows of one type to export: all of them, or the given codes.</summary>
public sealed record ExportSelection(string EntityType, List<string>? Codes);

/// <summary>
/// What to export, in apply order: put types before the types that refer to them. Rows maintained by the product
/// (owner SYSTEM) come with every installation and are left out unless asked for. Codes that do not exist are an error,
/// or skipped with <see cref="SkipMissing"/> (when following references that may point at nothing).
/// </summary>
public sealed record ExportRequest(string? Title, List<ExportSelection> Types, bool IncludeSystem = false, bool SkipMissing = false);

public sealed record ExportedItem(string EntityType, string EntityCode, string Operation, JsonElement Payload);

/// <summary>An exported configuration package: importable as is, in this or another environment.</summary>
public sealed record ExportedPackage(string Title, DateTimeOffset ExportedAt, IReadOnlyList<ExportedItem> Items);

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
    /// <summary>Most items a package may carry, whether exported, checked or imported.</summary>
    internal const int MaxPackageItems = 2000;

    private static readonly string[] SnapshotOnly = ["code", "owner"];

    public static void Map(RouteGroupBuilder group)
    {
        var admin = group.MapGroup("/admin").AddEndpointFilter(AppsControlFilters.Errors);
        BrandingEndpoints.MapAdmin(admin);
        AccountEndpoints.Map(admin);

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

        // How many rows hang under each row of a type, for the master lists: values per class, users per role, menus per group.
        admin.MapGet("/counts/{type}", async (string type, IEnumerable<IConfigApplier> appliers, AccessResolver resolver, AppsControlDbContext db, CancellationToken ct) =>
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

            Dictionary<string, int>? counts = type switch
            {
                AppsControlChangeTypes.Class => await db.Domains.GroupBy(d => d.Class!.Code).ToDictionaryAsync(g => g.Key, g => g.Count(), ct),
                AppsControlChangeTypes.Role => await db.UserRoles.GroupBy(r => r.Role!.Code).ToDictionaryAsync(g => g.Key, g => g.Select(r => r.UserId).Distinct().Count(), ct),
                AppsControlChangeTypes.MenuGroup => await db.Menus.Where(m => m.MenuGroup != null).GroupBy(m => m.MenuGroup!.Code).ToDictionaryAsync(g => g.Key, g => g.Count(), ct),
                _ => null,
            };
            return counts is null ? Results.NotFound() : Results.Ok(counts);
        }).WithName("CountConfigurationChildren");

        // Configuration packages out of an environment: rows as payloads keyed by code, ready to import elsewhere.
        admin.MapPost("/export", async (ExportRequest request, IEnumerable<IConfigApplier> appliers, AccessResolver resolver, TimeProvider clock, CancellationToken ct) =>
        {
            if (request.Types is not { Count: > 0 })
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["types"] = ["Choose at least one type."] });
            }

            var access = await resolver.GetAsync(ct);
            var items = new List<ExportedItem>();
            var seen = new HashSet<(string, string)>();
            var missing = new List<string>();
            foreach (var selection in request.Types)
            {
                var applier = Find(appliers, selection.EntityType ?? string.Empty);
                if (applier is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["types"] = [$"Unknown entity type '{selection.EntityType}'."] });
                }

                if (!access.Can(applier.ResourceCode, PrivilegeActions.View))
                {
                    return Forbidden(applier.ResourceCode, PrivilegeActions.View);
                }

                IEnumerable<JsonElement> rows;
                if (selection.Codes is null)
                {
                    rows = await applier.ExportAsync(ct);
                }
                else
                {
                    var picked = new List<JsonElement>();
                    foreach (var code in selection.Codes.Distinct(StringComparer.Ordinal))
                    {
                        if (await applier.GetAsync(code, ct) is { } row)
                        {
                            picked.Add(row);
                        }
                        else if (!request.SkipMissing)
                        {
                            missing.Add($"{applier.EntityType} {code}");
                        }
                    }

                    rows = picked;
                }

                foreach (var row in rows.Where(r => request.IncludeSystem || !IsSystem(r)))
                {
                    var code = row.TryGetProperty("code", out var c) ? c.GetString() ?? string.Empty : string.Empty;
                    if (seen.Add((applier.EntityType, code)))
                    {
                        items.Add(new ExportedItem(applier.EntityType, code, "UPSERT", Without(row, SnapshotOnly)));
                    }
                }
            }

            if (missing.Count > 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["codes"] = missing.Select(m => $"{m} does not exist.").ToArray() });
            }

            if (items.Count > MaxPackageItems)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["types"] = [$"{items.Count} rows; a package carries at most {MaxPackageItems}. Export fewer types."] });
            }

            var title = string.IsNullOrWhiteSpace(request.Title) ? "Exported configuration" : request.Title.Trim();
            return Results.Ok(new ExportedPackage(title, clock.GetUtcNow(), items));
        }).WithName("ExportConfiguration");

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

        // Configuration packages (e.g. a ready-made connector set): drafted per area, then submitted and approved as usual.
        sets.MapPost("/import", async (ImportPackageRequest request, ChangeSetRouter router, AccessResolver resolver, AppsControlDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title) || request.Items is not { Count: > 0 and <= MaxPackageItems })
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["items"] = [$"Give a title and 1 to {MaxPackageItems} items."] });
            }

            var prepared = new List<(IConfigApplier Applier, AddChangeItemRequest Item, string? Payload)>();
            foreach (var entry in request.Items)
            {
                var code = entry.EntityCode?.Trim() ?? string.Empty;
                var current = Find(router.Appliers, entry.EntityType ?? string.Empty) is { } target && code.Length > 0 ? await target.GetAsync(code, ct) : null;
                if (!TryResolveOperation(entry.Operation, current is not null, out var operation))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["operation"] = [$"{entry.EntityType} {code}: use CREATE, UPDATE, DELETE or UPSERT."] });
                }

                var item = new AddChangeItemRequest(entry.EntityType ?? string.Empty, code, operation, entry.Payload);
                var (applier, payload, problem) = await AuthorizeChangeAsync(item, router.Appliers, resolver, ct);
                if (problem is not null)
                {
                    return problem;
                }

                if (operation == ChangeOperation.Update && Unchanged(applier!, code, payload, current, out _))
                {
                    continue;
                }

                prepared.Add((applier!, item, payload));
            }

            var created = new List<(IChangeSetWorkflow Workflow, Guid Id)>();
            try
            {
                foreach (var group in prepared.GroupBy(p => p.Applier.Area))
                {
                    var workflow = router.ForArea(group.Key);
                    var changeSet = await workflow.CreateAsync(group.Key, request.Title.Trim(), ct);
                    created.Add((workflow, changeSet.Id));
                    foreach (var (applier, item, payload) in group)
                    {
                        await workflow.AddItemAsync(changeSet.Id, applier.EntityType, item.EntityCode?.Trim() ?? string.Empty, item.Operation, payload, ct);
                    }
                }
            }
            catch
            {
                // All or nothing: drop the drafts created so far.
                foreach (var (workflow, id) in created)
                {
                    await workflow.DiscardAsync(id, CancellationToken.None);
                }

                throw;
            }

            var details = new List<ChangeSetDetail>();
            foreach (var (workflow, id) in created)
            {
                details.Add(await DetailAsync(db, await workflow.GetAsync(id, ct), user.UserId, ct));
            }

            return Results.Ok(details);
        }).RequirePrivilege(AppsControlResources.ChangeSets, PrivilegeActions.Create).WithName("ImportConfigurationPackage");

        // Dry run of an import: every item is checked as the import would (references to earlier items included), then
        // nothing is kept. Accepts any JSON so the reviewer sees every problem, not just the first.
        sets.MapPost("/import/check", async (JsonElement package, ChangeSetRouter router, AccessResolver resolver, IUnitOfWork unitOfWork, CancellationToken ct) =>
            TypedResults.Ok(await CheckPackageAsync(package, router, resolver, unitOfWork, ct)))
            .RequirePrivilege(AppsControlResources.ChangeSets, PrivilegeActions.Create).WithName("CheckConfigurationPackage");

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
        var (applier, payload, error) = await CheckChangeAccessAsync(request.EntityType, request.Operation, request.Payload, appliers, resolver, ct);
        return error switch
        {
            null => (applier, payload, null),
            { Forbidden: true } => (applier, null, Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: error.Message)),
            _ => (null, null, Results.ValidationProblem(new Dictionary<string, string[]> { [error.Field] = [error.Message] })),
        };
    }

    private sealed record AccessError(string Field, string Message, bool Forbidden);

    /// <summary>The applier of an item and its payload as stored (explanations such as <c>_help</c> removed), or why it is refused.</summary>
    private static async Task<(IConfigApplier? Applier, string? Payload, AccessError? Error)> CheckChangeAccessAsync(
        string? entityType, ChangeOperation operation, JsonElement? body, IReadOnlyList<IConfigApplier> appliers, AccessResolver resolver, CancellationToken ct)
    {
        var applier = Find(appliers, entityType ?? string.Empty);
        if (applier is null)
        {
            return (null, null, new AccessError("entityType", $"Unknown entity type '{entityType}'.", false));
        }

        var access = await resolver.GetAsync(ct);
        var action = operation switch
        {
            ChangeOperation.Create => PrivilegeActions.Create,
            ChangeOperation.Delete => PrivilegeActions.Delete,
            _ => PrivilegeActions.Edit,
        };
        if (!access.Can(applier.ResourceCode, action))
        {
            return (applier, null, new AccessError("entityType", $"Requires {action} on {applier.ResourceCode}.", true));
        }

        var payload = body is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } p ? WithoutHelp(p) : null;
        if (applier.EntityType == AppsControlChangeTypes.Lov && payload is not null && UsesRestrictedSql(payload) && !access.Can(AppsControlResources.LovSql, PrivilegeActions.Edit))
        {
            return (applier, null, new AccessError("payload.sourceType", $"Requires {PrivilegeActions.Edit} on {AppsControlResources.LovSql}.", true));
        }

        return (applier, payload, null);
    }

    /// <summary>Templates explain each property under <c>_help</c>; explanations are not configuration.</summary>
    private static string WithoutHelp(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty("_help", out _))
        {
            return payload.GetRawText();
        }

        var node = System.Text.Json.Nodes.JsonObject.Create(payload)!;
        node.Remove("_help");
        return node.ToJsonString();
    }

    private static async Task<PackageCheck> CheckPackageAsync(JsonElement package, ChangeSetRouter router, AccessResolver resolver, IUnitOfWork unitOfWork, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (package.ValueKind != JsonValueKind.Object)
        {
            return new PackageCheck(null, false, new Dictionary<string, string[]> { ["package"] = ["A package is a JSON object with a title and items."] }, []);
        }

        var title = package.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
        if (string.IsNullOrWhiteSpace(title))
        {
            errors["title"] = ["Give the package a title."];
        }

        var items = package.TryGetProperty("items", out var i) && i.ValueKind == JsonValueKind.Array ? i.EnumerateArray().ToList() : null;
        if (items is not { Count: > 0 and <= MaxPackageItems })
        {
            errors["items"] = [$"Give 1 to {MaxPackageItems} items."];
            items ??= [];
        }

        var results = new List<PackageItemCheck>();
        await using (var work = await unitOfWork.BeginAsync(ct))
        {
            var drafts = new Dictionary<string, (IChangeSetWorkflow Workflow, Guid Id)>(StringComparer.Ordinal);
            for (var index = 0; index < Math.Min(items.Count, MaxPackageItems); index++)
            {
                results.Add(await CheckItemAsync(index, items[index], title ?? "check", drafts, router, resolver, ct));
            }

            // Nothing of the dry run is kept: the unit of work ends without a commit.
        }

        return new PackageCheck(title, errors.Count == 0 && results.All(r => r.Errors.Count == 0), errors, results);
    }

    private static async Task<PackageItemCheck> CheckItemAsync(
        int index, JsonElement item, string title, Dictionary<string, (IChangeSetWorkflow Workflow, Guid Id)> drafts, ChangeSetRouter router, AccessResolver resolver, CancellationToken ct)
    {
        string? Text(string name) => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        var entityType = Text("entityType");
        var entityCode = Text("entityCode")?.Trim();
        var operationText = Text("operation");
        JsonElement? payload = item.ValueKind == JsonValueKind.Object && item.TryGetProperty("payload", out var p) ? p.Clone() : null;
        JsonElement? current = null;
        JsonElement? incoming = null;
        PackageItemCheck Result(string? area, Dictionary<string, string[]> errors, bool unchanged = false) =>
            new(index, entityType, entityCode, operationText, area, current is not null, payload, errors, unchanged, current, incoming);

        if (item.ValueKind != JsonValueKind.Object)
        {
            return Result(null, new() { ["item"] = ["Each item is an object with entityType, entityCode, operation and payload."] });
        }

        var problems = new Dictionary<string, string[]>();
        if (string.IsNullOrEmpty(entityCode))
        {
            problems["entityCode"] = ["Required."];
        }

        var target = Find(router.Appliers, entityType ?? string.Empty);
        if (target is not null && !string.IsNullOrEmpty(entityCode))
        {
            current = await target.GetAsync(entityCode, ct);
        }

        if (TryResolveOperation(operationText, current is not null, out var operation))
        {
            operationText = operation.ToString().ToUpperInvariant();
        }
        else
        {
            problems["operation"] = ["Use CREATE, UPDATE, DELETE or UPSERT."];
        }

        var (applier, body, error) = await CheckChangeAccessAsync(entityType, operation, payload, router.Appliers, resolver, ct);
        if (error is not null)
        {
            problems[error.Field] = [error.Message];
        }

        if (applier is not null && operation != ChangeOperation.Delete && body is null)
        {
            problems["payload"] = ["Required for CREATE and UPDATE."];
        }

        var unchanged = applier is not null && !string.IsNullOrEmpty(entityCode) && Unchanged(applier, entityCode, body, current, out incoming)
            && operation == ChangeOperation.Update;
        if (problems.Count > 0 || applier is null)
        {
            return Result(applier?.Area, problems);
        }

        if (unchanged)
        {
            return Result(applier.Area, problems, unchanged: true);
        }

        if (!drafts.TryGetValue(applier.Area, out var draft))
        {
            var workflow = router.ForArea(applier.Area);
            draft = (workflow, (await workflow.CreateAsync(applier.Area, title, ct)).Id);
            drafts[applier.Area] = draft;
        }

        try
        {
            await draft.Workflow.AddItemAsync(draft.Id, applier.EntityType, entityCode!, operation, body, ct);
        }
        catch (ChangeValidationException ex)
        {
            return Result(applier.Area, ex.Errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
        }
        catch (ChangeSetException ex)
        {
            return Result(applier.Area, new() { ["item"] = [ex.Message] });
        }

        return Result(applier.Area, problems);
    }

    /// <summary>CREATE, UPDATE or DELETE as given; UPSERT, or no operation, by whether the row exists.</summary>
    private static bool TryResolveOperation(string? text, bool exists, out ChangeOperation operation)
    {
        if (string.IsNullOrWhiteSpace(text) || string.Equals(text, "UPSERT", StringComparison.OrdinalIgnoreCase))
        {
            operation = exists ? ChangeOperation.Update : ChangeOperation.Create;
            return true;
        }

        return Enum.TryParse(text, ignoreCase: true, out operation) && !int.TryParse(text, out _);
    }

    /// <summary>Whether an update would leave the row as it is: the payload, read as the type reads it, equals the row.</summary>
    private static bool Unchanged(IConfigApplier applier, string entityCode, string? payload, JsonElement? current, out JsonElement? incoming)
    {
        incoming = payload is null ? null : applier.Normalize(entityCode, payload);
        return incoming is { } next && current is { } now && JsonElement.DeepEquals(Without(next, SnapshotOnly), Without(now, SnapshotOnly));
    }

    private static bool IsSystem(JsonElement row) =>
        row.TryGetProperty("owner", out var owner) && owner.ValueKind == JsonValueKind.String && owner.GetString() == "SYSTEM";

    private static JsonElement Without(JsonElement row, string[] names)
    {
        if (row.ValueKind != JsonValueKind.Object || !names.Any(n => row.TryGetProperty(n, out _)))
        {
            return row;
        }

        var node = System.Text.Json.Nodes.JsonObject.Create(row)!;
        foreach (var name in names)
        {
            node.Remove(name);
        }

        return JsonSerializer.SerializeToElement(node);
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
