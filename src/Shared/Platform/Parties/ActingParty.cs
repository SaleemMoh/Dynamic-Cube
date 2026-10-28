namespace DynamicCube.Platform.Parties;

public enum PartyKind
{
    Individual,
    Establishment,
}

/// <summary>
/// Who a portal request is for: the signed-in individual acting for themselves or for a party they are allowed to
/// represent (owner, manager, delegate…). Requests record both (submitter and beneficiary).
/// </summary>
public sealed record ActingParty(Guid IndividualId, Guid PartyId, PartyKind Kind, string NameAr, string NameEn, string? RelationshipCode)
{
    public bool IsSelf => IndividualId == PartyId;
}

/// <summary>Resolves and validates the acting party of the current external request (header <see cref="Header"/>).</summary>
public interface IActingPartyAccessor
{
    public const string Header = "X-Acting-Party";

    /// <summary>The acting party; the signed-in individual when no header is sent.</summary>
    /// <exception cref="ActingPartyException">The user may not act for the requested party.</exception>
    Task<ActingParty> GetAsync(CancellationToken cancellationToken);
}

public sealed class ActingPartyException(string message) : Exception(message);
