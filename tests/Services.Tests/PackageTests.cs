using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.Services.Changes;
using DynamicCube.Modules.Services.Persistence;
using DynamicCube.Modules.Workflow.Changes;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Services.Tests;

public sealed class PackageTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task The_classification_packages_publish()
    {
        await using var host = new TestHost(fixture.RequireConnectionString());
        await host.PublishAsync(ServiceChangeTypes.Area, [.. Items("classification-new.json")]);

        var version = await host.AsAsync("admin", sp => sp.GetRequiredService<ServicesDbContext>().Versions.AsNoTracking().SingleAsync(v => v.Code == "CLASSIFICATION_NEW/1"));
        version.Resolved.ShouldNotBeNull();
        version.Resolved.ShouldContain("CLS_ANNUAL_REVENUE");

        // The workflow package: staff roles and users, the workflow, and version 2 of the service processed by it.
        var workflow = Items("classification-workflow.json");
        await host.PublishAsync(AppsControlAreas.Security, [.. workflow.Where(i => i.EntityType.StartsWith("apps.", StringComparison.Ordinal))]);
        await host.PublishAsync(WorkflowChangeTypes.Area, [.. workflow.Where(i => i.EntityType.StartsWith("wf.", StringComparison.Ordinal))]);
        await host.PublishAsync(ServiceChangeTypes.Area, [.. workflow.Where(i => i.EntityType.StartsWith("svc.", StringComparison.Ordinal))]);

        var second = await host.AsAsync("admin", sp => sp.GetRequiredService<ServicesDbContext>().Versions.AsNoTracking().SingleAsync(v => v.Code == "CLASSIFICATION_NEW/2"));
        JsonNode.Parse(second.Definition)!["workflow"]!.GetValue<string>().ShouldBe("CLASSIFICATION");
    }

    private static List<Change> Items(string file)
    {
        using var package = JsonDocument.Parse(File.ReadAllText(RepositoryFile("deploy/config-packages/" + file)));
        return [.. package.RootElement.GetProperty("items").EnumerateArray().Select(i => new Change(
            i.GetProperty("entityType").GetString()!,
            i.GetProperty("entityCode").GetString()!,
            Enum.Parse<ChangeOperation>(i.GetProperty("operation").GetString()!, ignoreCase: true),
            i.GetProperty("payload").Clone()))];
    }

    private static string RepositoryFile(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DynamicCube.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, relative);
    }
}
