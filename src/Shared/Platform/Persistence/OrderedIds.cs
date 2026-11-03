namespace DynamicCube.Platform.Persistence;

/// <summary>
/// Version 7 GUIDs that increase within the process, even when created in the same millisecond. Use them for rows read
/// back in the order they were written (timelines), so <c>ORDER BY at, id</c> is stable when events share a time.
/// </summary>
public static class OrderedIds
{
    private static readonly Lock Gate = new();
    private static long _last;

    public static Guid Next()
    {
        lock (Gate)
        {
            _last = Math.Max(_last + 1, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            return Guid.CreateVersion7(DateTimeOffset.FromUnixTimeMilliseconds(_last));
        }
    }
}
