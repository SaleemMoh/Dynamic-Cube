using DynamicCube.Platform.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DynamicCube.Platform.Persistence;

public static class ConfigModelBuilderExtensions
{
    /// <summary>Maps a configuration table: key, unique code, bilingual names, audit columns and optimistic concurrency.</summary>
    public static void ConfigTable<T>(this ModelBuilder modelBuilder, string schema, string table, Action<EntityTypeBuilder<T>>? configure = null, bool uniqueCode = true)
        where T : ConfigEntity
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<T>(b =>
        {
            b.ToTable(table, schema);
            b.HasKey(e => e.Id);
            b.Property(e => e.Code).HasMaxLength(100).IsRequired();
            if (uniqueCode)
            {
                b.HasIndex(e => e.Code).IsUnique();
            }

            b.Property(e => e.NameAr).HasMaxLength(500).IsRequired();
            b.Property(e => e.NameEn).HasMaxLength(500).IsRequired();
            b.Property(e => e.CreatedBy).HasMaxLength(128);
            b.Property(e => e.UpdatedBy).HasMaxLength(128);
            b.Property<uint>("RowVersion").IsRowVersion();
            configure?.Invoke(b);
        });
    }

    /// <summary>A <c>jsonb</c> column holding a small typed object graph.</summary>
    public static PropertyBuilder<TProperty> HasJsonColumn<TProperty>(this PropertyBuilder<TProperty> property)
        where TProperty : new()
    {
        ArgumentNullException.ThrowIfNull(property);
        return property.HasColumnType("jsonb").HasConversion(JsonColumn.Converter<TProperty>(), JsonColumn.Comparer<TProperty>());
    }
}
