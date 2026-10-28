using DynamicCube.Modules.Parties.Domain;
using DynamicCube.Modules.Parties.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Parties.Seeding;

/// <summary>
/// Initial Party Registry configuration of a new installation: relationship types with the MCI relation codes (from the
/// MCI integration guide lookups) and a basic sanction type. Rows belong to the entity: added when missing, never
/// overwritten. Run by the migrator.
/// </summary>
public sealed class PartiesSeeder(PartiesDbContext context)
{
    private const string Mci = "MCI";

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var types = await context.RelationshipTypes.Select(t => t.Code).ToListAsync(cancellationToken);
        foreach (var type in RelationshipTypes().Where(t => !types.Contains(t.Code)))
        {
            context.RelationshipTypes.Add(type);
        }

        if (!await context.SanctionTypes.AnyAsync(cancellationToken))
        {
            context.SanctionTypes.Add(new SanctionType { Code = "APPLICATION_BAN", NameAr = "منع من التقديم", NameEn = "Ban from applying", BlocksApplications = true, DefaultDays = 180 });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<RelationshipType> RelationshipTypes()
    {
        static List<ExternalCode> Codes(params string[] codes) => [.. codes.Select(c => new ExternalCode { Provider = Mci, Code = c })];

        yield return new RelationshipType { Code = "OWNER", NameAr = "مالك", NameEn = "Owner", CanAct = true, DisplayOrder = 10, ExternalCodes = Codes("1") };
        yield return new RelationshipType { Code = "PARTNER", NameAr = "شريك", NameEn = "Partner", CanAct = true, DisplayOrder = 20, ExternalCodes = Codes("2", "3", "4", "5") };
        yield return new RelationshipType { Code = "MANAGER", NameAr = "مدير", NameEn = "Manager", CanAct = true, DisplayOrder = 30, ExternalCodes = Codes("20", "24", "26", "29", "33", "35") };
        yield return new RelationshipType { Code = "DELEGATE", NameAr = "مفوض", NameEn = "Authorized delegate", CanAct = true, DisplayOrder = 40 };
        yield return new RelationshipType
        {
            Code = "BOARD_MEMBER",
            NameAr = "عضو مجلس إدارة أو مديرين",
            NameEn = "Board member",
            CanAct = false,
            DisplayOrder = 50,
            ExternalCodes = Codes("10", "11", "12", "13", "14", "30", "31", "32", "34", "36", "37"),
        };
        yield return new RelationshipType { Code = "OTHER", NameAr = "صفة أخرى", NameEn = "Other role", CanAct = false, DisplayOrder = 90, ExternalCodes = Codes("19", "21", "22", "28", "38", "40") };
    }
}
