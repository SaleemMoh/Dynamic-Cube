using DynamicCube.Platform.Security;

namespace DynamicCube.Modules.AppsControl.Access;

/// <summary>Kernel access checks answered from the current user's Apps Control privileges.</summary>
internal sealed class AccessChecker(AccessResolver resolver) : IAccessChecker
{
    public async Task<bool> CanAsync(string resourceCode, string action, CancellationToken cancellationToken) =>
        (await resolver.GetAsync(cancellationToken)).Can(resourceCode, action);
}
