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
/// global one; results are unioned across roles. Users without any grant see everything unless the LOV requires a grant.
/// </summary>
public sealed class GrantResolver(AppsControlDbContext context, JsonLogicEvaluator evaluator)
{
    public async Task<GrantSet> ResolveAsync(Lov lov, AccessSnapshot access, string? resourceCode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lov);
        ArgumentNullException.ThrowIfNull(access);

        var grants = access.RoleIds.Count == 0
            ? []
            : await context.LovGrants.AsNoTracking()
                .Where(g => g.LovId == lov.Id && access.RoleIds.Contains(g.RoleId) && (g.ResourceId == null || g.Resource!.Code == resourceCode))
                .Select(g => new { g.RoleId, g.GrantType, g.GrantedValues, g.Rule, g.DefaultValue, IsOverride = g.ResourceId != null })
                .ToListAsync(cancellationToken);

        if (grants.Count == 0)
        {
            return lov.RequiresGrant ? GrantSet.Nothing : GrantSet.Everything;
        }

        var all = false;
        var values = new HashSet<string>(StringComparer.Ordinal);
        string? defaultValue = null;
        var userJson = access.User.ToJson();
        foreach (var grant in grants.GroupBy(g => g.RoleId).Select(g => g.OrderByDescending(x => x.IsOverride).First()))
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
