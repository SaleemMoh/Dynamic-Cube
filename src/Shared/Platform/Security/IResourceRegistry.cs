namespace DynamicCube.Platform.Security;

/// <summary>
/// Resources a module creates at run time (e.g. one per report, ADR-0045), kept in Apps Control so roles can grant them and
/// menus can open them. Called from a publish hook, inside the change set's transaction.
/// </summary>
public interface IResourceRegistry
{
    /// <summary>Creates or brings in line an active resource (its parent must exist).</summary>
    Task SaveAsync(CatalogResource resource, CancellationToken cancellationToken);

    /// <summary>Deactivates a resource; its grants stay but give nothing while it is inactive.</summary>
    Task RetireAsync(string code, CancellationToken cancellationToken);
}
