using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.ChangeSets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DynamicCube.Modules.AppsControl.Changes;

/// <summary>
/// Approval policy maintained on the Apps Control approval-policy screen (<c>apps.change_area</c>). The governance area
/// always needs approval; areas without a row (other modules' areas) fall back to the <c>ChangeSets</c> configuration.
/// </summary>
internal sealed class AppsControlApprovalPolicy(AppsControlDbContext context, IOptions<ChangeSetOptions> options) : IChangeApprovalPolicy
{
    public async Task<bool> RequiresApprovalAsync(string area, CancellationToken cancellationToken)
    {
        if (area == AppsControlAreas.Governance)
        {
            return true;
        }

        var stored = await context.ChangeAreas.AsNoTracking()
            .Where(a => a.Code == area && a.IsActive)
            .Select(a => (bool?)a.RequiresApproval)
            .SingleOrDefaultAsync(cancellationToken);
        return stored ?? options.Value.RequiresApproval(area);
    }
}
