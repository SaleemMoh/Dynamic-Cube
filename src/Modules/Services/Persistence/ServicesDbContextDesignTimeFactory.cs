using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DynamicCube.Modules.Services.Persistence;

/// <summary>Used by <c>dotnet ef</c> to create migrations; no database connection is needed for that.</summary>
internal sealed class ServicesDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ServicesDbContext>
{
    public ServicesDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DynamicCube")
            ?? "Host=localhost;Database=dynamic_cube;Username=postgres";
        var builder = new DbContextOptionsBuilder<ServicesDbContext>();
        builder.UseDynamicCubePostgres(connectionString, ServicesDbContext.Schema);
        return new ServicesDbContext(builder.Options);
    }
}
