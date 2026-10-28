using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Platform.Persistence;

/// <summary>
/// Owns and migrates the kernel tables; also used by the worker's outbox processor and as the shared store of
/// ASP.NET Core data-protection keys, so every API instance can read what another one protected.
/// </summary>
public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : ModuleDbContext(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override bool OwnsPlatformTables => true;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<DataProtectionKey>().ToTable("data_protection_keys", PlatformSchemas.Platform);
    }
}
