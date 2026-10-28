namespace DynamicCube.Platform.Persistence;

/// <summary>PostgreSQL schemas owned by the platform kernel.</summary>
public static class PlatformSchemas
{
    /// <summary>Outbox and other kernel infrastructure tables.</summary>
    public const string Platform = "platform";

    /// <summary>Audit log.</summary>
    public const string Audit = "audit";

    /// <summary>Configuration change sets (maker-checker).</summary>
    public const string Config = "cfg";
}
