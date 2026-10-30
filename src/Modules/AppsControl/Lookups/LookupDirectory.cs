using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.Lookups;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Lookups;

/// <summary>Class/Domain values for the other modules; each class is read once per request.</summary>
internal sealed class LookupDirectory(AppsControlDbContext context, TimeProvider clock) : ILookupDirectory
{
    private readonly Dictionary<string, IReadOnlyList<LookupValue>> _classes = new(StringComparer.Ordinal);

    public async Task<IReadOnlyList<LookupValue>> ValuesAsync(string classCode, CancellationToken cancellationToken, bool includeInactive = false)
    {
        var all = await LoadAsync(classCode, cancellationToken);
        return includeInactive ? all : [.. all.Where(v => v.IsActive)];
    }

    public async Task<LookupValue?> FindAsync(string classCode, string code, CancellationToken cancellationToken) =>
        (await LoadAsync(classCode, cancellationToken)).FirstOrDefault(v => v.Code == code);

    public async Task<IReadOnlyDictionary<string, string?>> SettingsAsync(string classCode, CancellationToken cancellationToken) =>
        (await context.Classes.AsNoTracking().Where(c => c.Code == classCode).Select(c => c.Settings).SingleOrDefaultAsync(cancellationToken) ?? [])
            .ToDictionary(s => s.Code, s => s.Value, StringComparer.Ordinal);

    private async Task<IReadOnlyList<LookupValue>> LoadAsync(string classCode, CancellationToken cancellationToken)
    {
        if (_classes.TryGetValue(classCode, out var cached))
        {
            return cached;
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var rows = await context.Domains.AsNoTracking()
            .Where(d => d.Class!.Code == classCode)
            .Include(d => d.ParentDomain)
            .Include(d => d.ExternalCodes)
            .OrderBy(d => d.DisplayOrder).ThenBy(d => d.Code)
            .ToListAsync(cancellationToken);
        var values = rows.Select(d => new LookupValue(
            d.Code,
            d.NameAr,
            d.NameEn,
            d.DetailsAr,
            d.DetailsEn,
            d.ParentDomain?.Code,
            d.DisplayOrder,
            d.IsActive && (d.EffectiveFrom == null || d.EffectiveFrom <= today) && (d.EffectiveTo == null || d.EffectiveTo >= today),
            d.IsDefault,
            JsonNode.Parse(d.Attributes) as JsonObject ?? [],
            [.. d.ExternalCodes.Select(x => new LookupExternalCode(x.ProviderCode, x.ExternalCode, x.Direction.ToString().ToUpperInvariant()))],
            d.EntityCode,
            d.BusinessApp,
            d.Companies)).ToList();
        _classes[classCode] = values;
        return values;
    }
}
