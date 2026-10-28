using System.Text.Json;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Tests.Infrastructure;

namespace DynamicCube.Modules.Services.Tests;

/// <summary>The configuration packages of <c>deploy/config-packages</c>, as change items.</summary>
internal static class Packages
{
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
