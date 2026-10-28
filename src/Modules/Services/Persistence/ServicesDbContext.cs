using DynamicCube.Modules.Services.Domain;
using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Services.Persistence;

public sealed class ServicesDbContext(DbContextOptions<ServicesDbContext> options) : ModuleDbContext(options)
{
    public const string Schema = "svc";

    public DbSet<Service> Services => Set<Service>();

    public DbSet<Question> Questions => Set<Question>();

    public DbSet<ServiceVersion> Versions => Set<ServiceVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ConfigTable<Service>(Schema, "service", b =>
        {
            b.Property(e => e.DescriptionAr).HasMaxLength(4000);
            b.Property(e => e.DescriptionEn).HasMaxLength(4000);
            b.Property(e => e.CategoryCode).HasMaxLength(100);
            b.Property(e => e.Icon).HasMaxLength(100);
            b.Property(e => e.NumberPrefix).HasMaxLength(20).IsRequired();
            b.Property(e => e.PartyKinds).HasJsonColumn();
        });
        modelBuilder.ConfigTable<Question>(Schema, "question", b =>
        {
            b.Property(e => e.NameAr).HasMaxLength(1000);
            b.Property(e => e.NameEn).HasMaxLength(1000);
            b.Property(e => e.HintAr).HasMaxLength(2000);
            b.Property(e => e.HintEn).HasMaxLength(2000);
            b.Property(e => e.Lov).HasMaxLength(100);
            b.Property(e => e.LovParentQuestion).HasMaxLength(100);
            b.Property(e => e.Prefill).HasMaxLength(200);
            b.Property(e => e.Options).HasJsonColumn();
            b.Property(e => e.Validation).HasJsonColumn();
        });
        modelBuilder.ConfigTable<ServiceVersion>(Schema, "service_version", b =>
        {
            b.Property(e => e.Definition).HasColumnType("jsonb").IsRequired();
            b.Property(e => e.Resolved).HasColumnType("jsonb");
            b.HasOne(e => e.Service).WithMany().HasForeignKey(e => e.ServiceId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(e => new { e.ServiceId, e.Number }).IsUnique();
        });
    }
}
