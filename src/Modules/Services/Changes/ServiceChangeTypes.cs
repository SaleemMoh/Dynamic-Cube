namespace DynamicCube.Modules.Services.Changes;

/// <summary>Entity types, change-set area and guarding resources of service configuration.</summary>
public static class ServiceChangeTypes
{
    public const string Service = "svc.service";
    public const string Question = "svc.question";
    public const string Version = "svc.service-version";

    /// <summary>Services, the question bank and service versions (one area so a version can use new questions).</summary>
    public const string Area = "svc.services";
}

public static class ServiceResources
{
    public const string Root = "services";
    public const string Config = "services.config";
    public const string Requests = "services.requests";
}
