using System.Text.Json;
using DynamicCube.Modules.Workflow.Definitions;
using DynamicCube.Modules.Workflow.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Decisions;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Workflow.Runtime;

/// <summary>Published workflow versions whose decision steps use a rule set.</summary>
internal sealed class RuleSetUsage(WorkflowDbContext context) : IRuleSetUsage
{
    public async Task<IReadOnlyList<string>> WorkflowsUsingAsync(string ruleSetCode, CancellationToken cancellationToken)
    {
        var versions = await context.Versions.AsNoTracking()
            .Where(v => !v.IsRetired)
            .Select(v => new { v.Code, v.Definition })
            .ToListAsync(cancellationToken);
        return [.. versions
            .Where(v => JsonSerializer.Deserialize<WorkflowDefinition>(v.Definition, ChangeJson.Options)?.Nodes
                .Any(n => n.Type == NodeType.Decision && n.RuleSet == ruleSetCode) == true)
            .Select(v => v.Code)
            .Order(StringComparer.Ordinal)];
    }
}
