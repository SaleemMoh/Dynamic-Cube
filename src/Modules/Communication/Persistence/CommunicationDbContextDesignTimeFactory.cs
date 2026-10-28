using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DynamicCube.Modules.Communication.Persistence;

/// <summary>Used by <c>dotnet ef</c> to create migrations; no database connection is needed for that.</summary>
internal sealed class CommunicationDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CommunicationDbContext>
{
    public CommunicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DynamicCube")
            ?? "Host=localhost;Database=dynamic_cube;Username=postgres";
        var builder = new DbContextOptionsBuilder<CommunicationDbContext>();
        builder.UseDynamicCubePostgres(connectionString, CommunicationDbContext.Schema);
        return new CommunicationDbContext(builder.Options);
    }
}
