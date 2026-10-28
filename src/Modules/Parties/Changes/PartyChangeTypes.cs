namespace DynamicCube.Modules.Parties.Changes;

/// <summary>Entity types, change-set area and guarding resources of Party Registry configuration.</summary>
public static class PartyChangeTypes
{
    public const string RelationshipType = "party.relationship-type";
    public const string ProfileField = "party.profile-field";
    public const string CredentialType = "party.credential-type";
    public const string SanctionType = "party.sanction-type";
    public const string ContextSource = "party.context-source";

    public const string Area = "party.registry";
}

public static class PartyResources
{
    public const string Root = "party";
    public const string Parties = "party.parties";
    public const string Sanctions = "party.sanctions";
    public const string Config = "party.config";
}
