using DynamicCube.Modules.Parties.Domain;
using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DynamicCube.Modules.Parties.Persistence;

public sealed class PartiesDbContext(DbContextOptions<PartiesDbContext> options) : ModuleDbContext(options)
{
    public const string Schema = "party";

    private static readonly string[] ContextSourcePaths =
        ["ItemsPath", "IdentifierPath", "NameArPath", "NameEnPath", "RelationCodePath", "StatusPath", "ValidFromPath", "ValidToPath", "ServicesPath"];

    public DbSet<Party> Parties => Set<Party>();

    public DbSet<PartyIdentifier> Identifiers => Set<PartyIdentifier>();

    public DbSet<Relationship> Relationships => Set<Relationship>();

    public DbSet<ProfileValue> ProfileValues => Set<ProfileValue>();

    public DbSet<Credential> Credentials => Set<Credential>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Sanction> Sanctions => Set<Sanction>();

    public DbSet<ProfileField> ProfileFields => Set<ProfileField>();

    public DbSet<ContextSource> ContextSources => Set<ContextSource>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ConfigTable<ProfileField>(Schema, "profile_field", b =>
        {
            b.Property(e => e.OperationCode).HasMaxLength(100);
            b.Property(e => e.OutputName).HasMaxLength(100);
            b.Property(e => e.Inputs).HasJsonColumn();
        });
        modelBuilder.ConfigTable<ContextSource>(Schema, "context_source", b =>
        {
            b.Property(e => e.OperationCode).HasMaxLength(100).IsRequired();
            b.Property(e => e.InputParameter).HasMaxLength(100).IsRequired();
            foreach (var path in ContextSourcePaths)
            {
                b.Property<string>(path).HasMaxLength(200);
            }

            b.Property(e => e.PrincipalIdentifierType).HasMaxLength(50);
            b.Property(e => e.RelationProvider).HasMaxLength(100);
            b.Property(e => e.DefaultRelationshipCode).HasMaxLength(100);
            b.Property(e => e.ActiveStatuses).HasJsonColumn();
        });

        modelBuilder.Entity<Party>(b =>
        {
            b.ToTable("party", Schema);
            b.Property(e => e.NameAr).HasMaxLength(500).IsRequired();
            b.Property(e => e.NameEn).HasMaxLength(500).IsRequired();
            AuditColumns(b);
            b.HasMany(e => e.Identifiers).WithOne().HasForeignKey(i => i.PartyId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<PartyIdentifier>(b =>
        {
            b.ToTable("party_identifier", Schema);
            b.Property(e => e.Type).HasMaxLength(50).IsRequired();
            b.Property(e => e.Value).HasMaxLength(50).IsRequired();
            b.HasIndex(e => new { e.Type, e.Value }).IsUnique();
        });
        modelBuilder.Entity<Relationship>(b =>
        {
            b.ToTable("relationship", Schema);
            b.Property(e => e.Services).HasJsonColumn();
            b.Property(e => e.SourceReference).HasMaxLength(200);
            AuditColumns(b);
            b.HasOne(e => e.Individual).WithMany().HasForeignKey(e => e.IndividualId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(e => e.Principal).WithMany().HasForeignKey(e => e.PrincipalId).OnDelete(DeleteBehavior.Cascade);
            b.Property(e => e.RelationshipCode).HasMaxLength(100).IsRequired();
            b.HasIndex(e => new { e.IndividualId, e.PrincipalId });
            b.HasIndex(e => e.PrincipalId);
        });
        modelBuilder.Entity<ProfileValue>(b =>
        {
            b.ToTable("profile_value", Schema);
            b.Property(e => e.Value).HasColumnType("jsonb");
            b.HasOne(e => e.Field).WithMany().HasForeignKey(e => e.FieldId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<Party>().WithMany().HasForeignKey(e => e.PartyId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(e => new { e.PartyId, e.FieldId }).IsUnique();
        });
        modelBuilder.Entity<Credential>(b =>
        {
            b.ToTable("credential", Schema);
            b.Property(e => e.Number).HasMaxLength(100).IsRequired();
            b.Property(e => e.Data).HasColumnType("jsonb");
            AuditColumns(b);
            b.Property(e => e.CredentialTypeCode).HasMaxLength(100).IsRequired();
            b.HasOne<Party>().WithMany().HasForeignKey(e => e.PartyId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(e => new { e.PartyId, e.CredentialTypeCode, e.Number }).IsUnique();
        });
        modelBuilder.Entity<Project>(b =>
        {
            b.ToTable("project", Schema);
            b.Property(e => e.Number).HasMaxLength(100);
            b.Property(e => e.NameAr).HasMaxLength(500).IsRequired();
            b.Property(e => e.NameEn).HasMaxLength(500);
            b.Property(e => e.ClientName).HasMaxLength(500);
            b.Property(e => e.ContractValue).HasPrecision(18, 2);
            b.Property(e => e.Data).HasColumnType("jsonb");
            AuditColumns(b);
            b.HasOne<Party>().WithMany().HasForeignKey(e => e.PartyId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(e => e.PartyId);
        });
        modelBuilder.Entity<Sanction>(b =>
        {
            b.ToTable("sanction", Schema);
            b.Property(e => e.Reason).HasMaxLength(2000).IsRequired();
            b.Property(e => e.LiftedBy).HasMaxLength(128);
            b.Property(e => e.LiftReason).HasMaxLength(2000);
            AuditColumns(b);
            b.Property(e => e.SanctionTypeCode).HasMaxLength(100).IsRequired();
            b.HasOne<Party>().WithMany().HasForeignKey(e => e.PartyId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(e => e.PartyId);
        });
    }

    private static void AuditColumns<T>(EntityTypeBuilder<T> b)
        where T : class, Platform.Persistence.IHasAuditColumns
    {
        b.Property(e => e.CreatedBy).HasMaxLength(128);
        b.Property(e => e.UpdatedBy).HasMaxLength(128);
        b.Property<uint>("RowVersion").IsRowVersion();
    }
}
