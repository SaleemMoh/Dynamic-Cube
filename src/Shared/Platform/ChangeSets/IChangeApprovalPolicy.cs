using Microsoft.Extensions.Options;

namespace DynamicCube.Platform.ChangeSets;

/// <summary>Decides whether change sets of an area need a checker's approval. The decision is fixed when a change set is created.</summary>
public interface IChangeApprovalPolicy
{
    Task<bool> RequiresApprovalAsync(string area, CancellationToken cancellationToken);
}

/// <summary>Default policy from configuration (<c>ChangeSets</c> section); modules may replace it with a stored policy.</summary>
public sealed class OptionsChangeApprovalPolicy(IOptions<ChangeSetOptions> options) : IChangeApprovalPolicy
{
    public Task<bool> RequiresApprovalAsync(string area, CancellationToken cancellationToken) =>
        Task.FromResult(options.Value.RequiresApproval(area));
}
