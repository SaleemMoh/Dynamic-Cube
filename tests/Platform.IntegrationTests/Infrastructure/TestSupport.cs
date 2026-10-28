using System.Collections.Concurrent;
using System.Text.Json;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Outbox;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Platform.IntegrationTests.Infrastructure;

/// <summary>Controllable clock.</summary>
public sealed class TestClock : TimeProvider
{
    private DateTimeOffset _now = new(DateTimeOffset.UtcNow.Ticks / TimeSpan.TicksPerMillisecond * TimeSpan.TicksPerMillisecond, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>Current user that tests can switch (maker → checker).</summary>
public sealed class TestUser : ICurrentUser
{
    public string UserId { get; set; } = "maker";

    public string? UserName => UserId;

    public bool IsAuthenticated => true;

    public string AppCode => "TEST";
}

public sealed record TestMessage(string Text, bool Fail) : IOutboxMessage
{
    public static string MessageType => "test.message";
}

/// <summary>Records delivered messages; fails when asked to.</summary>
public sealed class TestMessageHandler(HandlerLog log) : IOutboxHandler<TestMessage>
{
    public Task HandleAsync(TestMessage message, CancellationToken cancellationToken)
    {
        if (message.Fail)
        {
            throw new InvalidOperationException("Simulated delivery failure");
        }

        log.Delivered.Add(message.Text);
        return Task.CompletedTask;
    }
}

public sealed class HandlerLog
{
    public ConcurrentBag<string> Delivered { get; } = [];
}

/// <summary>Applies <c>test.sample-setting</c> change items; refuses the code <c>BOOM</c> to test rollback.</summary>
public sealed class SampleSettingApplier : IChangeApplier<TestModuleDbContext>
{
    public string EntityType => "test.sample-setting";

    public async Task ApplyAsync(TestModuleDbContext context, ChangeItem item, CancellationToken cancellationToken)
    {
        if (item.EntityCode == "BOOM")
        {
            throw new InvalidOperationException("Simulated applier failure");
        }

        var payload = item.After is null ? null : JsonSerializer.Deserialize<SettingPayload>(item.After, JsonSerializerOptions.Web);
        var existing = await context.SampleSettings.SingleOrDefaultAsync(s => s.Code == item.EntityCode, cancellationToken);

        switch (item.Operation)
        {
            case ChangeOperation.Create:
                context.SampleSettings.Add(new SampleSetting { Code = item.EntityCode, ValueAr = payload!.ValueAr, ValueEn = payload.ValueEn });
                break;
            case ChangeOperation.Update:
                existing!.ValueAr = payload!.ValueAr;
                existing.ValueEn = payload.ValueEn;
                break;
            case ChangeOperation.Delete:
                context.SampleSettings.Remove(existing!);
                break;
        }
    }

    private sealed record SettingPayload(string ValueAr, string ValueEn);
}

public static class TestServices
{
    public static ServiceProvider Build(string connectionString, Action<IServiceCollection>? configure = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DynamicCube"] = connectionString })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TestClock>();
        services.AddSingleton<TimeProvider>(sp => sp.GetRequiredService<TestClock>());
        services.AddSingleton<TestUser>();
        services.AddSingleton<ICurrentUser>(sp => sp.GetRequiredService<TestUser>());
        services.AddSingleton<HandlerLog>();

        services.AddPlatform(configuration);
        services.AddModuleDbContext<TestModuleDbContext>(configuration, TestModuleDbContext.Schema);
        services.AddChangeSets<TestModuleDbContext>();
        services.AddScoped<IChangeApplier<TestModuleDbContext>, SampleSettingApplier>();
        services.AddOutboxHandler<TestMessage, TestMessageHandler>();

        configure?.Invoke(services);
        return services.BuildServiceProvider(validateScopes: true);
    }
}
