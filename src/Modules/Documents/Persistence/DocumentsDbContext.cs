using DynamicCube.Modules.Documents.Domain;
using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Documents.Persistence;

public sealed class DocumentsDbContext(DbContextOptions<DocumentsDbContext> options) : ModuleDbContext(options)
{
    public const string Schema = "doc";

    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();

    public DbSet<DocumentTemplate> Templates => Set<DocumentTemplate>();

    public DbSet<IssuedDocument> Documents => Set<IssuedDocument>();

    public DbSet<DocumentVersion> Versions => Set<DocumentVersion>();

    public DbSet<DocumentEvent> Events => Set<DocumentEvent>();

    public DbSet<DocumentNumberSequence> NumberSequences => Set<DocumentNumberSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ConfigTable<DocumentTemplate>(Schema, "template", b =>
        {
            b.Property(e => e.Html).IsRequired();
            b.Property(e => e.PageSize).HasMaxLength(10).IsRequired();
        });
        modelBuilder.ConfigTable<DocumentType>(Schema, "document_type", b =>
        {
            b.Property(e => e.DescriptionAr).HasMaxLength(4000);
            b.Property(e => e.DescriptionEn).HasMaxLength(4000);
            b.Property(e => e.NumberPrefix).HasMaxLength(20).IsRequired();
            b.Property(e => e.Fields).HasColumnType("jsonb").IsRequired();
            b.Property(e => e.LineFields).HasColumnType("jsonb").IsRequired();
            b.HasOne(e => e.Template).WithMany().HasForeignKey(e => e.TemplateId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<IssuedDocument>(b =>
        {
            b.ToTable("document", Schema);
            b.Property(e => e.TypeCode).HasMaxLength(100).IsRequired();
            b.Property(e => e.Number).HasMaxLength(50).IsRequired();
            b.Property(e => e.HolderNameAr).HasMaxLength(500).IsRequired();
            b.Property(e => e.HolderNameEn).HasMaxLength(500).IsRequired();
            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.VerificationCode).HasMaxLength(40).IsRequired();
            b.Property<uint>("RowVersion").IsRowVersion();
            b.HasOne<DocumentType>().WithMany().HasForeignKey(e => e.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(e => e.Number).IsUnique();
            b.HasIndex(e => e.VerificationCode).IsUnique();
            b.HasIndex(e => new { e.HolderPartyId, e.DocumentTypeId });
            b.HasIndex(e => new { e.Status, e.ExpiresAt });
        });
        modelBuilder.Entity<DocumentVersion>(b =>
        {
            b.ToTable("version", Schema);
            b.Property(e => e.Data).HasColumnType("jsonb").IsRequired();
            b.Property(e => e.Lines).HasColumnType("jsonb").IsRequired();
            b.Property(e => e.Holder).HasColumnType("jsonb").IsRequired();
            b.Property(e => e.IssuedBy).HasMaxLength(256).IsRequired();
            b.Property(e => e.SourceType).HasMaxLength(50).IsRequired();
            b.Property(e => e.SourceNumber).HasMaxLength(50);
            b.Property(e => e.Reason).HasMaxLength(4000);
            b.Property(e => e.FileKey).HasMaxLength(500);
            b.Property(e => e.RenderError).HasMaxLength(4000);
            b.HasOne<IssuedDocument>().WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(e => new { e.DocumentId, e.Version }).IsUnique();
            b.HasIndex(e => new { e.SourceType, e.SourceId });
        });
        modelBuilder.Entity<DocumentEvent>(b =>
        {
            b.ToTable("event", Schema);
            b.Property(e => e.Type).HasMaxLength(50).IsRequired();
            b.Property(e => e.Actor).HasMaxLength(256).IsRequired();
            b.Property(e => e.Comment).HasMaxLength(4000);
            b.HasOne<IssuedDocument>().WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(e => new { e.DocumentId, e.At });
        });
        modelBuilder.Entity<DocumentNumberSequence>(b =>
        {
            b.ToTable("number_sequence", Schema);
            b.HasKey(e => e.Key);
            b.Property(e => e.Key).HasMaxLength(50);
        });
    }
}
