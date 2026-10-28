namespace DynamicCube.Modules.Communication.Changes;

/// <summary>Entity types, change-set area and guarding resources of communication configuration.</summary>
public static class CommunicationChangeTypes
{
    public const string Template = "com.template";
    public const string Rule = "com.rule";

    public const string Area = "com.communication";
}

public static class CommunicationResources
{
    public const string Config = "communication.config";

    /// <summary>The log of messages sent; EDIT allows sending a failed message again.</summary>
    public const string Messages = "communication.messages";
}

/// <summary>How a message reaches people.</summary>
public static class Channels
{
    public const string Email = "EMAIL";
    public const string Sms = "SMS";
    public const string InApp = "IN_APP";

    public static readonly string[] All = [Email, Sms, InApp];
}

/// <summary>Who a rule notifies.</summary>
public static class RecipientKinds
{
    /// <summary>A party (the applicant or holder by default): email and mobile from its profile, in-app on the portal.</summary>
    public const string Party = "PARTY";

    /// <summary>Employees by user name (e.g. the task's assignee): email and mobile from their account, in-app in the internal system.</summary>
    public const string User = "USER";

    /// <summary>Every active employee holding a role.</summary>
    public const string Role = "ROLE";

    public static readonly string[] All = [Party, User, Role];
}
