using System.Text.Json;
using System.Text.Json.Serialization;
using DynamicCube.Platform.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DynamicCube.Platform.ChangeSets;

/// <summary>A configuration package as exported and imported in Apps Control (ADR-0027).</summary>
public sealed record ConfigPackage(string Title, List<ConfigPackageItem> Items);

public sealed record ConfigPackageItem(string EntityType, string EntityCode, string Operation, JsonElement? Payload);

/// <summary>
/// Loads configuration packages without a person at the screens (demo and sample data): each package goes through change
/// sets like an import in Apps Control, one per area in the order the areas first appear, each drafted and submitted by
/// one loader identity and approved and published by another, so the usual validation and maker-checker apply.
/// </summary>
public sealed class PackageLoader(IServiceScopeFactory scopes, ILogger<PackageLoader> logger)
{
    public const string Maker = "demo-loader";
    public const string Checker = "demo-checker";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { ReadCommentHandling = JsonCommentHandling.Skip };

    /// <summary>
    /// Reads the <c>*.json</c> packages of a folder in file-name order (e.g. <c>01-organization.json</c>). Each
    /// <c>${NAME}</c> in them is replaced by the variable of that name (e.g. the address of a gateway, which differs
    /// between a developer machine and Docker).
    /// </summary>
    public static IReadOnlyList<ConfigPackage> ReadFolder(string folder, IReadOnlyDictionary<string, string>? variables = null) =>
        [.. Directory.GetFiles(folder, "*.json").Order(StringComparer.Ordinal).Select(f => ReadFile(f, variables))];

    public static ConfigPackage ReadFile(string path, IReadOnlyDictionary<string, string>? variables = null)
    {
        var text = File.ReadAllText(path);
        foreach (var (name, value) in variables ?? new Dictionary<string, string>())
        {
            text = text.Replace("${" + name + "}", JsonEncodedText.Encode(value).ToString(), StringComparison.Ordinal);
        }

        return JsonSerializer.Deserialize<ConfigPackage>(text, Json) ?? throw new InvalidOperationException($"{path} is not a configuration package.");
    }

    /// <summary>Whether the packages were loaded: the last row they create exists.</summary>
    public async Task<bool> IsLoadedAsync(IReadOnlyList<ConfigPackage> packages, Action<string> actAs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(actAs);
        var last = packages.SelectMany(p => p.Items).LastOrDefault(i => Is(i.Operation, "CREATE"));
        if (last is null)
        {
            return false;
        }

        actAs(Maker);
        await using var scope = scopes.CreateAsyncScope();
        return await Applier(scope.ServiceProvider, last.EntityType).GetAsync(last.EntityCode, cancellationToken) is not null;
    }

    /// <summary>Applies the packages in order; <paramref name="actAs"/> switches the signed-in identity of the scopes it creates.</summary>
    public async Task LoadAsync(IReadOnlyList<ConfigPackage> packages, Action<string> actAs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(actAs);
        foreach (var package in packages)
        {
            var areas = 0;
            foreach (var group in (await GroupAsync(package, actAs, cancellationToken)).Where(g => g.Value.Count > 0))
            {
                await PublishAsync(package.Title, group.Key, group.Value, actAs, cancellationToken);
                areas++;
            }

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Loaded {Title}: {Items} item(s) in {Areas} change set(s)", package.Title, package.Items.Count, areas);
            }
        }
    }

    /// <summary>
    /// Items by area, in the order the areas first appear; UPSERT becomes CREATE or UPDATE. A row to create that exists
    /// already is left as it is, so a load stopped half-way resumes where it stopped.
    /// </summary>
    private async Task<List<KeyValuePair<string, List<(string Type, string Code, ChangeOperation Operation, string? Payload)>>>> GroupAsync(
        ConfigPackage package, Action<string> actAs, CancellationToken cancellationToken)
    {
        actAs(Maker);
        await using var scope = scopes.CreateAsyncScope();
        var groups = new List<KeyValuePair<string, List<(string, string, ChangeOperation, string?)>>>();
        foreach (var item in package.Items)
        {
            var applier = Applier(scope.ServiceProvider, item.EntityType);
            var exists = await applier.GetAsync(item.EntityCode, cancellationToken) is not null;
            if (exists && Is(item.Operation, "CREATE"))
            {
                continue;
            }

            var operation = Is(item.Operation, "UPSERT")
                ? exists ? ChangeOperation.Update : ChangeOperation.Create
                : Enum.Parse<ChangeOperation>(item.Operation, ignoreCase: true);
            var payload = item.Payload is { ValueKind: not JsonValueKind.Null } body ? body.GetRawText() : null;
            var index = groups.FindIndex(g => g.Key == applier.Area);
            if (index < 0 || index != groups.Count - 1)
            {
                // A later run of the same area after another area is its own change set: it may rely on that area.
                groups.Add(new(applier.Area, []));
            }

            groups[^1].Value.Add((applier.EntityType, item.EntityCode, operation, payload));
        }

        return groups;
    }

    private async Task PublishAsync(string title, string area, List<(string Type, string Code, ChangeOperation Operation, string? Payload)> items, Action<string> actAs, CancellationToken cancellationToken)
    {
        Guid id;
        actAs(Maker);
        await using (var scope = scopes.CreateAsyncScope())
        {
            var workflow = Workflow(scope.ServiceProvider, items[0].Type);
            var changeSet = await workflow.CreateAsync(area, title, cancellationToken);
            id = changeSet.Id;
            foreach (var (type, code, operation, payload) in items)
            {
                try
                {
                    await workflow.AddItemAsync(id, type, code, operation, payload, cancellationToken);
                }
                catch (ChangeValidationException ex)
                {
                    await workflow.DiscardAsync(id, CancellationToken.None);
                    var problems = string.Join("; ", ex.Errors.Select(e => $"{e.Key}: {string.Join(" ", e.Value)}"));
                    throw new ChangeSetException($"{title}: {type} '{code}' is not valid — {problems}");
                }
            }

            await workflow.SubmitAsync(id, cancellationToken);
        }

        actAs(Checker);
        await using (var scope = scopes.CreateAsyncScope())
        {
            var workflow = Workflow(scope.ServiceProvider, items[0].Type);
            if ((await workflow.GetAsync(id, cancellationToken)).Status == ChangeSetStatus.Submitted)
            {
                await workflow.ApproveAsync(id, "Loaded from a configuration package.", cancellationToken);
            }

            await workflow.PublishAsync(id, cancellationToken);
        }
    }

    private static IConfigApplier Applier(IServiceProvider services, string entityType) =>
        services.GetServices<IConfigApplier>().FirstOrDefault(a => string.Equals(a.EntityType, entityType, StringComparison.OrdinalIgnoreCase))
            ?? throw new ChangeSetException($"No module handles entity type '{entityType}'.");

    private static IChangeSetWorkflow Workflow(IServiceProvider services, string entityType) =>
        services.GetServices<IChangeSetWorkflow>().FirstOrDefault(w => w.OwnsEntityType(entityType))
            ?? throw new ChangeSetException($"No module handles entity type '{entityType}'.");

    private static bool Is(string operation, string name) => string.Equals(operation, name, StringComparison.OrdinalIgnoreCase);
}
