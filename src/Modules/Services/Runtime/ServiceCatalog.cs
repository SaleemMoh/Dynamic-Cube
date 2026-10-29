using System.Text.Json.Nodes;
using DynamicCube.Modules.Services.Changes;
using DynamicCube.Modules.Services.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Services;
using DynamicCube.Platform.Workflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Services.Runtime;

/// <summary>The services that exist, for modules that attach things to a service (its workflow).</summary>
internal sealed class ServiceCatalog(ServicesDbContext context) : IServiceCatalog
{
    public async Task<ServiceSummary?> FindAsync(string serviceCode, CancellationToken cancellationToken) =>
        (await LoadAsync(context.Services.Where(s => s.Code == serviceCode), cancellationToken)).SingleOrDefault();

    public async Task<IReadOnlyList<ServiceSummary>> ListAsync(CancellationToken cancellationToken) =>
        await LoadAsync(context.Services.OrderBy(s => s.Code), cancellationToken);

    public async Task<IReadOnlyList<string>> ServicesOfAppAsync(string businessApp, CancellationToken cancellationToken) =>
        await context.Services.AsNoTracking().Where(s => s.BusinessApp == businessApp).Select(s => s.Code).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> PartiesOfAppAsync(string businessApp, CancellationToken cancellationToken)
    {
        var services = context.Services.Where(s => s.BusinessApp == businessApp).Select(s => s.Code);
        var requests = context.Requests.AsNoTracking().Where(r => services.Contains(r.ServiceCode));
        return await requests.Select(r => r.SubmitterPartyId).Union(requests.Select(r => r.BeneficiaryPartyId)).ToListAsync(cancellationToken);
    }

    private async Task<List<ServiceSummary>> LoadAsync(IQueryable<Domain.Service> query, CancellationToken cancellationToken)
    {
        var services = await query.AsNoTracking().Select(s => new { s.Id, s.Code, s.NameAr, s.NameEn, s.EntityCode, s.BusinessApp }).ToListAsync(cancellationToken);
        var ids = services.Select(s => s.Id).ToList();
        var latest = await context.Versions.AsNoTracking()
            .Where(v => ids.Contains(v.ServiceId))
            .GroupBy(v => v.ServiceId)
            .Select(g => g.OrderByDescending(v => v.Number).Select(v => new { v.ServiceId, v.Definition }).First())
            .ToListAsync(cancellationToken);
        return [.. services.Select(s => new ServiceSummary(s.Code, s.NameAr, s.NameEn, Named(latest.FirstOrDefault(l => l.ServiceId == s.Id)?.Definition), s.EntityCode, s.BusinessApp))];
    }

    private static string? Named(string? definition) =>
        definition is not null && JsonNode.Parse(definition)?["workflow"] is JsonValue value && value.ToString() is { Length: > 0 } code ? code : null;
}

/// <summary>
/// In the same transaction as the change set: a service published for the first time gets its default workflow, and a
/// new version that names a workflow (packages from before services owned their workflow) has it copied into the
/// service's own workflow, or handed to the service when it has none and no other service owns it.
/// </summary>
internal sealed class ServiceWorkflowProvisioner(IServiceProvider services) : IChangeSetPublishHook<ServicesDbContext>
{
    public async Task OnPublishedAsync(ServicesDbContext context, ChangeSet changeSet, CancellationToken cancellationToken)
    {
        if (services.GetService<IServiceWorkflows>() is not { } workflows)
        {
            return;
        }

        var named = changeSet.Items
            .Where(i => i.EntityType == ServiceChangeTypes.Version && i.Operation == ChangeOperation.Create)
            .OrderBy(i => i.Sequence)
            .Select(i => (Service: i.EntityCode[..Math.Max(0, i.EntityCode.LastIndexOf('/'))], Workflow: NamedWorkflow(i.After)))
            .Where(n => n.Workflow is not null)
            .ToList();
        var created = changeSet.Items.Where(i => i.EntityType == ServiceChangeTypes.Service && i.Operation == ChangeOperation.Create).Select(i => i.EntityCode).ToList();
        var changed = changeSet.Items.Where(i => i.EntityType == ServiceChangeTypes.Service && i.Operation != ChangeOperation.Delete).Select(i => i.EntityCode).ToList();
        var codes = created.Concat(named.Select(n => n.Service)).Concat(changed).Distinct().ToList();
        if (codes.Count == 0)
        {
            return;
        }

        var found = await context.Services.AsNoTracking().Where(s => codes.Contains(s.Code)).ToDictionaryAsync(s => s.Code, cancellationToken);
        foreach (var (service, workflow) in named)
        {
            if (found.TryGetValue(service, out var s))
            {
                await workflows.AdoptAsync(s.Code, s.NameAr, s.NameEn, workflow!, cancellationToken);
            }
        }

        foreach (var code in created)
        {
            if (found.TryGetValue(code, out var s))
            {
                await workflows.EnsureAsync(s.Code, s.NameAr, s.NameEn, cancellationToken);
            }
        }

        // A service's workflow belongs to the same entity and business app as the service.
        foreach (var code in changed)
        {
            if (found.TryGetValue(code, out var s))
            {
                await workflows.ScopeAsync(s.Code, s.EntityCode, s.BusinessApp, cancellationToken);
            }
        }
    }

    /// <summary>The workflow a version payload names; property names in any case, as the applier reads them.</summary>
    private static string? NamedWorkflow(string? payload)
    {
        static JsonNode? Child(JsonNode? node, string name) =>
            node is JsonObject obj ? obj.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)).Value : null;
        return payload is not null && Child(Child(JsonNode.Parse(payload), "definition"), "workflow") is JsonValue value && value.ToString().Trim() is { Length: > 0 } code
            ? code
            : null;
    }
}
