using DynamicCube.Modules.Integration.Persistence;
using DynamicCube.Platform.PublicApi;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Integration.Runtime;

/// <summary>
/// Run by the migrator (ADR-0042): every endpoint has its resource in Apps Control, and each API client set up before
/// service accounts gets one — with a role of its own (<c>API_{code}</c>) that opens the endpoints it was given, held in
/// the companies and branches it was given — so it keeps calling exactly what it called before.
/// </summary>
internal sealed class PublicApiBackfill(IntegrationDbContext db, IResourceRegistry registry, IServiceAccounts accounts, IOrganizationDirectory organization)
{
    /// <summary>Brings the endpoints' resources in line and moves earlier clients to service accounts; returns how many moved.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        foreach (var endpoint in await db.ApiEndpoints.AsNoTracking().ToListAsync(cancellationToken))
        {
            await PublicApiAccessSync.SyncAsync(registry, endpoint.Code, endpoint, cancellationToken);
        }

        var clients = await db.ApiClients.Where(c => c.PendingAccess != null).ToListAsync(cancellationToken);
        if (clients.Count == 0)
        {
            return 0;
        }

        var companies = (await organization.CompaniesAsync(cancellationToken)).Select(c => c.Code).ToList();
        var branches = await organization.BranchesAsync(cancellationToken);
        foreach (var client in clients)
        {
            await accounts.EnsureAsync(client.Account, client.NameAr, client.NameEn, cancellationToken);
            if (PublicApiAccessSync.PendingAccess.Read(client.PendingAccess) is { } pending)
            {
                await accounts.GiveRoleAsync(
                    client.Account,
                    "API_" + client.Code,
                    $"الواجهة العامة — {client.NameAr}",
                    $"Public API — {client.NameEn}",
                    [.. pending.Endpoints.Select(ApiResources.Of)],
                    Holdings(pending, companies, [.. branches.Select(b => (b.Code, b.CompanyCode))]),
                    cancellationToken);
            }

            client.PendingAccess = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return clients.Count;
    }

    /// <summary>
    /// Where the role is held: every company and branch (<c>*</c>), each chosen company with all its branches, or each
    /// company with the chosen branches of it (a company none of whose branches was chosen saw nothing and is left out).
    /// </summary>
    internal static List<ServiceAccountHolding> Holdings(PublicApiAccessSync.PendingAccess pending, IReadOnlyList<string> companies, IReadOnlyList<(string Code, string Company)> branches)
    {
        var allBranches = pending.Branches.Contains("*");
        if (pending.Companies.Contains("*") && allBranches)
        {
            return [new("*", ["*"])];
        }

        var chosen = pending.Companies.Contains("*") ? companies : [.. pending.Companies.Where(companies.Contains)];
        var holdings = new List<ServiceAccountHolding>();
        foreach (var company in chosen)
        {
            var theirs = allBranches ? ["*"] : branches.Where(b => b.Company == company && pending.Branches.Contains(b.Code)).Select(b => b.Code).ToList();
            if (theirs.Count > 0)
            {
                holdings.Add(new(company, theirs));
            }
        }

        return holdings;
    }
}
