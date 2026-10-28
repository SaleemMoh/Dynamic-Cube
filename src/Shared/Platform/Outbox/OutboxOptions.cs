namespace DynamicCube.Platform.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>Messages fetched per polling cycle.</summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>Delay between polling cycles when the outbox is empty.</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>How long a worker owns a fetched message before others may take it.</summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Attempts before a message is dead-lettered.</summary>
    public int MaxAttempts { get; set; } = 8;

    /// <summary>First retry delay; doubled on each further attempt.</summary>
    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Upper bound for the retry delay.</summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromHours(1);
}
