namespace DynamicCube.Platform.Parties;

/// <summary>A field of a party's data as a path in a request's facts (<see cref="Code"/>, e.g. <c>party.profile.CR_CAPITAL</c>), with its names.</summary>
public sealed record FactField(string Code, string NameAr, string NameEn);

/// <summary>The fields of a party's data that answers and integration inputs can come from (implemented by the Party Registry).</summary>
public interface IPartyFactFields
{
    Task<IReadOnlyList<FactField>> ListAsync(CancellationToken cancellationToken);
}
