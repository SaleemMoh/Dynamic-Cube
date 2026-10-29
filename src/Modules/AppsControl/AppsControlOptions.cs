namespace DynamicCube.Modules.AppsControl;

public sealed class AppsControlOptions
{
    public const string SectionName = "AppsControl";

    /// <summary>
    /// Users who receive roles when they sign in (username → role codes). Used to bootstrap the first administrator;
    /// assignments are only added, never removed.
    /// </summary>
    public Dictionary<string, string[]> BootstrapUsers { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Development only: when a known username signs in with a new identity-provider subject (a development Keycloak
    /// re-imports its realm on every fresh start), move the account to the new subject. Off in production, where a reused
    /// username must not inherit someone else's roles; the sign-in is refused with 403 instead.
    /// </summary>
    public bool RelinkAccountsByUserName { get; init; }

    /// <summary>Database schemas whose tables and views may be registered as data objects.</summary>
    public string[] DataObjectSchemas { get; init; } = ["apps", "lov", "party", "svc", "req", "wf", "doc", "rpt"];

    /// <summary>
    /// Connection string of a read-only database role used for restricted-SQL LOVs. Restricted SQL is disabled when empty.
    /// Grant this role SELECT only on the tables and views that LOV queries may read.
    /// </summary>
    public string? LovSqlConnectionString { get; init; }

    /// <summary>Statement timeout for restricted-SQL LOVs.</summary>
    public int LovSqlTimeoutSeconds { get; init; } = 5;

    /// <summary>How long resolved privileges, menus and grants are cached per user.</summary>
    public int AccessCacheSeconds { get; init; } = 60;
}
