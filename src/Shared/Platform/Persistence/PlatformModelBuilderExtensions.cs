using DynamicCube.Platform.Auditing;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Outbox;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Platform.Persistence;

internal static class PlatformModelBuilderExtensions
{
    public static void ApplyPlatformModel(this ModelBuilder modelBuilder, bool excludeFromMigrations)
    {
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox_messages", PlatformSchemas.Platform, t => t.ExcludeFromMigrations(excludeFromMigrations));
            b.HasKey(m => m.Id);
            b.Property(m => m.Type).HasMaxLength(200).IsRequired();
            b.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
            b.Property(m => m.CorrelationId).HasMaxLength(64);
            b.HasIndex(m => new { m.AvailableAt, m.OccurredAt })
                .HasFilter("processed_at IS NULL AND dead_lettered_at IS NULL")
                .HasDatabaseName("ix_outbox_messages_pending");
        });

        modelBuilder.Entity<AuditEntry>(b =>
        {
            b.ToTable("audit_log", PlatformSchemas.Audit, t => t.ExcludeFromMigrations(excludeFromMigrations));
            b.HasKey(a => a.Id);
            b.Property(a => a.UserId).HasMaxLength(128).IsRequired();
            b.Property(a => a.AppCode).HasMaxLength(64).IsRequired();
            b.Property(a => a.EntityType).HasMaxLength(200).IsRequired();
            b.Property(a => a.EntityId).HasMaxLength(200).IsRequired();
            b.Property(a => a.Changes).HasColumnType("jsonb").IsRequired();
            b.Property(a => a.CorrelationId).HasMaxLength(64);
            b.HasIndex(a => new { a.EntityType, a.EntityId, a.OccurredAt });
            b.HasIndex(a => a.OccurredAt);
        });

        modelBuilder.Entity<ChangeSet>(b =>
        {
            b.ToTable("change_sets", PlatformSchemas.Config, t => t.ExcludeFromMigrations(excludeFromMigrations));
            b.HasKey(c => c.Id);
            b.Property(c => c.Area).HasMaxLength(100).IsRequired();
            b.Property(c => c.Title).HasMaxLength(300).IsRequired();
            b.Property(c => c.CreatedBy).HasMaxLength(128).IsRequired();
            b.Property(c => c.SubmittedBy).HasMaxLength(128);
            b.Property(c => c.ReviewedBy).HasMaxLength(128);
            b.Property(c => c.PublishedBy).HasMaxLength(128);
            b.Property(c => c.ReviewComment).HasMaxLength(2000);
            b.HasIndex(c => new { c.Area, c.Status });
            b.HasMany(c => c.Items).WithOne().HasForeignKey(i => i.ChangeSetId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(c => c.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ChangeItem>(b =>
        {
            b.ToTable("change_items", PlatformSchemas.Config, t => t.ExcludeFromMigrations(excludeFromMigrations));
            b.HasKey(i => i.Id);
            b.Property(i => i.EntityType).HasMaxLength(100).IsRequired();
            b.Property(i => i.EntityCode).HasMaxLength(200).IsRequired();
            b.Property(i => i.Before).HasColumnType("jsonb");
            b.Property(i => i.After).HasColumnType("jsonb");
            b.HasIndex(i => new { i.ChangeSetId, i.Sequence }).IsUnique();
        });
    }
}
