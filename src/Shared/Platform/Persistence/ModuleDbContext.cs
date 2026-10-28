using DynamicCube.Platform.Auditing;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Outbox;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Platform.Persistence;

/// <summary>
/// Base class for every module's DbContext. Maps the kernel tables (outbox, audit log, change sets) so a module
/// can write them in the same transaction as its own changes. Only <see cref="PlatformDbContext"/> owns their
/// migrations; module contexts exclude them.
/// </summary>
public abstract class ModuleDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<ChangeSet> ChangeSets => Set<ChangeSet>();

    /// <summary>True only for the context that creates and migrates the kernel tables.</summary>
    protected virtual bool OwnsPlatformTables => false;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyPlatformModel(excludeFromMigrations: !OwnsPlatformTables);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // Enums are stored as readable text.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(64);
        configurationBuilder.Conventions.Add(_ => new ClientGeneratedKeyConvention());
    }
}
