namespace DynamicCube.Platform.Services;

/// <param name="NamedWorkflow">The workflow the latest version named before services owned their workflow (1f); used to link them once.</param>
/// <param name="BusinessApp">The business app the service belongs to (null: no app).</param>
public sealed record ServiceSummary(string Code, string NameAr, string NameEn, string? NamedWorkflow, string? EntityCode = null, string? BusinessApp = null);

/// <summary>The services that exist (implemented by the Services module), for modules that attach things to a service.</summary>
public interface IServiceCatalog
{
    Task<ServiceSummary?> FindAsync(string serviceCode, CancellationToken cancellationToken);

    Task<IReadOnlyList<ServiceSummary>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Codes of the services of a business app (those of no app are not included).</summary>
    Task<IReadOnlyList<string>> ServicesOfAppAsync(string businessApp, CancellationToken cancellationToken);

    /// <summary>Parties with at least one request for a service of the business app.</summary>
    Task<IReadOnlyList<Guid>> PartiesOfAppAsync(string businessApp, CancellationToken cancellationToken);
}
