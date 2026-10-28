namespace DynamicCube.Platform.Security;

/// <summary>Identity used by background processes (worker, migrator).</summary>
public sealed class SystemUser(string appCode) : ICurrentUser
{
    public const string Id = "system";

    public string UserId => Id;

    public string? UserName => Id;

    public bool IsAuthenticated => true;

    public string AppCode { get; } = appCode;
}
