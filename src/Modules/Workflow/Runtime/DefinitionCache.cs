using System.Collections.Concurrent;
using System.Text.Json;
using DynamicCube.Modules.Workflow.Definitions;
using DynamicCube.Modules.Workflow.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Workflow.Runtime;

/// <summary>A published version's graph with its nodes by code.</summary>
public sealed class ParsedVersion(Guid id, string workflowCode, int number, WorkflowDefinition definition)
{
    public Guid Id { get; } = id;

    public string WorkflowCode { get; } = workflowCode;

    public int Number { get; } = number;

    public WorkflowDefinition Definition { get; } = definition;

    public IReadOnlyDictionary<string, NodeDefinition> Nodes { get; } = definition.Nodes.ToDictionary(n => n.Code, StringComparer.Ordinal);

    public NodeDefinition Node(string code) =>
        Nodes.TryGetValue(code, out var node) ? node : throw new InvalidOperationException($"Node '{code}' is not in {WorkflowCode}/{Number}.");
}

/// <summary>Versions never change once published, so their parsed graphs are cached for the life of the process.</summary>
internal sealed class DefinitionCache
{
    private readonly ConcurrentDictionary<Guid, ParsedVersion> _versions = new();

    public async Task<ParsedVersion> GetAsync(WorkflowDbContext context, Guid versionId, CancellationToken cancellationToken)
    {
        if (_versions.TryGetValue(versionId, out var cached))
        {
            return cached;
        }

        var row = await context.Versions.AsNoTracking().Where(v => v.Id == versionId)
            .Select(v => new { v.Id, Workflow = v.Workflow!.Code, v.Number, v.Definition })
            .SingleAsync(cancellationToken);
        var parsed = new ParsedVersion(row.Id, row.Workflow, row.Number, JsonSerializer.Deserialize<WorkflowDefinition>(row.Definition, ChangeJson.Options)!);
        return _versions.GetOrAdd(versionId, parsed);
    }
}
