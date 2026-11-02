using DynamicCube.Modules.Reports.Domain;
using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Reports.Persistence;

public sealed class ReportsDbContext(DbContextOptions<ReportsDbContext> options) : ModuleDbContext(options)
{
    public const string Schema = "rpt";

    public DbSet<ReportDefinition> Reports => Set<ReportDefinition>();

    public DbSet<SavedReportFilter> SavedFilters => Set<SavedReportFilter>();

    public DbSet<ReportSchedule> Schedules => Set<ReportSchedule>();

    public DbSet<ReportDelivery> Deliveries => Set<ReportDelivery>();

    public DbSet<DashboardDefinition> Dashboards => Set<DashboardDefinition>();

    public DbSet<PersonalDashboard> PersonalDashboards => Set<PersonalDashboard>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ConfigTable<ReportDefinition>(Schema, "report", b =>
        {
            b.Property(e => e.DescriptionAr).HasMaxLength(2000);
            b.Property(e => e.DescriptionEn).HasMaxLength(2000);
            b.Property(e => e.CategoryAr).HasMaxLength(200);
            b.Property(e => e.CategoryEn).HasMaxLength(200);
            b.Property(e => e.Icon).HasMaxLength(100);
            b.Property(e => e.Query).HasColumnType("jsonb").IsRequired();
            b.Property(e => e.Columns).HasJsonColumn();
            b.Property(e => e.Filters).HasJsonColumn();
            b.Property(e => e.Tiles).HasJsonColumn();
            b.Property(e => e.Sort).HasJsonColumn();
            b.Property(e => e.Charts).HasJsonColumn();
            b.Property(e => e.Details).HasJsonColumn();
        });
        modelBuilder.Entity<SavedReportFilter>(b =>
        {
            b.ToTable("saved_filter", Schema);
            b.Property(e => e.ReportCode).HasMaxLength(100).IsRequired();
            b.Property(e => e.UserName).HasMaxLength(256).IsRequired();
            b.Property(e => e.Name).HasMaxLength(200).IsRequired();
            b.Property(e => e.Values).HasJsonColumn();
            b.HasIndex(e => new { e.ReportCode, e.UserName, e.Name }).IsUnique();
        });
        modelBuilder.ConfigTable<ReportSchedule>(Schema, "schedule", b =>
        {
            b.Property(e => e.ReportCode).HasMaxLength(100).IsRequired();
            b.Property(e => e.Frequency).HasMaxLength(20).IsRequired();
            b.Property(e => e.Time).HasMaxLength(5).IsRequired();
            b.Property(e => e.Format).HasMaxLength(10).IsRequired();
            b.Property(e => e.Language).HasMaxLength(2).IsRequired();
            b.Property(e => e.Users).HasJsonColumn();
            b.Property(e => e.Roles).HasJsonColumn();
            b.Property(e => e.FilterValues).HasJsonColumn();
            b.HasIndex(e => e.ReportCode);
        });
        modelBuilder.Entity<ReportDelivery>(b =>
        {
            b.ToTable("delivery", Schema);
            b.Property(e => e.ScheduleCode).HasMaxLength(100).IsRequired();
            b.Property(e => e.ReportCode).HasMaxLength(100).IsRequired();
            b.Property(e => e.Recipients).HasJsonColumn();
            b.Property(e => e.Error).HasMaxLength(4000);
            b.HasIndex(e => new { e.ScheduleCode, e.DueAt }).IsUnique();
            b.HasIndex(e => e.StartedAt);
        });
        modelBuilder.ConfigTable<DashboardDefinition>(Schema, "dashboard", b =>
        {
            b.Property(e => e.DescriptionAr).HasMaxLength(2000);
            b.Property(e => e.DescriptionEn).HasMaxLength(2000);
            b.Property(e => e.Icon).HasMaxLength(100);
            b.Property(e => e.Filters).HasJsonColumn();
            b.Property(e => e.Widgets).HasJsonColumn();
        });
        modelBuilder.Entity<PersonalDashboard>(b =>
        {
            b.ToTable("personal_dashboard", Schema);
            b.Property(e => e.UserName).HasMaxLength(256).IsRequired();
            b.Property(e => e.Name).HasMaxLength(200).IsRequired();
            b.Property(e => e.Filters).HasJsonColumn();
            b.Property(e => e.Widgets).HasJsonColumn();
            b.HasIndex(e => new { e.UserName, e.Name }).IsUnique();
        });
    }
}
