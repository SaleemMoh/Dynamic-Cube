using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Access;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.Expressions;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Lovs;

/// <summary>
/// Resolves a user's effective grant on an LOV: for each of the user's roles the screen-specific grant wins over the
/// global one; results are unioned across roles, and with the person's own grants on the screen (personal privileges,
/// ADR-0036). Users without any grant see everything unless the LOV requires a grant.
/// </summary>
public sealed class GrantResolver(AppsControlDbContext context, JsonLogicEvaluator evaluator, TimeProvider timeProvider)
{
    public async Task<GrantSet> ResolveAsync(Lov lov, AccessSnapshot access, string? resourceCode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lov);
        ArgumentNullException.ThrowIfNull(access);

        // Each held role's own grants, or those of the company where it is held and differs (ADR-0036).
        var companies = access.RoleSources.Select(s => s.CompanyCode).OfType<string>().Distinct().ToList();
        var grants = access.RoleIds.Count == 0
            ? []
            : (await context.LovGrants.AsNoTracking()
                .Where(g => g.LovId == lov.Id && access.RoleIds.Contains(g.RoleId) && (g.ResourceId == null || g.Resource!.Code == resourceCode)
                    && (g.CompanyCode == null || companies.Contains(g.CompanyCode)))
                .Select(g => new { g.RoleId, g.CompanyCode, g.GrantType, g.GrantedValues, g.Rule, g.DefaultValue, IsOverride = g.ResourceId != null })
                .ToListAsync(cancellationToken))
                .Where(g => access.RoleSources.Contains(new RoleSource(g.RoleId, g.CompanyCode)))
                .ToList();

        // Values given to the person alone on this screen (in their period) only add to what the roles give.
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var personal = access.User.UserId is not { } userId || resourceCode is null
            ? []
            : await context.UserPrivileges.AsNoTracking()
                .Where(p => p.UserId == userId && p.Resource!.Code == resourceCode
                    && (p.EffectiveFrom == null || p.EffectiveFrom <= today) && (p.EffectiveTo == null || p.EffectiveTo >= today))
                .SelectMany(p => p.Grants.Where(g => g.LovId == lov.Id))
                .Select(g => new { g.GrantType, g.GrantedValues })
                .ToListAsync(cancellationToken);

        if (grants.Count == 0 && (!lov.RequiresGrant || personal.Count == 0))
        {
            return lov.RequiresGrant ? GrantSet.Nothing : GrantSet.Everything;
        }

        var all = personal.Any(g => g.GrantType == GrantType.All);
        var values = new HashSet<string>(personal.SelectMany(g => g.GrantedValues), StringComparer.Ordinal);
        string? defaultValue = null;
        var userJson = access.User.ToJson();
        foreach (var grant in grants.GroupBy(g => (g.RoleId, g.CompanyCode)).Select(g => g.OrderByDescending(x => x.IsOverride).First()))
        {
            defaultValue ??= grant.DefaultValue;
            switch (grant.GrantType)
            {
                case GrantType.All:
                    all = true;
                    break;
                case GrantType.Values:
                    values.UnionWith(grant.GrantedValues);
                    break;
                case GrantType.Rule when grant.Rule is not null:
                    all |= AddRuleValues(evaluator.Evaluate(JsonNode.Parse(grant.Rule), userJson), values);
                    break;
            }
        }

        return all ? GrantSet.Everything with { DefaultValue = defaultValue } : new GrantSet(false, values, defaultValue);
    }

    /// <summary>Adds the values a rule returned; returns true when the rule granted everything.</summary>
    internal static bool AddRuleValues(JsonNode? result, HashSet<string> values)
    {
        switch (result)
        {
            case null:
                return false;
            case JsonArray array:
                foreach (var item in array)
                {
                    AddRuleValues(item, values);
                }

                return false;
            case JsonValue value when value.GetValueKind() == JsonValueKind.True:
                return true;
            case JsonValue value when value.GetValueKind() == JsonValueKind.False:
                return false;
            case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                values.Add(value.GetValue<string>());
                return false;
            case JsonValue value:
                values.Add(value.ToJsonString());
                return false;
            default:
                return false;
        }
    }
}
