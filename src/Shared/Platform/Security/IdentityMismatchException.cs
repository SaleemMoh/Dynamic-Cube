namespace DynamicCube.Platform.Security;

/// <summary>
/// The signed-in identity carries the username of an account linked to another identity-provider subject, e.g. after the
/// user was deleted and created again in the identity provider. Answered with 403: an administrator must relink it.
/// </summary>
public sealed class IdentityMismatchException(string userName)
    : Exception($"The account '{userName}' is linked to another identity. Ask an administrator to relink it.")
{
    public string UserName { get; } = userName;
}
