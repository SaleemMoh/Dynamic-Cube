using DynamicCube.Modules.Visits.Domain;
using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Visits.Persistence;

public sealed class VisitsDbContext(DbContextOptions<VisitsDbContext> options) : ModuleDbContext(options)
{
    public const string Schema = "vis";

    public DbSet<VisitType> Types => Set<VisitType>();

    public DbSet<Visit> Visits => Set<Visit>();

    public DbSet<VisitPhoto> Photos => Set<VisitPhoto>();

    public DbSet<VisitEvent> Events => Set<VisitEvent>();

    public DbSet<VisitNumberSequence> NumberSequences => Set<VisitNumberSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ConfigTable<VisitType>(Schema, "visit_type", b =>
        {
            b.Property(e => e.Mode).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.DescriptionAr).HasMaxLength(2000);
            b.Property(e => e.DescriptionEn).HasMaxLength(2000);
            b.Property(e => e.Outcomes).HasJsonColumn();
            b.Property(e => e.Fields).HasJsonColumn();
            b.Property(e => e.InspectorRole).HasMaxLength(100);
            b.Property(e => e.External!).HasJsonColumn();
        });

        modelBuilder.Entity<Visit>(b =>
        {
            b.ToTable("visit", Schema);
            b.Property(e => e.Number).HasMaxLength(50).IsRequired();
            b.Property(e => e.TypeCode).HasMaxLength(100).IsRequired();
            b.Property(e => e.Mode).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.SubjectType).HasMaxLength(50).IsRequired();
            b.Property(e => e.SubjectNumber).HasMaxLength(50);
            b.Property(e => e.TitleAr).HasMaxLength(500).IsRequired();
            b.Property(e => e.TitleEn).HasMaxLength(500).IsRequired();
            b.Property(e => e.PartyName).HasMaxLength(500);
            b.Property(e => e.SiteAddress).HasMaxLength(1000);
            b.Property(e => e.Instructions).HasMaxLength(4000);
            b.Property(e => e.Inspector).HasMaxLength(256);
            b.Property(e => e.InspectorName).HasMaxLength(500);
            b.Property(e => e.CompletedBy).HasMaxLength(256);
            b.Property(e => e.Outcome).HasMaxLength(100);
            b.Property(e => e.Data).HasColumnType("jsonb").IsRequired();
            b.Property(e => e.Report).HasMaxLength(8000);
            b.Property(e => e.SignatureKey).HasMaxLength(200);
            b.Property(e => e.SignedBy).HasMaxLength(200);
            b.Property(e => e.SubmissionId).HasMaxLength(100);
            b.Property(e => e.ExternalReference).HasMaxLength(100);
            b.Property(e => e.ExternalStatus).HasMaxLength(100);
            b.Property(e => e.CancelReason).HasMaxLength(2000);
            b.Property(e => e.CreatedBy).HasMaxLength(256).IsRequired();
            b.Property<uint>("RowVersion").IsRowVersion();
            b.HasOne<VisitType>().WithMany().HasForeignKey(e => e.TypeId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(e => e.Number).IsUnique();
            b.HasIndex(e => new { e.TypeId, e.ExternalReference }).IsUnique();
            b.HasIndex(e => new { e.SubjectType, e.SubjectId });
            b.HasIndex(e => new { e.Inspector, e.Status });
            b.HasIndex(e => new { e.Status, e.NextPollAt });
        });
        modelBuilder.Entity<VisitPhoto>(b =>
        {
            b.ToTable("visit_photo", Schema);
            b.Property(e => e.FileKey).HasMaxLength(200).IsRequired();
            b.Property(e => e.FileName).HasMaxLength(255).IsRequired();
            b.Property(e => e.ContentType).HasMaxLength(100).IsRequired();
            b.Property(e => e.Caption).HasMaxLength(500);
            b.Property(e => e.CreatedBy).HasMaxLength(256).IsRequired();
            b.HasOne<Visit>().WithMany().HasForeignKey(e => e.VisitId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(e => e.VisitId);
        });
        modelBuilder.Entity<VisitEvent>(b =>
        {
            b.ToTable("visit_event", Schema);
            b.Property(e => e.Type).HasMaxLength(50).IsRequired();
            b.Property(e => e.Actor).HasMaxLength(256).IsRequired();
            b.Property(e => e.Comment).HasMaxLength(4000);
            b.HasOne<Visit>().WithMany().HasForeignKey(e => e.VisitId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(e => new { e.VisitId, e.At });
        });
        modelBuilder.Entity<VisitNumberSequence>(b =>
        {
            b.ToTable("number_sequence", Schema);
            b.HasKey(e => e.Key);
            b.Property(e => e.Key).HasMaxLength(100);
        });
    }
}
