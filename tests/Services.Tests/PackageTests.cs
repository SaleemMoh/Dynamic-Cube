using System.Text.Json;
using DynamicCube.Modules.Services.Changes;
using DynamicCube.Modules.Services.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Services.Tests;

public sealed class PackageTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task The_classification_package_publishes()
    {
        await using var host = new TestHost(fixture.RequireConnectionString());
        using var package = JsonDocument.Parse(await File.ReadAllTextAsync(RepositoryFile("deploy/config-packages/classification-new.json"), TestContext.Current.CancellationToken));
        var changes = package.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => new Change(
                i.GetProperty("entityType").GetString()!,
                i.GetProperty("entityCode").GetString()!,
                Enum.Parse<ChangeOperation>(i.GetProperty("operation").GetString()!, ignoreCase: true),
                i.GetProperty("payload").Clone()))
            .ToArray();

        await host.PublishAsync(ServiceChangeTypes.Area, changes);

        var version = await host.AsAsync("admin", sp => sp.GetRequiredService<ServicesDbContext>().Versions.AsNoTracking().SingleAsync(v => v.Code == "CLASSIFICATION_NEW/1"));
        version.Resolved.ShouldNotBeNull();
        version.Resolved.ShouldContain("CLS_ANNUAL_REVENUE");
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
