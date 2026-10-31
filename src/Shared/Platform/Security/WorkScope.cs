namespace DynamicCube.Platform.Security;

/// <summary>
/// Where the current employee works (ADR-0036 steps 5 and 6): the company, and the branches of it their roles cover. Lists of
/// requests, tasks and documents show only work of that company and those branches.
/// </summary>
/// <param name="Restricted">False outside the internal system (the portal, background work): nothing is filtered.</param>
/// <param name="Company">The company the employee works in; null when they may work in none (then nothing is visible).</param>
/// <param name="Branches">Branches of that company they cover; null for every branch.</param>
public sealed record WorkScope(bool Restricted, string? Company, IReadOnlySet<string>? Branches)
{
    public static WorkScope Unrestricted { get; } = new(false, null, null);

    /// <summary>Whether work of a company and branch is visible. Work without a company (older rows) is visible as before.</summary>
    public bool Allows(string? company, string? branch) =>
        !Restricted || company is null || (company == Company && (Branches is null || branch is null || Branches.Contains(branch)));
}

/// <summary>The current employee's work scope (implemented by Apps Control).</summary>
public interface IWorkScope
{
    Task<WorkScope> CurrentAsync(CancellationToken cancellationToken);
}
