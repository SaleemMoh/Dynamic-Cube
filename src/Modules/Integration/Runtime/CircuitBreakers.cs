using System.Collections.Concurrent;

namespace DynamicCube.Modules.Integration.Runtime;

/// <summary>
/// Per-connection circuit breakers (in memory, per instance): after N consecutive technical failures calls fail fast
/// for a while, so a provider outage does not tie up request threads; one trial call is let through afterwards.
/// </summary>
public sealed class CircuitBreakers(TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, State> _states = new(StringComparer.Ordinal);

    public bool IsOpen(string connection)
    {
        if (!_states.TryGetValue(connection, out var state))
        {
            return false;
        }

        lock (state)
        {
            if (state.OpenUntil is null)
            {
                return false;
            }

            if (clock.GetUtcNow() < state.OpenUntil)
            {
                return true;
            }

            // Half-open: let the next call try; a failure re-opens immediately.
            state.OpenUntil = null;
            state.Failures = state.Threshold - 1;
            return false;
        }
    }

    public void Record(string connection, bool technicalFailure, int threshold, int openSeconds)
    {
        var state = _states.GetOrAdd(connection, _ => new State());
        lock (state)
        {
            state.Threshold = threshold;
            if (!technicalFailure)
            {
                state.Failures = 0;
                state.OpenUntil = null;
                return;
            }

            state.Failures++;
            if (state.Failures >= threshold)
            {
                state.OpenUntil = clock.GetUtcNow().AddSeconds(openSeconds);
            }
        }
    }

    private sealed class State
    {
        public int Failures { get; set; }

        public int Threshold { get; set; } = 1;

        public DateTimeOffset? OpenUntil { get; set; }
    }
}
