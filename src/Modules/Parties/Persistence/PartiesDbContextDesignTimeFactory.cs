using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DynamicCube.Modules.Parties.Persistence;

/// <summary>Used by <c>dotnet ef</c> to create migrations; no database connection is needed for that.</summary>
internal sealed class PartiesDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PartiesDbContext>
{
    public PartiesDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DynamicCube")
            ?? "Host=localhost;Database=dynamic_cube;Username=postgres";
        var builder = new DbContextOptionsBuilder<PartiesDbContext>();
        builder.UseDynamicCubePostgres(connectionString, PartiesDbContext.Schema);
        return new PartiesDbContext(builder.Options);
    }
}
