using DynamicCube.Modules.Payments.Domain;
using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Payments.Persistence;

public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : ModuleDbContext(options)
{
    public const string Schema = "pay";

    public DbSet<PaymentProvider> Providers => Set<PaymentProvider>();

    public DbSet<Bill> Bills => Set<Bill>();

    public DbSet<BillEvent> Events => Set<BillEvent>();

    public DbSet<BillNumberSequence> NumberSequences => Set<BillNumberSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ConfigTable<PaymentProvider>(Schema, "provider", b =>
        {
            b.Property(e => e.CreateOperation).HasMaxLength(100);
            b.Property(e => e.BillNumberOutput).HasMaxLength(100).IsRequired();
            b.Property(e => e.BillerCode).HasMaxLength(50);
            b.Property(e => e.PayUrlTemplate).HasMaxLength(500);
            b.Property(e => e.InstructionsAr).HasMaxLength(2000);
            b.Property(e => e.InstructionsEn).HasMaxLength(2000);
            b.Property(e => e.CallbackSecret).HasMaxLength(100);
            b.Property(e => e.Callback).HasJsonColumn();
        });

        modelBuilder.Entity<Bill>(b =>
        {
            b.ToTable("bill", Schema);
            b.Property(e => e.Number).HasMaxLength(50).IsRequired();
            b.Property(e => e.ProviderCode).HasMaxLength(100).IsRequired();
            b.Property(e => e.ProviderBillNumber).HasMaxLength(100);
            b.Property(e => e.SubjectType).HasMaxLength(50).IsRequired();
            b.Property(e => e.SubjectNumber).HasMaxLength(50);
            b.Property(e => e.PayerName).HasMaxLength(500).IsRequired();
            b.Property(e => e.DescriptionAr).HasMaxLength(1000).IsRequired();
            b.Property(e => e.DescriptionEn).HasMaxLength(1000).IsRequired();
            b.Property(e => e.Lines).HasJsonColumn();
            b.Property(e => e.Net).HasPrecision(18, 2);
            b.Property(e => e.Vat).HasPrecision(18, 2);
            b.Property(e => e.Total).HasPrecision(18, 2);
            b.Property(e => e.PaidAmount).HasPrecision(18, 2);
            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.PaymentReference).HasMaxLength(100);
            b.Property(e => e.CreatedBy).HasMaxLength(256).IsRequired();
            b.Property<uint>("RowVersion").IsRowVersion();
            b.HasOne<PaymentProvider>().WithMany().HasForeignKey(e => e.ProviderId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(e => e.Number).IsUnique();
            b.HasIndex(e => new { e.ProviderId, e.ProviderBillNumber }).IsUnique();
            b.HasIndex(e => new { e.SubjectType, e.SubjectId });
            b.HasIndex(e => new { e.Status, e.DueAt });
        });
        modelBuilder.Entity<BillEvent>(b =>
        {
            b.ToTable("bill_event", Schema);
            b.Property(e => e.Type).HasMaxLength(50).IsRequired();
            b.Property(e => e.Actor).HasMaxLength(256).IsRequired();
            b.Property(e => e.Comment).HasMaxLength(4000);
            b.HasOne<Bill>().WithMany().HasForeignKey(e => e.BillId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(e => new { e.BillId, e.At });
        });
        modelBuilder.Entity<BillNumberSequence>(b =>
        {
            b.ToTable("number_sequence", Schema);
            b.HasKey(e => e.Key);
            b.Property(e => e.Key).HasMaxLength(100);
        });
    }
}
