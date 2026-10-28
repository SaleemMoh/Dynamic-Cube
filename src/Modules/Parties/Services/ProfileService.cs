using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Parties.Domain;
using DynamicCube.Modules.Parties.Persistence;
using DynamicCube.Platform.Integration;
using DynamicCube.Platform.Parties;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Parties.Services;

public sealed record ProfileEntry(string Code, string NameAr, string NameEn, ProfileDataType DataType, JsonNode? Value, RecordSource? Source, DateTimeOffset? RefreshedAt, bool IsStale);

public sealed record ProfileRefreshResult(string OperationCode, IntegrationOutcome Outcome, string? MessageAr, string? MessageEn, IReadOnlyList<string> Fields);

/// <summary>Profile attributes of a party; integration attributes are fetched again when older than their refresh period.</summary>
internal sealed class ProfileService(PartiesDbContext context, IIntegrationGateway gateway, TimeProvider clock)
{
    public async Task<IReadOnlyList<ProfileEntry>> GetAsync(Guid partyId, CancellationToken cancellationToken)
    {
        var party = await context.Parties.AsNoTracking().SingleOrDefaultAsync(p => p.Id == partyId, cancellationToken)
            ?? throw new PartyNotFoundException($"Party {partyId} does not exist.");
        var attributes = await context.ProfileFields.AsNoTracking().Where(a => a.IsActive && a.PartyKind == party.Kind)
            .OrderBy(a => a.DisplayOrder).ThenBy(a => a.Code).ToListAsync(cancellationToken);
        var values = await context.ProfileValues.AsNoTracking().Where(v => v.PartyId == partyId).ToDictionaryAsync(v => v.FieldId, cancellationToken);
        var now = clock.GetUtcNow();
        return [.. attributes.Select(a =>
        {
            var value = values.GetValueOrDefault(a.Id);
            return new ProfileEntry(a.Code, a.NameAr, a.NameEn, a.DataType, value?.Value is null ? null : JsonNode.Parse(value.Value), value?.Source, value?.RefreshedAt,
                a.OperationCode is not null && (value is null || now - value.RefreshedAt >= TimeSpan.FromHours(a.RefreshHours)));
        })];
    }

    /// <summary>Fetches stale integration attributes (all of them when forced); one call per operation and input set.</summary>
    public async Task<IReadOnlyList<ProfileRefreshResult>> RefreshAsync(Guid partyId, bool force, CancellationToken cancellationToken)
    {
        var party = await context.Parties.Include(p => p.Identifiers).SingleOrDefaultAsync(p => p.Id == partyId, cancellationToken)
            ?? throw new PartyNotFoundException($"Party {partyId} does not exist.");
        var attributes = await context.ProfileFields.AsNoTracking()
            .Where(a => a.IsActive && a.PartyKind == party.Kind && a.OperationCode != null)
            .ToListAsync(cancellationToken);
        var values = await context.ProfileValues.Where(v => v.PartyId == partyId).ToDictionaryAsync(v => v.FieldId, cancellationToken);
        var now = clock.GetUtcNow();
        var due = attributes.Where(a => force || !values.TryGetValue(a.Id, out var v) || now - v.RefreshedAt >= TimeSpan.FromHours(a.RefreshHours)).ToList();

        var results = new List<ProfileRefreshResult>();
        foreach (var group in due.GroupBy(a => (a.OperationCode!, Inputs(a))))
        {
            var inputs = new Dictionary<string, string?>();
            foreach (var binding in group.First().Inputs)
            {
                inputs[binding.Parameter] = party.Identifiers.FirstOrDefault(i => i.Type == binding.IdentifierType)?.Value;
            }

            if (inputs.Values.Any(v => v is null))
            {
                results.Add(new ProfileRefreshResult(group.Key.Item1, IntegrationOutcome.BusinessError, "لا يتوفر المعرّف المطلوب لهذا الطرف.", "The party has no identifier of the required type.", [.. group.Select(a => a.Code)]));
                continue;
            }

            var result = await gateway.InvokeAsync(group.Key.Item1, inputs, cancellationToken);
            if (result.Outcome is IntegrationOutcome.Success or IntegrationOutcome.NotFound)
            {
                foreach (var attribute in group)
                {
                    var value = result.Outputs.GetValueOrDefault(attribute.OutputName!);
                    if (!values.TryGetValue(attribute.Id, out var stored))
                    {
                        stored = new ProfileValue { PartyId = partyId, FieldId = attribute.Id };
                        context.ProfileValues.Add(stored);
                        values[attribute.Id] = stored;
                    }

                    stored.Value = value?.ToJsonString();
                    stored.Source = RecordSource.Integration;
                    stored.RefreshedAt = now;
                }
            }

            results.Add(new ProfileRefreshResult(group.Key.Item1, result.Outcome, result.MessageAr, result.MessageEn, [.. group.Select(a => a.Code)]));
        }

        await context.SaveChangesAsync(cancellationToken);
        return results;
    }

    /// <summary>Records a manual value (attributes without an integration source).</summary>
    public async Task SetAsync(Guid partyId, string attributeCode, JsonNode? value, CancellationToken cancellationToken)
    {
        var party = await context.Parties.AsNoTracking().SingleOrDefaultAsync(p => p.Id == partyId, cancellationToken)
            ?? throw new PartyNotFoundException($"Party {partyId} does not exist.");
        var attribute = await context.ProfileFields.AsNoTracking().SingleOrDefaultAsync(a => a.Code == attributeCode && a.IsActive && a.PartyKind == party.Kind, cancellationToken)
            ?? throw new PartyNotFoundException($"Profile attribute '{attributeCode}' does not exist for {party.Kind} parties.");
        if (attribute.OperationCode is not null)
        {
            throw new PartyConflictException($"'{attributeCode}' is read from an integration and cannot be set by hand.");
        }

        var stored = await context.ProfileValues.SingleOrDefaultAsync(v => v.PartyId == partyId && v.FieldId == attribute.Id, cancellationToken);
        if (stored is null)
        {
            stored = new ProfileValue { PartyId = partyId, FieldId = attribute.Id };
            context.ProfileValues.Add(stored);
        }

        stored.Value = value?.ToJsonString();
        stored.Source = RecordSource.Manual;
        stored.RefreshedAt = clock.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken);
    }

    private static string Inputs(ProfileField attribute) => JsonSerializer.Serialize(attribute.Inputs.OrderBy(i => i.Parameter, StringComparer.Ordinal).Select(i => $"{i.Parameter}={i.IdentifierType}"));
}
