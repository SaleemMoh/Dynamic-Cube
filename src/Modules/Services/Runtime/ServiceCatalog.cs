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

    private async Task<List<ServiceSummary>> LoadAsync(IQueryable<Domain.Service> query, CancellationToken cancellationToken)
    {
        var services = await query.AsNoTracking().Select(s => new { s.Id, s.Code, s.NameAr, s.NameEn }).ToListAsync(cancellationToken);
        var ids = services.Select(s => s.Id).ToList();
        var latest = await context.Versions.AsNoTracking()
            .Where(v => ids.Contains(v.ServiceId))
            .GroupBy(v => v.ServiceId)
            .Select(g => g.OrderByDescending(v => v.Number).Select(v => new { v.ServiceId, v.Definition }).First())
            .ToListAsync(cancellationToken);
        return [.. services.Select(s => new ServiceSummary(s.Code, s.NameAr, s.NameEn, Named(latest.FirstOrDefault(l => l.ServiceId == s.Id)?.Definition)))];
    }

    private static string? Named(string? definition) =>
        definition is not null && JsonNode.Parse(definition)?["workflow"] is JsonValue value && value.ToString() is { Length: > 0 } code ? code : null;
}

/// <summary>A service published for the first time gets its default workflow, in the same transaction.</summary>
internal sealed class ServiceWorkflowProvisioner(IServiceProvider services) : IChangeSetPublishHook<ServicesDbContext>
{
    public async Task OnPublishedAsync(ServicesDbContext context, ChangeSet changeSet, CancellationToken cancellationToken)
    {
        var created = changeSet.Items.Where(i => i.EntityType == ServiceChangeTypes.Service && i.Operation == ChangeOperation.Create).Select(i => i.EntityCode).ToList();
        if (created.Count == 0 || services.GetService<IServiceWorkflows>() is not { } workflows)
        {
            return;
        }

        foreach (var service in await context.Services.AsNoTracking().Where(s => created.Contains(s.Code)).ToListAsync(cancellationToken))
        {
            await workflows.EnsureAsync(service.Code, service.NameAr, service.NameEn, cancellationToken);
        }
    }
}
