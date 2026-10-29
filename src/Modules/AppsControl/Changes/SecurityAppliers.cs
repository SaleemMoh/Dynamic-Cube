using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.Expressions;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Changes;

public sealed class ResourcePayload : ConfigPayload
{
    public string? AppCode { get; set; }

    public ResourceType ResourceType { get; set; } = ResourceType.Screen;

    public string? ParentCode { get; set; }

    public List<string> AvailableActions { get; set; } = [PrivilegeActions.View];
}

internal sealed class ResourceApplier(AppsControlDbContext db) : ConfigApplier<Resource, ResourcePayload>(db)
{
    public override string EntityType => AppsControlChangeTypes.Resource;

    public override string Area => AppsControlAreas.Security;

    public override string ResourceCode => AppsControlResources.Resources;

    protected override IQueryable<Resource> Query(AppsControlDbContext context) =>
        context.Resources.Include(r => r.App).Include(r => r.Parent);

    protected override IQueryable<Resource> ListQuery(AppsControlDbContext context, string? parent) =>
        (parent is null ? Query(context) : Query(context).Where(r => r.App!.Code == parent)).OrderBy(r => r.Code);

    protected override Guid? ParentOf(Resource entity) => entity.ParentId;

    protected override async Task ApplyAsync(Resource entity, ResourcePayload payload, RefResolver refs, ValidationErrors errors, Resource? existing, CancellationToken cancellationToken)
    {
        var appId = await refs.AppAsync(payload.AppCode, "appCode", errors, cancellationToken);
        var parentId = payload.ParentCode == entity.Code ? null : await refs.ResourceAsync(payload.ParentCode, "parentCode", errors, cancellationToken);
        if (payload.ParentCode is not null && payload.ParentCode == entity.Code)
        {
            errors.Add("parentCode", "A resource cannot be its own parent.");
        }

        var actions = payload.AvailableActions.Select(a => a.Trim().ToUpperInvariant()).Distinct().ToList();
        if (actions.Count == 0)
        {
            errors.Add("availableActions", "At least one action is required.");
        }

        foreach (var action in actions.Where(a => !ValidationErrors.IdentifierCode().IsMatch(a)))
        {
            errors.Add("availableActions", $"'{action}' is not a valid action code.");
        }

        entity.AppId = appId;
        entity.ResourceType = payload.ResourceType;
        entity.ParentId = parentId;
        entity.AvailableActions = actions;
    }

    protected override Task<ResourcePayload> ToPayloadAsync(AppsControlDbContext context, Resource entity, CancellationToken cancellationToken) =>
        Task.FromResult(new ResourcePayload
        {
            AppCode = entity.App?.Code,
            ResourceType = entity.ResourceType,
            ParentCode = entity.Parent?.Code,
            AvailableActions = entity.AvailableActions,
        });
}

public sealed class PrivilegePayload
{
    public string ResourceCode { get; set; } = string.Empty;

    public List<string> Actions { get; set; } = [];

    /// <summary>Field code → <c>HIDDEN</c> | <c>READONLY</c>.</summary>
    public Dictionary<string, string>? FieldRules { get; set; }
}

public sealed class GrantPayload
{
    public string LovCode { get; set; } = string.Empty;

    /// <summary>Null for the role's global grant on the LOV; set to override it on one screen.</summary>
    public string? ResourceCode { get; set; }

    public GrantType GrantType { get; set; }

    public List<string> GrantedValues { get; set; } = [];

    public JsonNode? Rule { get; set; }

    public string? DefaultValue { get; set; }
}

public sealed class RolePayload : ConfigPayload
{
    public string? AppCode { get; set; }

    public string? ReferenceCode { get; set; }

    public List<PrivilegePayload> Privileges { get; set; } = [];

    public List<GrantPayload> Grants { get; set; } = [];

    /// <summary>Codes of the menu items the role shows (each opens its screen for the role).</summary>
    public List<string> Menus { get; set; } = [];
}

internal sealed class RoleApplier(AppsControlDbContext db, JsonLogicEvaluator evaluator) : ConfigApplier<Role, RolePayload>(db)
{
    private static readonly string[] FieldRuleValues = ["HIDDEN", "READONLY"];

    public override string EntityType => AppsControlChangeTypes.Role;

    public override string Area => AppsControlAreas.Security;

    public override string ResourceCode => AppsControlResources.Roles;

    protected override IQueryable<Role> Query(AppsControlDbContext context) =>
        context.Roles
            .Include(r => r.App)
            .Include(r => r.Privileges).ThenInclude(p => p.Resource)
            .Include(r => r.Grants).ThenInclude(g => g.Lov)
            .Include(r => r.Grants).ThenInclude(g => g.Resource)
            .Include(r => r.Menus).ThenInclude(m => m.Menu)
            .AsSplitQuery();

    protected override IQueryable<Role> ListQuery(AppsControlDbContext context, string? parent) =>
        context.Roles.Include(r => r.App).OrderBy(r => r.Code);

    protected override async Task ApplyAsync(Role entity, RolePayload payload, RefResolver refs, ValidationErrors errors, Role? existing, CancellationToken cancellationToken)
    {
        entity.AppId = await refs.AppAsync(payload.AppCode, "appCode", errors, cancellationToken);
        entity.ReferenceCode = payload.ReferenceCode;

        ChildSync.RequireUnique(payload.Privileges, p => p.ResourceCode, "privileges", errors);
        var privileges = new List<Privilege>();
        foreach (var (p, i) in payload.Privileges.Select((p, i) => (p, i)))
        {
            var field = $"privileges[{i}]";
            var resourceId = await refs.ResourceAsync(p.ResourceCode, field + ".resourceCode", errors, cancellationToken);
            var actions = p.Actions.Select(a => a.Trim().ToUpperInvariant()).Distinct().ToList();
            if (resourceId is { } rid && rid != Guid.Empty)
            {
                var available = await refs.Context.Resources.Where(r => r.Id == rid).Select(r => r.AvailableActions).SingleAsync(cancellationToken);
                foreach (var action in actions.Except(available))
                {
                    errors.Add(field + ".actions", $"'{action}' is not available on resource {p.ResourceCode}.");
                }
            }

            foreach (var rule in (p.FieldRules ?? []).Where(r => !FieldRuleValues.Contains(r.Value)))
            {
                errors.Add(field + ".fieldRules", $"'{rule.Value}' for field {rule.Key}: use HIDDEN or READONLY.");
            }

            privileges.Add(new Privilege
            {
                RoleId = entity.Id,
                ResourceId = resourceId ?? Guid.Empty,
                Actions = actions,
                FieldRules = p.FieldRules is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(p.FieldRules) : null,
            });
        }

        ChildSync.RequireUnique(payload.Grants, g => (g.LovCode, g.ResourceCode), "grants", errors);
        var grants = new List<LovGrant>();
        foreach (var (g, i) in payload.Grants.Select((g, i) => (g, i)))
        {
            var field = $"grants[{i}]";
            var lovId = await refs.LovAsync(g.LovCode, field + ".lovCode", errors, cancellationToken);
            var resourceId = await refs.ResourceAsync(g.ResourceCode, field + ".resourceCode", errors, cancellationToken);
            switch (g.GrantType)
            {
                case GrantType.Values when g.GrantedValues.Count == 0:
                    errors.Add(field + ".grantedValues", "List at least one value.");
                    break;
                case GrantType.Rule when g.Rule is null:
                    errors.Add(field + ".rule", "Required for a RULE grant.");
                    break;
                case GrantType.Rule:
                    ValidateRule(g.Rule, field + ".rule", errors);
                    break;
            }

            grants.Add(new LovGrant
            {
                RoleId = entity.Id,
                LovId = lovId ?? Guid.Empty,
                ResourceId = resourceId,
                GrantType = g.GrantType,
                GrantedValues = g.GrantType == GrantType.Values ? g.GrantedValues.Distinct().ToList() : [],
                Rule = g.GrantType == GrantType.Rule ? g.Rule?.ToJsonString() : null,
                DefaultValue = g.DefaultValue,
            });
        }

        ChildSync.RequireUnique(payload.Menus, m => m, "menus", errors);
        var menus = new List<RoleMenu>();
        foreach (var (code, i) in payload.Menus.Select((m, i) => (m, i)))
        {
            menus.Add(new RoleMenu { RoleId = entity.Id, MenuId = await refs.MenuAsync(code, $"menus[{i}]", errors, cancellationToken) ?? Guid.Empty });
        }

        if (errors.HasErrors || refs.IsValidating)
        {
            return;
        }

        ChildSync.Sync(entity.Menus, menus, m => m.MenuId, (_, _) => { });
        ChildSync.Sync(entity.Privileges, privileges, p => p.ResourceId, (from, to) =>
        {
            to.Actions = from.Actions;
            to.FieldRules = from.FieldRules;
        });
        ChildSync.Sync(entity.Grants, grants, g => (g.LovId, g.ResourceId), (from, to) =>
        {
            to.GrantType = from.GrantType;
            to.GrantedValues = from.GrantedValues;
            to.Rule = from.Rule;
            to.DefaultValue = from.DefaultValue;
        });
    }

    protected override Task<RolePayload> ToPayloadAsync(AppsControlDbContext context, Role entity, CancellationToken cancellationToken) =>
        Task.FromResult(new RolePayload
        {
            AppCode = entity.App?.Code,
            ReferenceCode = entity.ReferenceCode,
            Privileges = entity.Privileges
                .OrderBy(p => p.Resource?.Code)
                .Select(p => new PrivilegePayload
                {
                    ResourceCode = p.Resource?.Code ?? string.Empty,
                    Actions = p.Actions,
                    FieldRules = p.FieldRules is null ? null : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(p.FieldRules),
                })
                .ToList(),
            Menus = [.. entity.Menus.Select(m => m.Menu?.Code ?? string.Empty).Order(StringComparer.Ordinal)],
            Grants = entity.Grants
                .OrderBy(g => g.Lov?.Code).ThenBy(g => g.Resource?.Code)
                .Select(g => new GrantPayload
                {
                    LovCode = g.Lov?.Code ?? string.Empty,
                    ResourceCode = g.Resource?.Code,
                    GrantType = g.GrantType,
                    GrantedValues = g.GrantedValues,
                    Rule = JsonNodeExtensions.ParseOrNull(g.Rule),
                    DefaultValue = g.DefaultValue,
                })
                .ToList(),
        });

    protected override async Task BeforeDeleteAsync(AppsControlDbContext context, Role entity, CancellationToken cancellationToken)
    {
        if (await context.UserRoles.AnyAsync(r => r.RoleId == entity.Id, cancellationToken))
        {
            throw new DynamicCube.Platform.ChangeSets.ChangeSetException($"Role '{entity.Code}' is still assigned to users.");
        }
    }

    private void ValidateRule(JsonNode rule, string field, ValidationErrors errors)
    {
        try
        {
            evaluator.Evaluate(rule, Access.UserContext.Sample().ToJson());
        }
        catch (JsonLogicException ex)
        {
            errors.Add(field, ex.Message);
        }
    }
}

public sealed class AssignmentPayload
{
    public string OrgUnitCode { get; set; } = string.Empty;

    public string? PositionCode { get; set; }

    public bool IsPrimary { get; set; }

    public DateOnly? EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }
}

public sealed class UserRolePayload
{
    public string RoleCode { get; set; } = string.Empty;

    /// <summary>When set, the role is held only within this org unit (and its sub-units).</summary>
    public string? OrgUnitCode { get; set; }

    public DateOnly? EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }
}

/// <summary>An employee account. The entity code is the username; sign-in links the identity-provider subject.</summary>
public sealed class UserPayload : ConfigPayload
{
    public string? Email { get; set; }

    public string? Mobile { get; set; }

    public string? NationalId { get; set; }

    public UserStatus Status { get; set; } = UserStatus.Active;

    public string? PreferredLanguage { get; set; }

    public List<AssignmentPayload> Assignments { get; set; } = [];

    public List<UserRolePayload> Roles { get; set; } = [];
}

internal sealed class UserApplier(AppsControlDbContext db) : ConfigApplier<UserAccount, UserPayload>(db)
{
    public override string EntityType => AppsControlChangeTypes.User;

    public override string Area => AppsControlAreas.Security;

    public override string ResourceCode => AppsControlResources.Users;

    protected override System.Text.RegularExpressions.Regex CodePattern => ValidationErrors.UserName();

    protected override IQueryable<UserAccount> Query(AppsControlDbContext context) =>
        context.Users
            .Include(u => u.Assignments).ThenInclude(a => a.OrgUnit)
            .Include(u => u.Assignments).ThenInclude(a => a.Position)
            .Include(u => u.Roles).ThenInclude(r => r.Role)
            .Include(u => u.Roles).ThenInclude(r => r.OrgUnit)
            .AsSplitQuery();

    /// <summary>A user is listed under the business app they belong to, or the apps of their roles.</summary>
    protected override bool InApp(UserAccount entity, string businessApp) =>
        entity.BusinessApp == businessApp || entity.Roles.Any(r => r.Role?.BusinessApp == businessApp)
        || (entity.BusinessApp is null && entity.Roles.All(r => r.Role?.BusinessApp is null));

    /// <summary>With roles and placements: the org structure lists a unit's employees from it.</summary>
    protected override IQueryable<UserAccount> ListQuery(AppsControlDbContext context, string? parent) =>
        Query(context).OrderBy(u => u.Code);

    protected override async Task ApplyAsync(UserAccount entity, UserPayload payload, RefResolver refs, ValidationErrors errors, UserAccount? existing, CancellationToken cancellationToken)
    {
        errors.MaxLength("email", payload.Email, 256);
        errors.MaxLength("mobile", payload.Mobile, 32);
        errors.MaxLength("nationalId", payload.NationalId, 32);
        if (payload.PreferredLanguage is not (null or "ar" or "en"))
        {
            errors.Add("preferredLanguage", "Use 'ar' or 'en'.");
        }

        entity.Email = payload.Email;
        entity.Mobile = payload.Mobile;
        entity.NationalId = payload.NationalId;
        entity.Status = payload.Status;
        entity.PreferredLanguage = payload.PreferredLanguage;

        if (payload.Assignments.Count(a => a.IsPrimary) > 1)
        {
            errors.Add("assignments", "Only one assignment can be primary.");
        }

        ChildSync.RequireUnique(payload.Assignments, a => a.OrgUnitCode, "assignments", errors);
        var assignments = new List<UserAssignment>();
        foreach (var (a, i) in payload.Assignments.Select((a, i) => (a, i)))
        {
            var orgUnitId = await refs.OrgUnitAsync(a.OrgUnitCode, $"assignments[{i}].orgUnitCode", errors, cancellationToken);
            if (string.IsNullOrWhiteSpace(a.OrgUnitCode))
            {
                errors.Add($"assignments[{i}].orgUnitCode", "Required.");
            }

            var positionId = await refs.PositionAsync(a.PositionCode, $"assignments[{i}].positionCode", errors, cancellationToken);
            assignments.Add(new UserAssignment
            {
                UserId = entity.Id,
                OrgUnitId = orgUnitId ?? Guid.Empty,
                PositionId = positionId,
                IsPrimary = a.IsPrimary || payload.Assignments.Count == 1,
                EffectiveFrom = a.EffectiveFrom,
                EffectiveTo = a.EffectiveTo,
            });
        }

        ChildSync.RequireUnique(payload.Roles, r => (r.RoleCode, r.OrgUnitCode), "roles", errors);
        var roles = new List<UserRole>();
        foreach (var (r, i) in payload.Roles.Select((r, i) => (r, i)))
        {
            if (string.IsNullOrWhiteSpace(r.RoleCode))
            {
                errors.Add($"roles[{i}].roleCode", "Required.");
            }

            var roleId = await refs.RoleAsync(r.RoleCode, $"roles[{i}].roleCode", errors, cancellationToken);
            var orgUnitId = await refs.OrgUnitAsync(r.OrgUnitCode, $"roles[{i}].orgUnitCode", errors, cancellationToken);
            roles.Add(new UserRole
            {
                UserId = entity.Id,
                RoleId = roleId ?? Guid.Empty,
                OrgUnitId = orgUnitId,
                EffectiveFrom = r.EffectiveFrom,
                EffectiveTo = r.EffectiveTo,
            });
        }

        if (errors.HasErrors || refs.IsValidating)
        {
            return;
        }

        ChildSync.Sync(entity.Assignments, assignments, a => a.OrgUnitId, (from, to) =>
        {
            to.PositionId = from.PositionId;
            to.IsPrimary = from.IsPrimary;
            to.EffectiveFrom = from.EffectiveFrom;
            to.EffectiveTo = from.EffectiveTo;
        });
        ChildSync.Sync(entity.Roles, roles, r => (r.RoleId, r.OrgUnitId), (from, to) =>
        {
            to.EffectiveFrom = from.EffectiveFrom;
            to.EffectiveTo = from.EffectiveTo;
        });
    }

    protected override Task<UserPayload> ToPayloadAsync(AppsControlDbContext context, UserAccount entity, CancellationToken cancellationToken) =>
        Task.FromResult(new UserPayload
        {
            Email = entity.Email,
            Mobile = entity.Mobile,
            NationalId = entity.NationalId,
            Status = entity.Status,
            PreferredLanguage = entity.PreferredLanguage,
            Assignments = entity.Assignments
                .OrderByDescending(a => a.IsPrimary).ThenBy(a => a.OrgUnit?.Code)
                .Select(a => new AssignmentPayload
                {
                    OrgUnitCode = a.OrgUnit?.Code ?? string.Empty,
                    PositionCode = a.Position?.Code,
                    IsPrimary = a.IsPrimary,
                    EffectiveFrom = a.EffectiveFrom,
                    EffectiveTo = a.EffectiveTo,
                })
                .ToList(),
            Roles = entity.Roles
                .OrderBy(r => r.Role?.Code)
                .Select(r => new UserRolePayload
                {
                    RoleCode = r.Role?.Code ?? string.Empty,
                    OrgUnitCode = r.OrgUnit?.Code,
                    EffectiveFrom = r.EffectiveFrom,
                    EffectiveTo = r.EffectiveTo,
                })
                .ToList(),
        });
}
