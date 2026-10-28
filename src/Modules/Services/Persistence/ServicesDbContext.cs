using DynamicCube.Modules.Services.Domain;
using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Services.Persistence;

public sealed class ServicesDbContext(DbContextOptions<ServicesDbContext> options) : ModuleDbContext(options)
{
    public const string Schema = "svc";

    public DbSet<Service> Services => Set<Service>();

    public DbSet<ServiceCategory> Categories => Set<ServiceCategory>();

    public DbSet<Question> Questions => Set<Question>();

    public DbSet<ServiceVersion> Versions => Set<ServiceVersion>();

    public DbSet<ServiceRequest> Requests => Set<ServiceRequest>();

    public DbSet<RequestEvent> RequestEvents => Set<RequestEvent>();

    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();

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
        modelBuilder.ConfigTable<ServiceCategory>(Schema, "category", b =>
        {
            b.Property(e => e.DescriptionAr).HasMaxLength(1000);
            b.Property(e => e.DescriptionEn).HasMaxLength(1000);
            b.Property(e => e.Icon).HasMaxLength(100);
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

        modelBuilder.Entity<ServiceRequest>(b =>
        {
            b.ToTable("request", Schema);
            b.Property(e => e.Number).HasMaxLength(50);
            b.HasIndex(e => e.Number).IsUnique();
            b.Property(e => e.ServiceCode).HasMaxLength(100).IsRequired();
            b.Property(e => e.LevelCode).HasMaxLength(100);
            b.Property(e => e.SubmitterName).HasMaxLength(500);
            b.Property(e => e.BeneficiaryNameAr).HasMaxLength(500);
            b.Property(e => e.BeneficiaryNameEn).HasMaxLength(500);
            b.Property(e => e.DisplayStatus).HasMaxLength(100).IsRequired();
            b.Property(e => e.Data).HasColumnType("jsonb").IsRequired();
            b.Property(e => e.CurrentStep).HasMaxLength(100);
            b.Property(e => e.Fees).HasJsonColumn();
            b.Property(e => e.FeeTotal).HasPrecision(18, 2);
            b.Property(e => e.CreatedBy).HasMaxLength(128);
            b.Property(e => e.UpdatedBy).HasMaxLength(128);
            b.Property<uint>("RowVersion").IsRowVersion();
            b.HasOne<ServiceVersion>().WithMany().HasForeignKey(e => e.ServiceVersionId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(e => new { e.BeneficiaryPartyId, e.StatusCategory });
            b.HasIndex(e => new { e.ServiceCode, e.StatusCategory });
        });
        modelBuilder.Entity<RequestEvent>(b =>
        {
            b.ToTable("request_event", Schema);
            b.Property(e => e.UserId).HasMaxLength(128).IsRequired();
            b.Property(e => e.Type).HasMaxLength(50).IsRequired();
            b.Property(e => e.Comment).HasMaxLength(4000);
            b.HasOne<ServiceRequest>().WithMany().HasForeignKey(e => e.RequestId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(e => new { e.RequestId, e.At });
        });
        modelBuilder.Entity<NumberSequence>(b =>
        {
            b.ToTable("number_sequence", Schema);
            b.HasKey(e => e.Key);
            b.Property(e => e.Key).HasMaxLength(100);
        });
    }
}
