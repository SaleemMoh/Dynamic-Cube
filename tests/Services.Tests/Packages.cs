using System.Text.Json;
using DynamicCube.Modules.Services.Changes;
using DynamicCube.Modules.Services.Persistence;
using DynamicCube.Platform.ChangeSets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DynamicCube.Tests.Infrastructure;

namespace DynamicCube.Modules.Services.Tests;

/// <summary>The configuration packages of <c>deploy/config-packages</c>, as change items.</summary>
internal static class Packages
{
    private static readonly SemaphoreSlim PortalLock = new(1, 1);

    /// <summary>
    /// Publishes the portal's sample services (<c>portal-services.json</c>) once per test database: several tests build
    /// on them, and their codes are fixed.
    /// </summary>
    public static async Task PortalServicesAsync(TestHost host)
    {
        await PortalLock.WaitAsync();
        try
        {
            var exists = await host.AsAsync("admin", sp => sp.GetRequiredService<ServicesDbContext>().Services.AsNoTracking().AnyAsync(s => s.Code == "SITE_VISIT"));
            if (!exists)
            {
                await host.PublishAsync(ServiceChangeTypes.Area, [.. Items("portal-services.json").Where(i => i.EntityType.StartsWith("svc.", StringComparison.Ordinal))]);
            }
        }
        finally
        {
            PortalLock.Release();
        }
    }

    public static List<Change> Items(string file)
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
