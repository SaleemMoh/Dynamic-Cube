using System.Text.Json;
using DynamicCube.Modules.AppsControl.Access;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.ChangeSets;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.AppsControl.Tests.Infrastructure;

public sealed record Change(string EntityType, string EntityCode, ChangeOperation Operation, object? Payload);

/// <summary>Runs Apps Control services as a given user, each call in its own scope (like one HTTP request).</summary>
public sealed class Harness : IAsyncDisposable
{
    private readonly TestUser _user = new("admin");
    private readonly ServiceProvider _services;

    public Harness(AppsControlFixture fixture, IDictionary<string, string?>? settings = null)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _services = TestServices.Build(fixture.RequireConnectionString(), _user, settings);
    }

    /// <summary>Short unique suffix so tests sharing the database do not collide.</summary>
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

    public Task<AccessSnapshot> AccessAsync(string userName) =>
        AsAsync(userName, sp => sp.GetRequiredService<AccessResolver>().GetAsync(CancellationToken.None));

    /// <summary>Maker drafts and submits; the checker approves and publishes (when the area needs approval).</summary>
    public async Task<Guid> PublishAsync(string area, params Change[] changes)
    {
        var id = await DraftAsync(area, changes);
        await AsAsync("maker", async sp =>
        {
            var service = sp.GetRequiredService<ChangeSetService<AppsControlDbContext>>();
            await service.SubmitAsync(id, CancellationToken.None);
        });
        await AsAsync("checker", async sp =>
        {
            var service = sp.GetRequiredService<ChangeSetService<AppsControlDbContext>>();
            if ((await service.GetAsync(id, CancellationToken.None)).Status == ChangeSetStatus.Submitted)
            {
                await service.ApproveAsync(id, "ok", CancellationToken.None);
            }

            await service.PublishAsync(id, CancellationToken.None);
        });
        return id;
    }

    public Task<Guid> DraftAsync(string area, params Change[] changes) =>
        AsAsync("maker", async sp =>
        {
            var service = sp.GetRequiredService<ChangeSetService<AppsControlDbContext>>();
            var changeSet = await service.CreateAsync(area, "test", CancellationToken.None);
            foreach (var change in changes)
            {
                await service.AddItemAsync(
                    changeSet.Id,
                    change.EntityType,
                    change.EntityCode,
                    change.Operation,
                    change.Payload is null ? null : JsonSerializer.Serialize(change.Payload),
                    CancellationToken.None);
            }

            return changeSet.Id;
        });

    public async ValueTask DisposeAsync() => await _services.DisposeAsync();
}
