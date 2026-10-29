using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DynamicCube.Modules.Visits.Persistence;

/// <summary>Used by <c>dotnet ef</c> to create migrations; no database connection is needed for that.</summary>
internal sealed class VisitsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<VisitsDbContext>
{
    public VisitsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DynamicCube")
            ?? "Host=localhost;Database=dynamic_cube;Username=postgres";
        var builder = new DbContextOptionsBuilder<VisitsDbContext>();
        builder.UseDynamicCubePostgres(connectionString, VisitsDbContext.Schema);
        return new VisitsDbContext(builder.Options);
    }
}
