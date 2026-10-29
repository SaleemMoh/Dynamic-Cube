using DynamicCube.Modules.AppsControl.Access;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Accounts;

/// <summary>
/// Account actions on a user: enabling and disabling act at once on the sign-in account and on the user's status in
/// Apps Control (so a disabled user loses access straight away, without waiting for a change set); passwords and sign-out
/// act on the sign-in account only.
/// </summary>
internal sealed class AccountService(IAccountDirectory directory, AppsControlDbContext db, AccessCache cache)
{
    public Task<AccountCapabilities> CapabilitiesAsync(CancellationToken cancellationToken) => directory.CapabilitiesAsync(cancellationToken);

    public Task<AccountView> GetAsync(string userName, CancellationToken cancellationToken) => directory.GetAsync(userName, cancellationToken);

    public Task<AccountView> CreateAsync(NewAccountRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.UserName) || !Platform.Configuration.ValidationErrors.UserName().IsMatch(request.UserName.Trim()))
        {
            throw new AccountException("A user name: letters, digits and . _ - @.", AccountProblem.Invalid, "userName");
        }

        return directory.CreateAsync(request, cancellationToken);
    }

    public async Task SetEnabledAsync(string userName, bool enabled, CancellationToken cancellationToken)
    {
        var account = await directory.GetAsync(userName, cancellationToken);
        if (account.Exists)
        {
            await directory.SetEnabledAsync(userName, enabled, cancellationToken);
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Code == userName, cancellationToken);
        if (user is not null)
        {
            user.Status = enabled ? UserStatus.Active : UserStatus.Disabled;
            await db.SaveChangesAsync(cancellationToken);
            cache.Invalidate();
        }
        else if (!account.Exists)
        {
            throw new AccountException($"There is no user or account '{userName}'.", AccountProblem.NotFound);
        }
    }

    public Task SetPasswordAsync(string userName, PasswordRequest request, CancellationToken cancellationToken) =>
        directory.SetPasswordAsync(userName, request, cancellationToken);

    public Task SignOutAsync(string userName, CancellationToken cancellationToken) => directory.SignOutAsync(userName, cancellationToken);
}
