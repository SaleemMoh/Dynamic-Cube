using DynamicCube.Modules.Integration.Domain;
using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Integration.Persistence;

public sealed class IntegrationDbContext(DbContextOptions<IntegrationDbContext> options) : ModuleDbContext(options)
{
    public const string Schema = "integ";

    public DbSet<Provider> Providers => Set<Provider>();

    public DbSet<Connection> Connections => Set<Connection>();

    public DbSet<EnvelopeProfile> EnvelopeProfiles => Set<EnvelopeProfile>();

    public DbSet<Operation> Operations => Set<Operation>();

    public DbSet<CallLog> CallLogs => Set<CallLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ConfigTable<Provider>(Schema, "provider", b =>
        {
            b.Property(e => e.DescriptionAr).HasMaxLength(2000);
            b.Property(e => e.DescriptionEn).HasMaxLength(2000);
        });
        modelBuilder.ConfigTable<Connection>(Schema, "connection", b =>
        {
            b.Property(e => e.BaseUrl).HasMaxLength(500).IsRequired();
            b.Property(e => e.SecretName).HasMaxLength(100);
            b.Property(e => e.TokenUrl).HasMaxLength(500);
            b.Property(e => e.ApiKeyHeader).HasMaxLength(100);
            b.Property(e => e.Headers).HasJsonColumn();
            b.HasOne(e => e.Provider).WithMany().HasForeignKey(e => e.ProviderId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.ConfigTable<EnvelopeProfile>(Schema, "envelope_profile", b =>
        {
            b.Property(e => e.StatusCodePath).HasMaxLength(200);
            b.Property(e => e.StatusMessagePath).HasMaxLength(200);
            b.Property(e => e.BusinessCodePath).HasMaxLength(200);
            b.Property(e => e.BusinessMessagePath).HasMaxLength(200);
            b.Property(e => e.ResultPath).HasMaxLength(200);
            b.Property(e => e.SuccessStatusCodes).HasJsonColumn();
            b.Property(e => e.SuccessBusinessCodes).HasJsonColumn();
            b.Property(e => e.ResponseCodes).HasJsonColumn();
        });
        modelBuilder.ConfigTable<Operation>(Schema, "operation", b =>
        {
            b.Property(e => e.DescriptionAr).HasMaxLength(2000);
            b.Property(e => e.DescriptionEn).HasMaxLength(2000);
            b.Property(e => e.Method).HasMaxLength(10).IsRequired();
            b.Property(e => e.PathTemplate).HasMaxLength(1000).IsRequired();
            b.Property(e => e.Parameters).HasJsonColumn();
            b.Property(e => e.Outputs).HasJsonColumn();
            b.Property(e => e.ResponseCodes).HasJsonColumn();
            b.Property(e => e.MaskFields).HasJsonColumn();
            b.HasOne(e => e.Connection).WithMany().HasForeignKey(e => e.ConnectionId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(e => e.EnvelopeProfile).WithMany().HasForeignKey(e => e.EnvelopeProfileId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CallLog>(b =>
        {
            b.ToTable("call_log", Schema);
            b.HasKey(e => e.Id);
            b.Property(e => e.OperationCode).HasMaxLength(100).IsRequired();
            b.Property(e => e.ConnectionCode).HasMaxLength(100).IsRequired();
            b.Property(e => e.CorrelationId).HasMaxLength(64);
            b.Property(e => e.UserId).HasMaxLength(128).IsRequired();
            b.Property(e => e.AppCode).HasMaxLength(64).IsRequired();
            b.Property(e => e.Code).HasMaxLength(100);
            b.Property(e => e.Message).HasMaxLength(2000);
            b.Property(e => e.Method).HasMaxLength(10);
            b.Property(e => e.Url).HasMaxLength(2000);
            b.Property(e => e.Error).HasMaxLength(4000);
            b.HasIndex(e => e.StartedAt);
            b.HasIndex(e => new { e.OperationCode, e.StartedAt });
        });
    }
}
