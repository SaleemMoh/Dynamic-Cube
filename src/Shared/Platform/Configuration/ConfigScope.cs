namespace DynamicCube.Platform.Configuration;

/// <summary>The level of a configuration row: the whole installation, one government entity, or one business app.</summary>
public enum ConfigLevel
{
    System,
    Entity,
    App,
}

/// <summary>
/// The entities and business apps whose configuration a user sees and changes. Null means every one; rows of the whole
/// installation (no entity) and of no app are seen by everyone.
/// </summary>
public sealed record ConfigScope(IReadOnlySet<string>? Entities, IReadOnlySet<string>? Apps)
{
    public static ConfigScope Everything { get; } = new(null, null);

    public bool IsEverything => Entities is null && Apps is null;

    /// <summary>A row is seen by staff of its entity and of any of its business apps (its own or a shared one).</summary>
    public bool CanSee(string? entityCode, string? businessApp, IEnumerable<string>? sharedApps = null) =>
        (entityCode is null || Entities is null || Entities.Contains(entityCode))
        && (businessApp is null || Apps is null || Apps.Contains(businessApp) || (sharedApps?.Any(Apps.Contains) ?? false));

    /// <summary>
    /// Whether the user may create or change rows at this level: system rows need every entity, entity rows every app of
    /// theirs, and a row of several apps all of them.
    /// </summary>
    public bool CanChange(string? entityCode, string? businessApp, IEnumerable<string>? sharedApps = null) =>
        CanSee(entityCode, businessApp)
        && (entityCode is not null || Entities is null)
        && (businessApp is not null || Apps is null)
        && (Apps is null || (sharedApps ?? []).All(Apps.Contains));

    public static ConfigLevel LevelOf(string? entityCode, string? businessApp) =>
        businessApp is not null ? ConfigLevel.App : entityCode is not null ? ConfigLevel.Entity : ConfigLevel.System;
}

/// <summary>Entities, business apps and the signed-in user's scope (implemented by Apps Control).</summary>
public interface IConfigScopes
{
    /// <summary>What the signed-in user sees and changes; <see cref="ConfigScope.Everything"/> outside a request.</summary>
    Task<ConfigScope> CurrentAsync(CancellationToken cancellationToken);

    /// <summary>The scope of another user (by sign-in subject or user name), e.g. the maker of a change set being published.</summary>
    Task<ConfigScope> OfUserAsync(string user, CancellationToken cancellationToken);

    /// <summary>
    /// Checks an entity and a business app given for a row: both must exist, and the app must belong to the entity
    /// (an app without an entity takes the app's). Entities and apps created earlier in <paramref name="draft"/> count.
    /// Returns the entity and app to store.
    /// </summary>
    Task<(string? EntityCode, string? BusinessApp)> ResolveAsync(
        string? entityCode, string? businessApp, ValidationErrors errors, CancellationToken cancellationToken, ChangeSets.ChangeSet? draft = null);

    /// <summary>
    /// Codes of the active apps that appear on a product channel (<c>INTERNAL</c>, <c>EXTERNAL</c> for the portal,
    /// <c>FIELD</c>): Dynamic Cube apps that tick it (ADR-0036).
    /// </summary>
    Task<IReadOnlySet<string>> AppsOnChannelAsync(string channel, CancellationToken cancellationToken);
}
