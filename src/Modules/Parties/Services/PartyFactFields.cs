using DynamicCube.Modules.Parties.Persistence;
using DynamicCube.Platform.Parties;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Parties.Services;

/// <summary>A party's names, kind, identifiers and profile fields, as paths in a request's facts.</summary>
internal sealed class PartyFactFields(PartiesDbContext context) : IPartyFactFields
{
    public async Task<IReadOnlyList<FactField>> ListAsync(CancellationToken cancellationToken)
    {
        List<FactField> fields =
        [
            new("party.nameAr", "اسم المستفيد بالعربية", "Beneficiary name (Arabic)"),
            new("party.nameEn", "اسم المستفيد بالإنجليزية", "Beneficiary name (English)"),
            new("party.kind", "نوع المستفيد (فرد أو منشأة)", "Beneficiary kind (individual or establishment)"),
            new("party.identifiers.NATIONAL_ID", "رقم الهوية الوطنية", "National ID"),
            new("party.identifiers.IQAMA", "رقم الإقامة", "Iqama number"),
            new("party.identifiers.GCC_ID", "هوية خليجية", "GCC ID"),
            new("party.identifiers.PASSPORT", "رقم الجواز", "Passport number"),
            new("party.identifiers.CR_NUMBER", "رقم السجل التجاري", "Commercial registration number"),
            new("party.identifiers.UNIFIED_NUMBER", "الرقم الموحد (700)", "Unified number (700)"),
        ];
        var profile = await context.ProfileFields.AsNoTracking().Where(f => f.IsActive).OrderBy(f => f.DisplayOrder).ThenBy(f => f.Code)
            .Select(f => new { f.Code, f.NameAr, f.NameEn }).ToListAsync(cancellationToken);
        fields.AddRange(profile.Select(f => new FactField($"party.profile.{f.Code}", f.NameAr, f.NameEn)));
        return fields;
    }
}
