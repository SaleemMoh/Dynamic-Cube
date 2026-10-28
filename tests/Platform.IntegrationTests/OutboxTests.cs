using DynamicCube.Platform.IntegrationTests.Infrastructure;
using DynamicCube.Platform.Outbox;
using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Platform.IntegrationTests;

public sealed class OutboxTests(PostgresFixture database)
{
    [Fact]
    public async Task Message_is_saved_atomically_with_the_business_change_and_delivered()
    {
        await using var services = await BuildCleanAsync();
        var ct = TestContext.Current.CancellationToken;
        var code = "OUTBOX-" + Guid.NewGuid().ToString("N")[..8];

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestModuleDbContext>();
            db.SampleSettings.Add(new SampleSetting { Code = code, ValueAr = "أ", ValueEn = "a" });
            db.Enqueue(new TestMessage("hello", Fail: false), services.GetRequiredService<TimeProvider>());
            await db.SaveChangesAsync(ct);
        }

        var processed = await services.GetRequiredService<OutboxProcessor>().ProcessBatchAsync(ct);

        processed.ShouldBe(1);
        services.GetRequiredService<HandlerLog>().Delivered.ShouldContain("hello");
        var message = await SingleMessageAsync(services, ct);
        message.ProcessedAt.ShouldNotBeNull();
        message.Attempts.ShouldBe(1);
        message.LockedUntil.ShouldBeNull();
    }

    [Fact]
    public async Task Failed_message_is_retried_after_backoff()
    {
        await using var services = await BuildCleanAsync();
        var ct = TestContext.Current.CancellationToken;
        var clock = services.GetRequiredService<TestClock>();
        var processor = services.GetRequiredService<OutboxProcessor>();
        await EnqueueAsync(services, new TestMessage("boom", Fail: true), ct);

        (await processor.ProcessBatchAsync(ct)).ShouldBe(1);
        var afterFirst = await SingleMessageAsync(services, ct);
        afterFirst.Attempts.ShouldBe(1);
        afterFirst.LastError.ShouldNotBeNull();
        afterFirst.ProcessedAt.ShouldBeNull();
        afterFirst.AvailableAt.ShouldBe(clock.GetUtcNow() + TimeSpan.FromSeconds(10));

        (await processor.ProcessBatchAsync(ct)).ShouldBe(0, "not due yet");

        clock.Advance(TimeSpan.FromSeconds(11));
        (await processor.ProcessBatchAsync(ct)).ShouldBe(1);
        (await SingleMessageAsync(services, ct)).Attempts.ShouldBe(2);
    }

    [Fact]
    public async Task Message_is_dead_lettered_after_max_attempts()
    {
        await using var services = await BuildCleanAsync(o => o.MaxAttempts = 2);
        var ct = TestContext.Current.CancellationToken;
        var clock = services.GetRequiredService<TestClock>();
        var processor = services.GetRequiredService<OutboxProcessor>();
        await EnqueueAsync(services, new TestMessage("boom", Fail: true), ct);

        await processor.ProcessBatchAsync(ct);
        clock.Advance(TimeSpan.FromMinutes(1));
        await processor.ProcessBatchAsync(ct);

        (await SingleMessageAsync(services, ct)).DeadLetteredAt.ShouldNotBeNull();
        clock.Advance(TimeSpan.FromDays(1));
        (await processor.ProcessBatchAsync(ct)).ShouldBe(0);
    }

    [Fact]
    public async Task Message_without_handler_is_recorded_as_failed()
    {
        await using var services = await BuildCleanAsync();
        var ct = TestContext.Current.CancellationToken;
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
            db.OutboxMessages.Add(new OutboxMessage { Type = "nobody.handles-this", Payload = "{}", OccurredAt = now, AvailableAt = now });
            await db.SaveChangesAsync(ct);
        }

        await services.GetRequiredService<OutboxProcessor>().ProcessBatchAsync(ct);

        var message = await SingleMessageAsync(services, ct);
        message.ProcessedAt.ShouldBeNull();
        message.LastError!.ShouldContain("No outbox handler registered");
    }

    [Fact]
    public async Task Concurrent_processors_deliver_each_message_once()
    {
        await using var services = await BuildCleanAsync(o => o.BatchSize = 25);
        var ct = TestContext.Current.CancellationToken;
        for (var i = 0; i < 40; i++)
        {
            await EnqueueAsync(services, new TestMessage($"m{i}", Fail: false), ct);
        }

        var processor = services.GetRequiredService<OutboxProcessor>();
        var counts = await Task.WhenAll(processor.ProcessBatchAsync(ct), processor.ProcessBatchAsync(ct), processor.ProcessBatchAsync(ct));

        counts.Sum().ShouldBe(40);
        var delivered = services.GetRequiredService<HandlerLog>().Delivered.ToList();
        delivered.Count.ShouldBe(40);
        delivered.Distinct().Count().ShouldBe(40);
    }

    private async Task<ServiceProvider> BuildCleanAsync(Action<OutboxOptions>? configure = null)
    {
        var services = TestServices.Build(database.RequireConnectionString(), s => s.Configure<OutboxOptions>(o => configure?.Invoke(o)));
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        return services;
    }

    private static async Task EnqueueAsync(ServiceProvider services, TestMessage message, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TestModuleDbContext>();
        db.Enqueue(message, services.GetRequiredService<TimeProvider>());
        await db.SaveChangesAsync(ct);
    }

    private static async Task<OutboxMessage> SingleMessageAsync(ServiceProvider services, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().OutboxMessages.AsNoTracking().SingleAsync(ct);
    }
}
