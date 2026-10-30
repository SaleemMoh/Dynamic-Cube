using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DynamicCube.Modules.AppsControl.Persistence;

public sealed class AppsControlDbContext(DbContextOptions<AppsControlDbContext> options) : ModuleDbContext(options)
{
    public const string Schema = "apps";

    public DbSet<Installation> Installations => Set<Installation>();

    public DbSet<Holiday> Holidays => Set<Holiday>();

    public DbSet<GovernmentEntity> Entities => Set<GovernmentEntity>();

    public DbSet<App> Apps => Set<App>();

    public DbSet<BusinessApplication> BusinessApps => Set<BusinessApplication>();

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<Branch> Branches => Set<Branch>();

    public DbSet<OrgUnitType> OrgUnitTypes => Set<OrgUnitType>();

    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();

    public DbSet<Position> Positions => Set<Position>();

    public DbSet<UserAccount> Users => Set<UserAccount>();

    public DbSet<UserAssignment> UserAssignments => Set<UserAssignment>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<UserPrivilege> UserPrivileges => Set<UserPrivilege>();

    public DbSet<Resource> Resources => Set<Resource>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Privilege> Privileges => Set<Privilege>();

    public DbSet<RoleMenu> RoleMenus => Set<RoleMenu>();

    public DbSet<MenuGroup> MenuGroups => Set<MenuGroup>();

    public DbSet<Menu> Menus => Set<Menu>();

    public DbSet<LookupClass> Classes => Set<LookupClass>();

    public DbSet<AttributeDefinition> AttributeDefinitions => Set<AttributeDefinition>();

    public DbSet<DomainValue> Domains => Set<DomainValue>();

    public DbSet<DomainExternalCode> DomainExternalCodes => Set<DomainExternalCode>();

    public DbSet<DataObject> DataObjects => Set<DataObject>();

    public DbSet<DataScopeBinding> DataScopeBindings => Set<DataScopeBinding>();

    public DbSet<Lov> Lovs => Set<Lov>();

    public DbSet<LovBinding> LovBindings => Set<LovBinding>();

    public DbSet<LovGrant> LovGrants => Set<LovGrant>();

    public DbSet<Setting> Settings => Set<Setting>();

    public DbSet<UiText> UiTexts => Set<UiText>();

    public DbSet<ChangeArea> ChangeAreas => Set<ChangeArea>();

    public DbSet<BrandingAsset> BrandingAssets => Set<BrandingAsset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("ltree");

        Config<Installation>(modelBuilder, "installation");
        Config<Holiday>(modelBuilder, "holiday");
        Config<ChangeArea>(modelBuilder, "change_area");
        Config<BusinessApplication>(modelBuilder, "business_app", b =>
        {
            b.Property(e => e.Icon).HasMaxLength(100);
            b.Property(e => e.DescriptionAr).HasMaxLength(2000);
            b.Property(e => e.DescriptionEn).HasMaxLength(2000);
            b.Property(e => e.BaseUrl).HasMaxLength(500);
            b.Property(e => e.Channels).HasColumnType("text[]");
            b.Property(e => e.Companies).HasColumnType("text[]");
            b.Property(e => e.Branches).HasColumnType("text[]");
        });
        Config<Company>(modelBuilder, "company", b =>
        {
            b.Property(e => e.CrNumber).HasMaxLength(50);
            b.Property(e => e.UnifiedNumber).HasMaxLength(50);
            b.Property(e => e.VatNumber).HasMaxLength(50);
        });
        Config<Branch>(modelBuilder, "branch", b =>
        {
            b.Property(e => e.CompanyCode).HasMaxLength(100);
            b.Property(e => e.OrgUnitCode).HasMaxLength(100);
            b.Property(e => e.RegionCode).HasMaxLength(100);
            b.Property(e => e.City).HasMaxLength(200);
            b.HasIndex(e => e.CompanyCode);
        });
        Config<GovernmentEntity>(modelBuilder, "entity", b =>
        {
            b.Property(e => e.FooterLinks).HasColumnType("jsonb").HasConversion(JsonColumn.Converter<List<FooterLink>>(), JsonColumn.Comparer<List<FooterLink>>());
            b.Property(e => e.InternalThemeColor).HasMaxLength(7);
            b.Property(e => e.PortalThemeColor).HasMaxLength(7);
            b.Property(e => e.FooterImageKey).HasMaxLength(100);
            b.Property(e => e.LoginImageKey).HasMaxLength(100);
            b.Property(e => e.PortalHeroImageKey).HasMaxLength(100);
            b.Property(e => e.Contact).HasColumnType("jsonb").HasConversion(JsonColumn.Converter<EntityContact>(), JsonColumn.Comparer<EntityContact>());
        });
        modelBuilder.Entity<BrandingAsset>(b =>
        {
            b.ToTable("branding_asset", Schema);
            b.Property(e => e.FileName).HasMaxLength(255);
            b.Property(e => e.ContentType).HasMaxLength(100);
            b.Property(e => e.Sha256).HasMaxLength(64);
            b.Property(e => e.CreatedBy).HasMaxLength(128);
            b.Property(e => e.UpdatedBy).HasMaxLength(128);
        });
        Config<App>(modelBuilder, "app");

        Config<OrgUnitType>(modelBuilder, "org_unit_type");
        Config<OrgUnit>(modelBuilder, "org_unit", b =>
        {
            b.Property(e => e.Path).HasColumnType("ltree");
            b.HasIndex(e => e.Path).HasMethod("gist");
            b.Property(e => e.Attributes).HasColumnType("jsonb");
            b.HasOne(e => e.OrgUnitType).WithMany().HasForeignKey(e => e.OrgUnitTypeId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(e => e.Parent).WithMany().HasForeignKey(e => e.ParentId).OnDelete(DeleteBehavior.Restrict);
            b.Property(e => e.DefaultRoles).HasColumnType("text[]");
        });
        Config<Position>(modelBuilder, "position");

        Config<UserAccount>(modelBuilder, "user_account", b =>
        {
            b.Property(e => e.Code).HasMaxLength(256);
            b.HasIndex(e => e.IdpSubject).IsUnique();
            b.Property(e => e.IdpSubject).HasMaxLength(128);
            b.Property(e => e.AuthSource).HasMaxLength(32);
            b.Property(e => e.Email).HasMaxLength(256);
            b.Property(e => e.Mobile).HasMaxLength(32);
            b.Property(e => e.NationalId).HasMaxLength(32);
            b.Property(e => e.PreferredLanguage).HasMaxLength(8);
            b.HasMany(e => e.Assignments).WithOne().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(e => e.Roles).WithOne().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<UserAssignment>(b =>
        {
            b.ToTable("user_assignment", Schema);
            b.HasOne(e => e.OrgUnit).WithMany().HasForeignKey(e => e.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(e => e.Position).WithMany().HasForeignKey(e => e.PositionId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<UserRole>(b =>
        {
            b.ToTable("user_role", Schema);
            b.HasOne(e => e.Role).WithMany().HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(e => e.OrgUnit).WithMany().HasForeignKey(e => e.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
            b.Property(e => e.CompanyCode).HasMaxLength(100);
            b.Property(e => e.Branches).HasColumnType("text[]");
        });
        modelBuilder.Entity<UserPrivilege>(b =>
        {
            b.ToTable("user_privilege", Schema);
            b.HasOne<UserAccount>().WithMany(u => u.Privileges).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(e => e.Resource).WithMany().HasForeignKey(e => e.ResourceId).OnDelete(DeleteBehavior.Restrict);
            b.Property(e => e.CompanyCode).HasMaxLength(100);
            b.Property(e => e.Actions).HasColumnType("text[]");
        });

        Config<Resource>(modelBuilder, "resource", b =>
        {
            b.HasOne(e => e.App).WithMany().HasForeignKey(e => e.AppId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(e => e.Parent).WithMany().HasForeignKey(e => e.ParentId).OnDelete(DeleteBehavior.Restrict);
        });
        Config<Role>(modelBuilder, "role", b =>
        {
            b.Property(e => e.ReferenceCode).HasMaxLength(100);
            b.HasOne(e => e.App).WithMany().HasForeignKey(e => e.AppId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(e => e.Privileges).WithOne().HasForeignKey(p => p.RoleId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(e => e.Grants).WithOne().HasForeignKey(g => g.RoleId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(e => e.Menus).WithOne().HasForeignKey(m => m.RoleId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<RoleMenu>(b =>
        {
            b.ToTable("role_menu", Schema);
            b.HasIndex(e => new { e.RoleId, e.MenuId }).IsUnique();

            // A deleted menu item simply leaves the roles it was assigned to.
            b.HasOne(e => e.Menu).WithMany().HasForeignKey(e => e.MenuId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Privilege>(b =>
        {
            b.ToTable("privilege", Schema);
            b.HasIndex(e => new { e.RoleId, e.ResourceId }).IsUnique();
            b.Property(e => e.FieldRules).HasColumnType("jsonb");
            b.HasOne(e => e.Resource).WithMany().HasForeignKey(e => e.ResourceId).OnDelete(DeleteBehavior.Restrict);
        });

        Config<MenuGroup>(modelBuilder, "menu_group", b =>
        {
            b.Property(e => e.Icon).HasMaxLength(100);
            b.HasOne(e => e.App).WithMany().HasForeignKey(e => e.AppId).OnDelete(DeleteBehavior.Restrict);
        });
        Config<Menu>(modelBuilder, "menu", b =>
        {
            b.Property(e => e.Target).HasMaxLength(1000);
            b.Property(e => e.Icon).HasMaxLength(4000);
            b.HasOne(e => e.App).WithMany().HasForeignKey(e => e.AppId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(e => e.MenuGroup).WithMany().HasForeignKey(e => e.MenuGroupId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(e => e.Parent).WithMany().HasForeignKey(e => e.ParentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(e => e.Resource).WithMany().HasForeignKey(e => e.ResourceId).OnDelete(DeleteBehavior.Restrict);
        });

        Config<LookupClass>(modelBuilder, "class", b =>
        {
            b.HasOne(e => e.ParentClass).WithMany().HasForeignKey(e => e.ParentClassId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(e => e.Attributes).WithOne().HasForeignKey(a => a.ClassId).OnDelete(DeleteBehavior.Cascade);
            b.Property(e => e.Settings).HasColumnType("jsonb").HasConversion(JsonColumn.Converter<List<ClassSetting>>(), JsonColumn.Comparer<List<ClassSetting>>());
        });
        modelBuilder.Entity<AttributeDefinition>(b =>
        {
            b.ToTable("class_attribute", Schema);
            b.Property(e => e.Code).HasMaxLength(63).IsRequired();
            b.HasIndex(e => new { e.ClassId, e.Code }).IsUnique();
            b.Property(e => e.NameAr).HasMaxLength(500);
            b.Property(e => e.NameEn).HasMaxLength(500);
            b.Property(e => e.RefClassCode).HasMaxLength(100);
            b.Property(e => e.RefLovCode).HasMaxLength(100);
            b.Property(e => e.Validation).HasColumnType("jsonb");
        });
        Config<DomainValue>(modelBuilder, "domain", b =>
        {
            b.HasIndex(e => new { e.ClassId, e.Code }).IsUnique();
            b.Property(e => e.Attributes).HasColumnType("jsonb");
            b.HasIndex(e => e.Attributes).HasMethod("gin");
            b.HasOne(e => e.Class).WithMany().HasForeignKey(e => e.ClassId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(e => e.ParentDomain).WithMany().HasForeignKey(e => e.ParentDomainId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(e => e.ExternalCodes).WithOne().HasForeignKey(x => x.DomainId).OnDelete(DeleteBehavior.Cascade);
        }, uniqueCode: false);
        modelBuilder.Entity<DomainExternalCode>(b =>
        {
            b.ToTable("domain_external_code", Schema);
            b.Property(e => e.ProviderCode).HasMaxLength(100).IsRequired();
            b.Property(e => e.ExternalCode).HasMaxLength(200).IsRequired();
            b.HasIndex(e => new { e.ProviderCode, e.ExternalCode, e.Direction });
            b.HasIndex(e => new { e.DomainId, e.ProviderCode, e.Direction }).IsUnique();
        });

        Config<DataObject>(modelBuilder, "data_object", b =>
        {
            b.Property(e => e.DbSchema).HasMaxLength(63).IsRequired();
            b.Property(e => e.DbObject).HasMaxLength(63).IsRequired();
            b.Property(e => e.Columns).HasColumnType("jsonb").HasConversion(JsonColumn.Converter<List<DataObjectColumn>>(), JsonColumn.Comparer<List<DataObjectColumn>>());
            b.HasMany(e => e.ScopeBindings).WithOne().HasForeignKey(s => s.DataObjectId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<DataScopeBinding>(b =>
        {
            b.ToTable("data_scope_binding", Schema);
            b.Property(e => e.ColumnName).HasMaxLength(63).IsRequired();
            b.HasOne(e => e.Lov).WithMany().HasForeignKey(e => e.LovId).OnDelete(DeleteBehavior.Restrict);
        });
        Config<Lov>(modelBuilder, "lov", b =>
        {
            b.Property(e => e.IntegrationOperationCode).HasMaxLength(100);
            b.Property(e => e.ValueColumn).HasMaxLength(63);
            b.Property(e => e.DisplayArColumn).HasMaxLength(63);
            b.Property(e => e.DisplayEnColumn).HasMaxLength(63);
            b.Property(e => e.ParentColumn).HasMaxLength(63);
            b.Property(e => e.StaticFilter).HasColumnType("jsonb");
            b.HasOne(e => e.Class).WithMany().HasForeignKey(e => e.ClassId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(e => e.DataObject).WithMany().HasForeignKey(e => e.DataObjectId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(e => e.Bindings).WithOne().HasForeignKey(x => x.LovId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<LovBinding>(b =>
        {
            b.ToTable("lov_binding", Schema);
            b.Property(e => e.FieldCode).HasMaxLength(100).IsRequired();
            b.HasIndex(e => new { e.ResourceId, e.FieldCode }).IsUnique();
            b.HasOne(e => e.Resource).WithMany().HasForeignKey(e => e.ResourceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<LovGrant>(b =>
        {
            b.ToTable("lov_grant", Schema);
            b.HasIndex(e => new { e.RoleId, e.LovId, e.ResourceId }).IsUnique().AreNullsDistinct(false);
            b.Property(e => e.Rule).HasColumnType("jsonb");
            b.Property(e => e.DefaultValue).HasMaxLength(200);
            b.HasOne(e => e.Lov).WithMany().HasForeignKey(e => e.LovId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(e => e.Resource).WithMany().HasForeignKey(e => e.ResourceId).OnDelete(DeleteBehavior.Restrict);
        });

        Config<Setting>(modelBuilder, "setting", b =>
        {
            b.Property(e => e.Value).HasColumnType("jsonb");
            b.HasIndex(e => new { e.Code, e.AppId }).IsUnique().AreNullsDistinct(false);
            b.HasOne(e => e.App).WithMany().HasForeignKey(e => e.AppId).OnDelete(DeleteBehavior.Restrict);
        }, uniqueCode: false);
        Config<UiText>(modelBuilder, "ui_text", b =>
        {
            b.Property(e => e.NameAr).HasMaxLength(4000);
            b.Property(e => e.NameEn).HasMaxLength(4000);
            b.HasIndex(e => new { e.Code, e.AppId }).IsUnique().AreNullsDistinct(false);
            b.HasOne(e => e.App).WithMany().HasForeignKey(e => e.AppId).OnDelete(DeleteBehavior.Restrict);
        }, uniqueCode: false);
    }

    private static void Config<T>(ModelBuilder modelBuilder, string table, Action<EntityTypeBuilder<T>>? configure = null, bool uniqueCode = true)
        where T : ConfigEntity =>
        modelBuilder.ConfigTable(Schema, table, configure, uniqueCode);
}
