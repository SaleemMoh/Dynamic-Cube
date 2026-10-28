namespace DynamicCube.Modules.AppsControl.Changes;

internal static class ChildSync
{
    /// <summary>
    /// Makes <paramref name="existing"/> match <paramref name="desired"/> by key: updates matches in place, adds new
    /// rows and removes the rest (removed rows are deleted as orphans).
    /// </summary>
    public static void Sync<TChild, TKey>(List<TChild> existing, IReadOnlyList<TChild> desired, Func<TChild, TKey> key, Action<TChild, TChild> copy)
        where TKey : notnull
    {
        var wanted = desired.ToDictionary(key);
        existing.RemoveAll(e => !wanted.ContainsKey(key(e)));
        foreach (var item in existing)
        {
            copy(wanted[key(item)], item);
            wanted.Remove(key(item));
        }

        existing.AddRange(desired.Where(d => wanted.ContainsKey(key(d))));
    }

    /// <summary>Reports duplicate keys in a payload list.</summary>
    public static void RequireUnique<T, TKey>(IEnumerable<T> items, Func<T, TKey> key, string field, ValidationErrors errors)
    {
        foreach (var duplicate in items.GroupBy(key).Where(g => g.Count() > 1))
        {
            errors.Add(field, $"'{duplicate.Key}' is listed more than once.");
        }
    }
}
