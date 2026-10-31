using System.Text.Json.Nodes;
using DynamicCube.Modules.Workflow.Domain;
using DynamicCube.Platform.Integration;
using DynamicCube.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using static DynamicCube.Modules.Workflow.Tests.WorkflowHost;

namespace DynamicCube.Modules.Workflow.Tests;

/// <summary>Integration steps of a workflow: the call's outcome is kept in the facts and routes the request.</summary>
public sealed class IntegrationNodeTests(DatabaseFixture fixture)
{
    /// <summary>Answers a licence check: active for licence L-1, not found otherwise.</summary>
    private sealed class LicenceGateway : IIntegrationGateway
    {
        public List<IReadOnlyDictionary<string, string?>> Calls { get; } = [];

        public Task<IntegrationResult> InvokeAsync(string operationCode, IReadOnlyDictionary<string, string?> inputs, CancellationToken cancellationToken)
        {
            Calls.Add(inputs);
            var active = inputs.GetValueOrDefault("licence") == "L-1";
            return Task.FromResult(new IntegrationResult(
                active ? IntegrationOutcome.Success : IntegrationOutcome.NotFound,
                active ? "000" : "404",
                active ? null : "الترخيص غير موجود",
                active ? null : "Licence not found",
                new Dictionary<string, JsonNode?> { ["status"] = active ? "ACTIVE" : null },
                new JsonObject { ["licence"] = inputs.GetValueOrDefault("licence") },
                Guid.NewGuid(),
                false));
        }
    }

    private static object Call(string code, string next, string? failed) => new
    {
        code,
        type = "INTEGRATION",
        nameAr = "التحقق من الترخيص",
        nameEn = "Check the licence",
        operation = "LICENCE_CHECK",
        parameters = new { licence = new { var = "data.LICENCE" } },
        result = "licence",
        next,
        failed,
    };

    [Fact]
    public async Task A_call_keeps_its_outcome_in_the_facts_and_a_failure_takes_its_route()
    {
        var gateway = new LicenceGateway();
        await using var host = new WorkflowHost(fixture, s => s.AddSingleton<IIntegrationGateway>(gateway));
        var workflow = await host.WorkflowAsync(
        [
            Start("CHECK"),
            Call("CHECK", "APPROVED", "REJECTED"),
            End("APPROVED", "APPROVED"),
            End("REJECTED", "REJECTED"),
        ]);

        var active = await host.InstanceAsync(await host.StartAsync(workflow, new JsonObject { ["LICENCE"] = "L-1" }));
        active.Outcome.ShouldBe("APPROVED");
        var facts = JsonNode.Parse(active.Facts)!["integrations"]!["licence"]!;
        facts["success"]!.GetValue<bool>().ShouldBeTrue();
        facts["outputs"]!["status"]!.GetValue<string>().ShouldBe("ACTIVE");
        gateway.Calls[0]["licence"].ShouldBe("L-1");

        var missing = await host.InstanceAsync(await host.StartAsync(workflow, new JsonObject { ["LICENCE"] = "L-9" }));
        missing.Outcome.ShouldBe("REJECTED", "a failed call takes the failed route");
        JsonNode.Parse(missing.Facts)!["integrations"]!["licence"]!["messageEn"]!.GetValue<string>().ShouldBe("Licence not found");
        var events = await host.Host.AsAsync("admin", sp => sp.GetRequiredService<Persistence.WorkflowDbContext>().Events.Where(e => e.InstanceId == missing.Id).Select(e => e.Type).ToListAsync());
        events.ShouldContain(WorkflowEventTypes.IntegrationFailed);
    }
}
