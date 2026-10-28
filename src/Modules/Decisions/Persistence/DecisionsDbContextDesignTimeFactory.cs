using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DynamicCube.Modules.Decisions.Persistence;

/// <summary>Used by <c>dotnet ef</c> to create migrations; no database connection is needed for that.</summary>
internal sealed class DecisionsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<DecisionsDbContext>
{
    public DecisionsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DynamicCube")
            ?? "Host=localhost;Database=dynamic_cube;Username=postgres";
        var builder = new DbContextOptionsBuilder<DecisionsDbContext>();
        builder.UseDynamicCubePostgres(connectionString, DecisionsDbContext.Schema);
        return new DecisionsDbContext(builder.Options);
    }
}
