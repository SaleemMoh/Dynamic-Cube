namespace DynamicCube.Modules.AppsControl.Lovs;

/// <summary>One value of an LOV; both languages are returned and the client chooses.</summary>
public sealed record LovItem(string Value, string LabelAr, string LabelEn, string? Parent);

public sealed record LovPage(string Lov, IReadOnlyList<LovItem> Items, string? DefaultValue, bool HasMore);

/// <summary>Filters for reading LOV values.</summary>
public sealed record LovQuery(string? Parent = null, string? Search = null, int Page = 1, int PageSize = 0, string? Resource = null);

/// <summary>
/// The values of an LOV a user may see: <see cref="All"/>, or exactly <see cref="Values"/> (possibly none).
/// </summary>
public sealed record GrantSet(bool All, IReadOnlySet<string> Values, string? DefaultValue)
{
    public static GrantSet Everything { get; } = new(true, new HashSet<string>(), null);

    public static GrantSet Nothing { get; } = new(false, new HashSet<string>(), null);

    public bool Allows(string value) => All || Values.Contains(value);
}

/// <summary>A column of a data object restricted to granted values for the current user.</summary>
public sealed record DataScopeFilter(string Column, string Lov, GrantSet Grant);
