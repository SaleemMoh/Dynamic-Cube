using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Platform.Persistence;

/// <summary>Owns and migrates the kernel tables; also used by the worker's outbox processor.</summary>
public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : ModuleDbContext(options)
{
    protected override bool OwnsPlatformTables => true;
}
