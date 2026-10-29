using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DynamicCube.Modules.Content.Persistence;

/// <summary>Used by <c>dotnet ef</c> to create migrations; no database connection is needed for that.</summary>
internal sealed class ContentDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ContentDbContext>
{
    public ContentDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DynamicCube")
            ?? "Host=localhost;Database=dynamic_cube;Username=postgres";
        var builder = new DbContextOptionsBuilder<ContentDbContext>();
        builder.UseDynamicCubePostgres(connectionString, ContentDbContext.Schema);
        return new ContentDbContext(builder.Options);
    }
}
