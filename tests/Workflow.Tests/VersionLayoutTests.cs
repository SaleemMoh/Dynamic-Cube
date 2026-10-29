using System.Text.Json.Nodes;
using DynamicCube.Modules.Workflow.Changes;
using DynamicCube.Modules.Workflow.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using static DynamicCube.Modules.Workflow.Tests.WorkflowHost;

namespace DynamicCube.Modules.Workflow.Tests;

/// <summary>What the designer keeps in a version besides the graph, and versions published before a field existed.</summary>
public sealed class VersionLayoutTests(DatabaseFixture fixture)
{
    private static readonly object[] Graph =
    [
        Start("REVIEW"),
        Human("REVIEW", Role("REVIEWER"), [Act("APPROVE", "DONE", "APPROVE")]),
        End("DONE", "APPROVED"),
    ];

    [Fact]
    public async Task A_version_keeps_where_the_designer_drew_its_nodes()
    {
        await using var host = new WorkflowHost(fixture);
        var service = await host.ServiceAsync();
        var layout = new Dictionary<string, object> { ["START"] = new { x = 40, y = 20 }, ["REVIEW"] = new { x = 40.5, y = 140 }, ["DONE"] = new { x = 40, y = 260 } };
        await host.Host.PublishAsync(WorkflowChangeTypes.Area, Version(service, 2, new { nodes = Graph, layout }));

        var stored = await host.Host.AsAsync("admin", sp => sp.GetRequiredService<WorkflowDbContext>().Versions.AsNoTracking().SingleAsync(v => v.Code == service + "/2"));
        var position = JsonNode.Parse(stored.Definition)!["layout"]!["REVIEW"]!;
        position["x"]!.GetValue<double>().ShouldBe(40.5);
        position["y"]!.GetValue<double>().ShouldBe(140);

        // Only nodes of the graph, on the canvas.
        var errors = await Should.ThrowAsync<ChangeValidationException>(() => host.Host.PublishAsync(
            WorkflowChangeTypes.Area,
            Version(service, 3, new { nodes = Graph, layout = new Dictionary<string, object> { ["GONE"] = new { x = 1, y = 1 }, ["DONE"] = new { x = -5, y = 1 } } })));
        errors.Errors["definition.layout"].ShouldContain(m => m.Contains("'GONE' does not exist"));
        errors.Errors["definition.layout"].ShouldContain(m => m.Contains("position of 'DONE'"));
    }

    [Fact]
    public async Task A_version_published_before_newer_fields_existed_can_still_be_retired()
    {
        await using var host = new WorkflowHost(fixture);
        var service = await host.ServiceAsync();
        await host.Host.PublishAsync(WorkflowChangeTypes.Area, Version(service, 2, new { nodes = Graph }));

        // As stored by an earlier release: no key for the fields added since (left out here when empty).
        await host.Host.AsAsync("admin", async sp =>
        {
            var db = sp.GetRequiredService<WorkflowDbContext>();
            var version = await db.Versions.SingleAsync(v => v.Code == service + "/2");
            version.Definition = Strip(JsonNode.Parse(version.Definition)!)!.ToJsonString();
            await db.SaveChangesAsync();
            return 0;
        });

        await host.Host.PublishAsync(WorkflowChangeTypes.Area, new Change(WorkflowChangeTypes.Version, service + "/2", ChangeOperation.Update, new
        {
            nameAr = "الإصدار 2",
            nameEn = "Version 2",
            isRetired = true,
            definition = new { nodes = Graph },
        }));
        var retired = await host.Host.AsAsync("admin", sp => sp.GetRequiredService<WorkflowDbContext>().Versions.AsNoTracking().SingleAsync(v => v.Code == service + "/2"));
        retired.IsRetired.ShouldBeTrue();

        // The graph itself still cannot change.
        await Should.ThrowAsync<ChangeValidationException>(() => host.Host.PublishAsync(WorkflowChangeTypes.Area, new Change(WorkflowChangeTypes.Version, service + "/2", ChangeOperation.Update, new
        {
            nameAr = "الإصدار 2",
            nameEn = "Version 2",
            definition = new { nodes = new[] { Graph[0], Graph[1], End("DONE", "REJECTED") } },
        })));
    }

    private static Change Version(string service, int number, object definition) =>
        new(WorkflowChangeTypes.Version, $"{service}/{number}", ChangeOperation.Create, new { nameAr = $"الإصدار {number}", nameEn = $"Version {number}", definition });

    /// <summary>Drops null and empty values, as a definition serialized before those fields existed would lack them.</summary>
    private static JsonNode? Strip(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj.ToList())
                {
                    var kept = Strip(value);
                    obj.Remove(key);
                    if (kept is not null && kept is not JsonArray { Count: 0 } && kept is not JsonObject { Count: 0 })
                    {
                        obj[key] = kept;
                    }
                }

                return obj;
            case JsonArray array:
                var items = array.Select(i => Strip(i)?.DeepClone()).ToList();
                return new JsonArray([.. items]);
            default:
                return node?.DeepClone();
        }
    }
}
