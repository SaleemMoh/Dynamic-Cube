namespace DynamicCube.Platform.Persistence;

/// <summary>A module DbContext on the shared database, so a host can check that the migrator has brought it up to date.</summary>
public sealed record ModuleContextRegistration(Type ContextType);
