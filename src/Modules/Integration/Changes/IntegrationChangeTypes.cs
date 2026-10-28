namespace DynamicCube.Modules.Integration.Changes;

/// <summary>Entity types, change-set area and guarding resources of Integration Manager configuration.</summary>
public static class IntegrationChangeTypes
{
    public const string Provider = "integ.provider";
    public const string Connection = "integ.connection";
    public const string EnvelopeProfile = "integ.envelope-profile";
    public const string Operation = "integ.operation";

    /// <summary>All Integration Manager configuration: providers, connections, envelopes and operations.</summary>
    public const string Area = "integ.integrations";
}

public static class IntegrationResources
{
    public const string Root = "integration";
    public const string Config = "integration.config";
    public const string TestConsole = "integration.test-console";
    public const string CallLog = "integration.call-log";
}
