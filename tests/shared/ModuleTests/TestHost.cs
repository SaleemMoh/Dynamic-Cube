using System.Text.Json;
using DynamicCube.Modules.AppsControl;
using DynamicCube.Modules.Communication;
using DynamicCube.Modules.Content;
using DynamicCube.Modules.Decisions;
using DynamicCube.Modules.Documents;
using DynamicCube.Modules.Integration;
using DynamicCube.Modules.Parties;
using DynamicCube.Modules.Payments;
using DynamicCube.Modules.Services;
using DynamicCube.Modules.Visits;
using DynamicCube.Modules.Workflow;
using DynamicCube.Platform;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Communication;
using DynamicCube.Platform.Persistence;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Tests.Infrastructure;

public sealed record Change(string EntityType, string EntityCode, ChangeOperation Operation, object? Payload);

/// <summary>Switchable signed-in user (subject <c>sub-&lt;username&gt;</c>).</summary>
public sealed class TestUser(string userName) : ICurrentUser
{
    public string Name { get; set; } = userName;

    public string UserId => "sub-" + Name;

    public string? UserName => Name;

    public bool IsAuthenticated => true;

    public string AppCode => "INTERNAL";
}

/// <summary>Platform and module services against the test database, with a fake provider gateway.</summary>
public sealed class TestHost : IAsyncDisposable
{
    public const string SecretName = "MOMRAH_GATEWAY";

    private readonly TestUser _user = new("admin");
    private readonly ServiceProvider _services;

    public TestHost(string connectionString, Action<IServiceCollection>? configure = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DynamicCube"] = connectionString,
            ["AppsControl:BootstrapUsers:admin:0"] = "SYSTEM_ADMIN",
            ["AppsControl:BootstrapUsers:maker:0"] = "CONFIG_MAKER",
            ["AppsControl:BootstrapUsers:checker:0"] = "CONFIG_CHECKER",
            [$"Secrets:{SecretName}:clientId"] = "consumer-key",
            [$"Secrets:{SecretName}:clientSecret"] = "consumer-secret",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_user);
        services.AddSingleton<ICurrentUser>(sp => sp.GetRequiredService<TestUser>());
        services.AddPlatform(configuration);
        services.AddAppsControl(configuration, managesUsers: true);
        services.AddIntegration(configuration);
        services.AddParties(configuration);
        services.AddServices(configuration);
        services.AddWorkflow(configuration);
        services.AddDocuments(configuration);
        services.AddDecisions(configuration);
        services.AddCommunication(configuration);
        services.AddPayments(configuration);
        services.AddContent(configuration);
        services.AddVisits(configuration);
        configure?.Invoke(services);
        services.AddSingleton(Provider);
        services.AddHttpClient(IntegrationModule.HttpClientName).ConfigurePrimaryHttpMessageHandler(sp => sp.GetRequiredService<FakeProvider>());
        _services = services.BuildServiceProvider(validateScopes: true);
    }

    public FakeProvider Provider { get; } = new();

    public static string Unique(string prefix) => $"{prefix}_{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    public async Task<T> AsAsync<T>(string userName, Func<IServiceProvider, Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _user.Name = userName;
        await using var scope = _services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public Task AsAsync(string userName, Func<IServiceProvider, Task> action) =>
        AsAsync(userName, async sp =>
        {
            await action(sp);
            return true;
        });

    /// <summary>Maker drafts and submits through the workflow that owns the entity types; the checker approves and publishes.</summary>
    public async Task<Guid> PublishAsync(string area, params Change[] changes)
    {
        var id = await AsAsync("maker", async sp =>
        {
            var workflow = Workflow(sp, changes[0].EntityType);
            var changeSet = await workflow.CreateAsync(area, "test", CancellationToken.None);
            foreach (var change in changes)
            {
                await workflow.AddItemAsync(changeSet.Id, change.EntityType, change.EntityCode, change.Operation, change.Payload is null ? null : JsonSerializer.Serialize(change.Payload), CancellationToken.None);
            }

            await workflow.SubmitAsync(changeSet.Id, CancellationToken.None);
            return changeSet.Id;
        });
        await AsAsync("checker", async sp =>
        {
            var workflow = Workflow(sp, changes[0].EntityType);
            await workflow.ApproveAsync(id, "ok", CancellationToken.None);
            await workflow.PublishAsync(id, CancellationToken.None);
        });
        return id;
    }

    /// <summary>The notification events raised for a subject (still in the outbox: tests run no worker).</summary>
    public Task<List<CommunicationEvent>> RaisedAsync(Guid subjectId) =>
        AsAsync("admin", async sp =>
        {
            var rows = await sp.GetRequiredService<PlatformDbContext>().OutboxMessages
                .FromSql($"SELECT * FROM platform.outbox_messages WHERE type = 'comm.event' AND payload->>'subjectId' = {subjectId.ToString()}")
                .AsNoTracking().ToListAsync();
            return rows.OrderBy(r => r.OccurredAt).Select(r => JsonSerializer.Deserialize<CommunicationEvent>(r.Payload, JsonSerializerOptions.Web)!).ToList();
        });

    public static IChangeSetWorkflow Workflow(IServiceProvider services, string entityType) =>
        services.GetServices<IChangeSetWorkflow>().Single(w => w.OwnsEntityType(entityType));

    public async ValueTask DisposeAsync() => await _services.DisposeAsync();
}
