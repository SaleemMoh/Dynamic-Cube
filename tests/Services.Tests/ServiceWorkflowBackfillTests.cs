using System.Text.Json.Nodes;
using DynamicCube.Modules.Services.Changes;
using DynamicCube.Modules.Services.Persistence;
using DynamicCube.Modules.Workflow;
using DynamicCube.Modules.Workflow.Persistence;
using DynamicCube.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Services.Tests;

/// <summary>The migrator gives services configured before 1f their workflow.</summary>
public sealed class ServiceWorkflowBackfillTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Services_get_the_workflow_their_version_named_or_a_default_one()
    {
        await using var host = new TestHost(fixture.RequireConnectionString());
        var (named, plain) = (new ServiceSamples(), new ServiceSamples());
        await host.PublishAsync(ServiceChangeTypes.Area, [.. named.All()]);
        await host.PublishAsync(ServiceChangeTypes.Area, plain.ServiceChange());

        // As before 1f: the workflows belong to no service, and the first service's version names its workflow.
        await host.AsAsync("admin", async sp =>
        {
            var workflows = sp.GetRequiredService<WorkflowDbContext>();
            await workflows.Workflows.Where(w => w.ServiceCode == named.Service || w.ServiceCode == plain.Service).ExecuteUpdateAsync(u => u.SetProperty(w => w.ServiceCode, (string?)null));
            var versions = sp.GetRequiredService<ServicesDbContext>().Versions;
            var version = await versions.SingleAsync(v => v.Code == named.Service + "/1");
            var definition = JsonNode.Parse(version.Definition)!.AsObject();
            definition["workflow"] = named.Service;
            version.Definition = definition.ToJsonString();
            await sp.GetRequiredService<ServicesDbContext>().SaveChangesAsync();
        });

        var given = await host.AsAsync("migrator", sp => WorkflowModule.BackfillServiceWorkflowsAsync(sp, CancellationToken.None));
        given.ShouldBeGreaterThanOrEqualTo(2);

        var owners = await host.AsAsync("admin", sp => sp.GetRequiredService<WorkflowDbContext>().Workflows.AsNoTracking()
            .Where(w => w.ServiceCode == named.Service || w.ServiceCode == plain.Service).ToDictionaryAsync(w => w.ServiceCode!, w => w.Code));
        owners[named.Service].ShouldBe(named.Service, "the workflow the version named becomes the service's");
        owners[plain.Service].ShouldBe(plain.Service + "_2", "a new default one, since the old code is taken");
        (await host.AsAsync("migrator", sp => WorkflowModule.BackfillServiceWorkflowsAsync(sp, CancellationToken.None))).ShouldBe(0, "a second run finds nothing to do");
    }
}
