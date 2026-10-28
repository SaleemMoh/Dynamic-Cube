using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Platform.IntegrationTests.Infrastructure;

/// <summary>A minimal module used to exercise the kernel the way real modules will.</summary>
public sealed class TestModuleDbContext(DbContextOptions<TestModuleDbContext> options) : ModuleDbContext(options)
{
    public const string Schema = "test";

    public DbSet<SampleSetting> SampleSettings => Set<SampleSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<SampleSetting>(b =>
        {
            b.ToTable("sample_settings", Schema);
            b.HasKey(s => s.Id);
            b.Property(s => s.Code).HasMaxLength(100).IsRequired();
            b.HasIndex(s => s.Code).IsUnique();
            b.Property(s => s.CreatedBy).HasMaxLength(128);
            b.Property(s => s.UpdatedBy).HasMaxLength(128);
        });
    }
}

public sealed class SampleSetting : IAuditable, IHasAuditColumns
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Code { get; set; }

    public required string ValueAr { get; set; }

    public required string ValueEn { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}
