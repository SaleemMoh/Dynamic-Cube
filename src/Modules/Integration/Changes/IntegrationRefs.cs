using DynamicCube.Modules.Integration.Persistence;
using DynamicCube.Platform.ChangeSets;

namespace DynamicCube.Modules.Integration.Changes;

internal sealed class IntegrationRefs(IntegrationDbContext context, ChangeSet? draft) : ConfigRefs<IntegrationDbContext>(context, draft)
{
    public Task<Guid?> ProviderAsync(string? code, string field, ValidationErrors errors, CancellationToken ct) =>
        ResolveAsync(Context.Providers, IntegrationChangeTypes.Provider, code, field, errors, ct);

    public Task<Guid?> ConnectionAsync(string? code, string field, ValidationErrors errors, CancellationToken ct) =>
        ResolveAsync(Context.Connections, IntegrationChangeTypes.Connection, code, field, errors, ct);

    public Task<Guid?> EnvelopeProfileAsync(string? code, string field, ValidationErrors errors, CancellationToken ct) =>
        ResolveAsync(Context.EnvelopeProfiles, IntegrationChangeTypes.EnvelopeProfile, code, field, errors, ct);
}

/// <summary>Base of the Integration Manager appliers.</summary>
internal abstract class IntegrationApplier<TEntity, TPayload>(IntegrationDbContext context)
    : ConfigApplier<IntegrationDbContext, IntegrationRefs, TEntity, TPayload>(context)
    where TEntity : ConfigEntity, new()
    where TPayload : ConfigPayload, new()
{
    public override string Area => IntegrationChangeTypes.Area;

    public override string ResourceCode => IntegrationResources.Config;

    protected override System.Text.RegularExpressions.Regex CodePattern => ValidationErrors.IdentifierCode();

    protected override IntegrationRefs CreateRefs(IntegrationDbContext context, ChangeSet? draft) => new(context, draft);
}
