using DynamicCube.Platform.Parties;
using Microsoft.AspNetCore.Http;

namespace DynamicCube.Modules.Parties.Services;

/// <summary>
/// Validates the <c>X-Acting-Party</c> header of portal requests against the individual's active relationships (stored
/// data; contexts are refreshed when the applicant opens the context selector, not on every request).
/// </summary>
internal sealed class ActingPartyAccessor(IHttpContextAccessor accessor, ContextService contexts) : IActingPartyAccessor
{
    private ActingParty? _resolved;

    public async Task<ActingParty> GetAsync(CancellationToken cancellationToken)
    {
        if (_resolved is not null)
        {
            return _resolved;
        }

        var individual = await contexts.CurrentIndividualAsync(cancellationToken);
        var header = accessor.HttpContext?.Request.Headers[IActingPartyAccessor.Header].ToString();
        if (string.IsNullOrWhiteSpace(header) || (Guid.TryParse(header, out var requested) && requested == individual.Id))
        {
            return _resolved = new ActingParty(individual.Id, individual.Id, individual.Kind, individual.NameAr, individual.NameEn, null, []);
        }

        if (!Guid.TryParse(header, out requested))
        {
            throw new ActingPartyException($"{IActingPartyAccessor.Header} must be a party id.");
        }

        var context = (await contexts.ListAsync(individual.Id, cancellationToken)).FirstOrDefault(c => c.PartyId == requested)
            ?? throw new ActingPartyException("You are not authorized to act for this party.");
        return _resolved = new ActingParty(individual.Id, context.PartyId, context.Kind, context.NameAr, context.NameEn, context.RelationshipCode, context.Services);
    }
}
