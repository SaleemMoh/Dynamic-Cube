namespace DynamicCube.Platform.Security;

/// <summary>Answers whether the current user holds an action on a resource (implemented by Apps Control).</summary>
public interface IAccessChecker
{
    Task<bool> CanAsync(string resourceCode, string action, CancellationToken cancellationToken);
}
