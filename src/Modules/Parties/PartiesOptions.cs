namespace DynamicCube.Modules.Parties;

public sealed class PartiesOptions
{
    public const string SectionName = "Parties";

    /// <summary>Token claim with the applicant's national ID / iqama number (mapped from Nafath in Keycloak).</summary>
    public string NationalIdClaim { get; set; } = "national_id";

    public string NameClaim { get; set; } = "name";
}
