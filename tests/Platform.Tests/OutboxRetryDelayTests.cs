using DynamicCube.Platform.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace DynamicCube.Platform.Tests;

public sealed class OutboxRetryDelayTests
{
    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    [InlineData(3, 40)]
    [InlineData(20, 3600)]
    public void Delay_doubles_and_is_capped(int attempts, int expectedSeconds)
    {
        var options = Options.Create(new OutboxOptions
        {
            InitialRetryDelay = TimeSpan.FromSeconds(10),
            MaxRetryDelay = TimeSpan.FromHours(1),
        });
        var processor = new OutboxProcessor(
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            new OutboxHandlerRegistry(),
            options,
            TimeProvider.System,
            NullLogger<OutboxProcessor>.Instance);

        processor.RetryDelay(attempts).ShouldBe(TimeSpan.FromSeconds(expectedSeconds));
    }
}
