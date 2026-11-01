using DynamicCube.Modules.AppsControl.Access;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Lovs;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.Lookups;
using DynamicCube.Platform.PublicApi;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Lookups;

/// <summary>
/// A class's values as a public API caller may see them (ADR-0042): those in effect today that serve the call's company,
/// and — like a person's — only those its data grants allow: each active list of values built on the class that limits the
/// account (a grant of some values, or a list that requires a grant the account lacks) keeps only what it allows. A class
/// that no list limits shows all its values. The designer's try as administrator sees every value.
/// </summary>
internal sealed class ApiClassValues(AppsControlDbContext context, ILookupDirectory lookups, ApiAccess access, GrantResolver grants)
{
    public async Task<IReadOnlyList<LookupValue>> ValuesAsync(string classCode, ApiCaller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var values = (await lookups.ValuesAsync(classCode, cancellationToken))
            .Where(v => caller.Company is null || v.Serves(caller.Company))
            .ToList();
        if (await access.SnapshotAsync(caller, cancellationToken) is not { } snapshot || values.Count == 0)
        {
            return values;
        }

        var lists = await context.Lovs.AsNoTracking()
            .Where(l => l.SourceType == LovSourceType.Class && l.IsActive && l.Class!.Code == classCode)
            .ToListAsync(cancellationToken);
        foreach (var list in lists)
        {
            var grant = await grants.ResolveAsync(list, snapshot, caller.Resource, cancellationToken);
            if (!grant.All)
            {
                values = [.. values.Where(v => grant.Values.Contains(v.Code))];
            }
        }

        return values;
    }
}
