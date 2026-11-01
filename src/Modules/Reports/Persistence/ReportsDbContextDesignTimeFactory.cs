using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DynamicCube.Modules.Reports.Persistence;

/// <summary>Used by <c>dotnet ef</c> to create migrations; no database connection is needed for that.</summary>
internal sealed class ReportsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ReportsDbContext>
{
    public ReportsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DynamicCube")
            ?? "Host=localhost;Database=dynamic_cube;Username=postgres";
        var builder = new DbContextOptionsBuilder<ReportsDbContext>();
        builder.UseDynamicCubePostgres(connectionString, ReportsDbContext.Schema);
        return new ReportsDbContext(builder.Options);
    }
}
