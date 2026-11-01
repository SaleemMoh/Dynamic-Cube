using System.Text.Json;
using DynamicCube.Modules.Integration.Changes;
using DynamicCube.Modules.Integration.Domain;
using DynamicCube.Modules.Integration.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.PublicApi;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Integration.Runtime;

/// <summary>
/// In the change set's transaction (ADR-0042): each published endpoint is a resource of Apps Control
/// (<c>integration.api.{code}</c>, VIEW) under the public API's endpoints, which roles give to the service accounts that may
/// call it; a deleted or deactivated endpoint's resource is deactivated. Each published API client's service account is
/// created when it does not exist yet (with no roles).
/// </summary>
internal sealed class PublicApiAccessSync(IResourceRegistry registry, IServiceAccounts accounts) : IChangeSetPublishHook<IntegrationDbContext>
{
    public async Task OnPublishedAsync(IntegrationDbContext context, ChangeSet changeSet, CancellationToken cancellationToken)
    {
        var endpoints = changeSet.Items.Where(i => i.EntityType == IntegrationChangeTypes.ApiEndpoint).Select(i => i.EntityCode).Distinct(StringComparer.Ordinal).ToList();
        if (endpoints.Count > 0)
        {
            var rows = await context.ApiEndpoints.AsNoTracking().Where(e => endpoints.Contains(e.Code)).ToDictionaryAsync(e => e.Code, cancellationToken);
            foreach (var code in endpoints)
            {
                await SyncAsync(registry, code, rows.GetValueOrDefault(code), cancellationToken);
            }
        }

        var clients = changeSet.Items.Where(i => i.EntityType == IntegrationChangeTypes.ApiClient && i.Operation != ChangeOperation.Delete)
            .Select(i => i.EntityCode).Distinct(StringComparer.Ordinal).ToList();
        if (clients.Count > 0)
        {
            foreach (var client in await context.ApiClients.AsNoTracking().Where(c => clients.Contains(c.Code)).ToListAsync(cancellationToken))
            {
                await accounts.EnsureAsync(client.Account, client.NameAr, client.NameEn, cancellationToken);
            }
        }
    }

    /// <summary>The endpoint's resource: kept in line while the endpoint is active, deactivated otherwise.</summary>
    internal static Task SyncAsync(IResourceRegistry registry, string code, ApiEndpoint? endpoint, CancellationToken cancellationToken) =>
        endpoint is { IsActive: true }
            ? registry.SaveAsync(
                new CatalogResource(ApiResources.Of(code), endpoint.NameAr, endpoint.NameEn, "INTERNAL", ApiResources.Root, [CatalogActions.View], CatalogResourceTypes.Api),
                cancellationToken)
            : registry.RetireAsync(ApiResources.Of(code), cancellationToken);

    /// <summary>What an API client was given before it had a service account (moved once to the account's own role).</summary>
    internal sealed record PendingAccess(List<string> Endpoints, List<string> Companies, List<string> Branches)
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        public static PendingAccess? Read(string? json) =>
            string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<PendingAccess>(json, Json);
    }
}
