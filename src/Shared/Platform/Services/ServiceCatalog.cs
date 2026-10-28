namespace DynamicCube.Platform.Services;

/// <param name="NamedWorkflow">The workflow the latest version named before services owned their workflow (1f); used to link them once.</param>
public sealed record ServiceSummary(string Code, string NameAr, string NameEn, string? NamedWorkflow);

/// <summary>The services that exist (implemented by the Services module), for modules that attach things to a service.</summary>
public interface IServiceCatalog
{
    Task<ServiceSummary?> FindAsync(string serviceCode, CancellationToken cancellationToken);

    Task<IReadOnlyList<ServiceSummary>> ListAsync(CancellationToken cancellationToken);
}
