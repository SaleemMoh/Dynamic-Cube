using DynamicCube.Modules.Decisions.Domain;
using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Decisions.Persistence;

public sealed class DecisionsDbContext(DbContextOptions<DecisionsDbContext> options) : ModuleDbContext(options)
{
    public const string Schema = "dec";

    public DbSet<RuleSet> RuleSets => Set<RuleSet>();

    public DbSet<RuleSetVersion> Versions => Set<RuleSetVersion>();

    public DbSet<Evaluation> Evaluations => Set<Evaluation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ConfigTable<RuleSet>(Schema, "rule_set", b =>
        {
            b.Property(e => e.DescriptionAr).HasMaxLength(4000);
            b.Property(e => e.DescriptionEn).HasMaxLength(4000);
            b.Property(e => e.Engine).HasMaxLength(50).IsRequired();
        });
        modelBuilder.ConfigTable<RuleSetVersion>(Schema, "rule_set_version", b =>
        {
            b.Property(e => e.Configuration).HasColumnType("jsonb").IsRequired();
            b.HasOne(e => e.RuleSet).WithMany().HasForeignKey(e => e.RuleSetId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(e => new { e.RuleSetId, e.Number }).IsUnique();
        });
        modelBuilder.Entity<Evaluation>(b =>
        {
            b.ToTable("evaluation", Schema);
            b.Property(e => e.RuleSetCode).HasMaxLength(100).IsRequired();
            b.Property(e => e.Engine).HasMaxLength(50).IsRequired();
            b.Property(e => e.SubjectType).HasMaxLength(50).IsRequired();
            b.Property(e => e.SubjectNumber).HasMaxLength(50);
            b.Property(e => e.Input).HasColumnType("jsonb").IsRequired();
            b.Property(e => e.Output).HasColumnType("jsonb").IsRequired();
            b.Property(e => e.Trace).HasColumnType("jsonb").IsRequired();
            b.Property(e => e.By).HasMaxLength(256).IsRequired();
            b.HasIndex(e => new { e.SubjectType, e.SubjectId });
            b.HasIndex(e => new { e.RuleSetCode, e.At });
        });
    }
}
