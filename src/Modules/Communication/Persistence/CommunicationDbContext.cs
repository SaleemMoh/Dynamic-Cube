using DynamicCube.Modules.Communication.Domain;
using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Communication.Persistence;

public sealed class CommunicationDbContext(DbContextOptions<CommunicationDbContext> options) : ModuleDbContext(options)
{
    public const string Schema = "com";

    public DbSet<MessageTemplate> Templates => Set<MessageTemplate>();

    public DbSet<NotificationRule> Rules => Set<NotificationRule>();

    public DbSet<OutboundMessage> Messages => Set<OutboundMessage>();

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ConfigTable<MessageTemplate>(Schema, "template", b =>
        {
            b.Property(e => e.EmailSubjectAr).HasMaxLength(1000);
            b.Property(e => e.EmailSubjectEn).HasMaxLength(1000);
            b.Property(e => e.SmsAr).HasMaxLength(2000);
            b.Property(e => e.SmsEn).HasMaxLength(2000);
            b.Property(e => e.InAppTitleAr).HasMaxLength(1000);
            b.Property(e => e.InAppTitleEn).HasMaxLength(1000);
            b.Property(e => e.InAppBodyAr).HasMaxLength(4000);
            b.Property(e => e.InAppBodyEn).HasMaxLength(4000);
            b.Property(e => e.Link).HasMaxLength(1000);
        });
        modelBuilder.ConfigTable<NotificationRule>(Schema, "rule", b =>
        {
            b.Property(e => e.EventCode).HasMaxLength(100).IsRequired();
            b.Property(e => e.Condition).HasColumnType("jsonb");
            b.Property(e => e.Recipients).HasColumnType("jsonb").IsRequired();
            b.Property(e => e.Language).HasMaxLength(2).IsRequired();
            b.HasOne(e => e.Template).WithMany().HasForeignKey(e => e.TemplateId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(e => e.EventCode);
        });

        modelBuilder.Entity<OutboundMessage>(b =>
        {
            b.ToTable("message", Schema);
            b.Property(e => e.EventCode).HasMaxLength(100).IsRequired();
            b.Property(e => e.RuleCode).HasMaxLength(100).IsRequired();
            b.Property(e => e.Channel).HasMaxLength(20).IsRequired();
            b.Property(e => e.RecipientKind).HasMaxLength(20).IsRequired();
            b.Property(e => e.Recipient).HasMaxLength(256).IsRequired();
            b.Property(e => e.RecipientName).HasMaxLength(500);
            b.Property(e => e.Address).HasMaxLength(320);
            b.Property(e => e.SubjectType).HasMaxLength(50).IsRequired();
            b.Property(e => e.SubjectNumber).HasMaxLength(50);
            b.Property(e => e.Title).HasMaxLength(1000);
            b.Property(e => e.Body).IsRequired();
            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.Error).HasMaxLength(4000);
            b.HasIndex(e => new { e.EventId, e.RuleCode, e.Channel, e.Recipient }).IsUnique();
            b.HasIndex(e => new { e.SubjectType, e.SubjectId });
            b.HasIndex(e => new { e.Status, e.CreatedAt });
        });
        modelBuilder.Entity<Notification>(b =>
        {
            b.ToTable("notification", Schema);
            b.Property(e => e.UserName).HasMaxLength(256);
            b.Property(e => e.EventCode).HasMaxLength(100).IsRequired();
            b.Property(e => e.TitleAr).HasMaxLength(1000).IsRequired();
            b.Property(e => e.TitleEn).HasMaxLength(1000).IsRequired();
            b.Property(e => e.BodyAr).HasMaxLength(4000).IsRequired();
            b.Property(e => e.BodyEn).HasMaxLength(4000).IsRequired();
            b.Property(e => e.Link).HasMaxLength(1000);
            b.Property(e => e.SubjectType).HasMaxLength(50).IsRequired();
            b.Property(e => e.SubjectNumber).HasMaxLength(50);
            b.HasOne<OutboundMessage>().WithMany().HasForeignKey(e => e.MessageId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(e => new { e.PartyId, e.CreatedAt });
            b.HasIndex(e => new { e.UserName, e.CreatedAt });
        });
    }
}
