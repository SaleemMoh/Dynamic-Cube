using DynamicCube.Modules.Content.Domain;
using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Content.Persistence;

public sealed class ContentDbContext(DbContextOptions<ContentDbContext> options) : ModuleDbContext(options)
{
    public const string Schema = "cms";

    public DbSet<ContentPage> Pages => Set<ContentPage>();

    public DbSet<Faq> Faqs => Set<Faq>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ConfigTable<ContentPage>(Schema, "page", b =>
        {
            b.Property(e => e.BodyAr).IsRequired();
            b.Property(e => e.BodyEn).IsRequired();
        });
        modelBuilder.ConfigTable<Faq>(Schema, "faq", b =>
        {
            b.Property(e => e.AnswerAr).HasMaxLength(10_000).IsRequired();
            b.Property(e => e.AnswerEn).HasMaxLength(10_000).IsRequired();
            b.Property(e => e.TopicAr).HasMaxLength(200);
            b.Property(e => e.TopicEn).HasMaxLength(200);
            b.Property(e => e.ServiceCode).HasMaxLength(100);
            b.HasIndex(e => e.ServiceCode);
        });
    }
}
