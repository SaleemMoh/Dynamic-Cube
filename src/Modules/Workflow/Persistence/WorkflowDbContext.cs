using DynamicCube.Modules.Workflow.Domain;
using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Workflow.Persistence;

public sealed class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : ModuleDbContext(options)
{
    public const string Schema = "wf";

    public DbSet<WorkflowConfig> Workflows => Set<WorkflowConfig>();

    public DbSet<WorkflowVersion> Versions => Set<WorkflowVersion>();

    public DbSet<WorkflowInstance> Instances => Set<WorkflowInstance>();

    public DbSet<WorkflowToken> Tokens => Set<WorkflowToken>();

    public DbSet<WorkTask> Tasks => Set<WorkTask>();

    public DbSet<WorkflowEvent> Events => Set<WorkflowEvent>();

    public DbSet<Delegation> Delegations => Set<Delegation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ConfigTable<WorkflowConfig>(Schema, "workflow", b =>
        {
            b.Property(e => e.DescriptionAr).HasMaxLength(4000);
            b.Property(e => e.DescriptionEn).HasMaxLength(4000);
            b.Property(e => e.ServiceCode).HasMaxLength(100);
            b.HasIndex(e => e.ServiceCode).IsUnique().HasFilter("service_code IS NOT NULL");
        });
        modelBuilder.ConfigTable<WorkflowVersion>(Schema, "workflow_version", b =>
        {
            b.Property(e => e.Definition).HasColumnType("jsonb").IsRequired();
            b.HasOne(e => e.Workflow).WithMany().HasForeignKey(e => e.WorkflowId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(e => new { e.WorkflowId, e.Number }).IsUnique();
        });

        modelBuilder.Entity<WorkflowInstance>(b =>
        {
            b.ToTable("instance", Schema);
            b.Property(e => e.WorkflowCode).HasMaxLength(100).IsRequired();
            b.Property(e => e.SubjectType).HasMaxLength(50).IsRequired();
            b.Property(e => e.SubjectNumber).HasMaxLength(50).IsRequired();
            b.Property(e => e.TitleAr).HasMaxLength(500);
            b.Property(e => e.TitleEn).HasMaxLength(500);
            b.Property(e => e.PartyNameAr).HasMaxLength(500);
            b.Property(e => e.PartyNameEn).HasMaxLength(500);
            b.Property(e => e.ServiceCode).HasMaxLength(100);
            b.Property(e => e.CompanyCode).HasMaxLength(100);
            b.Property(e => e.BranchCode).HasMaxLength(100);
            b.Property(e => e.LevelCode).HasMaxLength(100);
            b.Property(e => e.Outcome).HasMaxLength(50);
            b.Property(e => e.Facts).HasColumnType("jsonb").IsRequired();
            b.Property<uint>("RowVersion").IsRowVersion();
            b.HasOne<WorkflowVersion>().WithMany().HasForeignKey(e => e.VersionId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(e => new { e.SubjectType, e.SubjectId }).IsUnique();
            b.HasIndex(e => new { e.Status, e.DueAt });
        });
        modelBuilder.Entity<WorkflowToken>(b =>
        {
            b.ToTable("token", Schema);
            b.Property(e => e.NodeCode).HasMaxLength(100).IsRequired();
            b.Property(e => e.ResumeAt).HasMaxLength(100);
            b.Property(e => e.Forks).HasJsonColumn();
            b.HasOne<WorkflowInstance>().WithMany().HasForeignKey(e => e.InstanceId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(e => new { e.InstanceId, e.Status });
        });
        modelBuilder.Entity<WorkTask>(b =>
        {
            b.ToTable("task", Schema);
            b.Property(e => e.NodeCode).HasMaxLength(100).IsRequired();
            b.Property(e => e.NameAr).HasMaxLength(500);
            b.Property(e => e.NameEn).HasMaxLength(500);
            b.Property(e => e.CandidateRole).HasMaxLength(100);
            b.Property(e => e.CandidateOrgUnit).HasMaxLength(100);
            b.Property(e => e.CandidatePosition).HasMaxLength(100);
            b.Property(e => e.CandidateOrgUnits).HasDefaultValueSql("'{}'::text[]");
            b.Property(e => e.CandidateUser).HasMaxLength(256);
            b.Property(e => e.DistributorRole).HasMaxLength(100);
            b.Property(e => e.Assignee).HasMaxLength(256);
            b.Property(e => e.AssignedBy).HasMaxLength(256);
            b.Property(e => e.EscalatedToRole).HasMaxLength(100);
            b.Property(e => e.CompletedBy).HasMaxLength(256);
            b.Property(e => e.ActionCode).HasMaxLength(100);
            b.Property(e => e.Comment).HasMaxLength(4000);
            b.Property(e => e.SubjectType).HasMaxLength(50).IsRequired();
            b.Property(e => e.SubjectNumber).HasMaxLength(50).IsRequired();
            b.Property(e => e.TitleAr).HasMaxLength(500);
            b.Property(e => e.TitleEn).HasMaxLength(500);
            b.Property(e => e.PartyNameAr).HasMaxLength(500);
            b.Property(e => e.PartyNameEn).HasMaxLength(500);
            b.Property(e => e.ServiceCode).HasMaxLength(100);
            b.Property(e => e.CompanyCode).HasMaxLength(100);
            b.Property(e => e.BranchCode).HasMaxLength(100);
            b.Property(e => e.LevelCode).HasMaxLength(100);
            b.Property<uint>("RowVersion").IsRowVersion();
            b.HasOne<WorkflowInstance>().WithMany().HasForeignKey(e => e.InstanceId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(e => new { e.Status, e.Assignee });
            b.HasIndex(e => new { e.Status, e.CandidateRole });
            b.HasIndex(e => new { e.Status, e.DueAt });
            b.HasIndex(e => e.InstanceId);
        });
        modelBuilder.Entity<WorkflowEvent>(b =>
        {
            b.ToTable("event", Schema);
            b.Property(e => e.Actor).HasMaxLength(256).IsRequired();
            b.Property(e => e.OnBehalfOf).HasMaxLength(256);
            b.Property(e => e.Type).HasMaxLength(50).IsRequired();
            b.Property(e => e.NodeCode).HasMaxLength(100);
            b.Property(e => e.ActionCode).HasMaxLength(100);
            b.Property(e => e.Target).HasMaxLength(256);
            b.Property(e => e.Comment).HasMaxLength(4000);
            b.HasOne<WorkflowInstance>().WithMany().HasForeignKey(e => e.InstanceId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(e => new { e.InstanceId, e.At });
        });
        modelBuilder.Entity<Delegation>(b =>
        {
            b.ToTable("delegation", Schema);
            b.Property(e => e.Delegator).HasMaxLength(256).IsRequired();
            b.Property(e => e.Delegate).HasMaxLength(256).IsRequired();
            b.Property(e => e.Reason).HasMaxLength(1000);
            b.Property(e => e.CreatedBy).HasMaxLength(128);
            b.Property(e => e.UpdatedBy).HasMaxLength(128);
            b.HasIndex(e => new { e.Delegate, e.FromDate, e.ToDate });
        });
    }
}
