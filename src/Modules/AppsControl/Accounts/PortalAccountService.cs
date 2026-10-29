using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.AppsControl.Accounts;

/// <summary>An applicant's portal account: the national ID is the user name the applicant signs in with.</summary>
public sealed record NewPortalAccountRequest(string NationalId, string FirstName, string? LastName, string? Email, string? TemporaryPassword, bool SendSetupEmail);

/// <summary>
/// Applicants' portal accounts, kept in the portal realm (<c>AppsControl:PortalIdentity</c>). An account's user name is the
/// applicant's national ID or iqama number, also kept as the <c>national_id</c> attribute that the portal's tokens carry,
/// so the account finds the applicant's party and requests. Applicants are not Apps Control users: enabling and
/// disabling act on the account only.
/// </summary>
internal sealed partial class PortalAccountService([FromKeyedServices(PortalAccountService.DirectoryKey)] IAccountDirectory directory)
{
    public const string DirectoryKey = "portal";
    public const string NationalIdAttribute = "national_id";

    public Task<AccountCapabilities> CapabilitiesAsync(CancellationToken cancellationToken) => directory.CapabilitiesAsync(cancellationToken);

    public Task<AccountPage> SearchAsync(string? search, int first, int max, CancellationToken cancellationToken) =>
        directory.SearchAsync(search, first, max, cancellationToken);

    public Task<AccountView> GetAsync(string nationalId, CancellationToken cancellationToken) => directory.GetAsync(Require(nationalId), cancellationToken);

    public Task<AccountView> CreateAsync(NewPortalAccountRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var nationalId = Require(request.NationalId);
        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            throw new AccountException("Give the applicant's first name.", AccountProblem.Invalid, "firstName");
        }

        return directory.CreateAsync(
            new NewAccountRequest(
                nationalId,
                request.Email,
                request.FirstName.Trim(),
                request.LastName?.Trim(),
                request.TemporaryPassword,
                request.SendSetupEmail,
                new Dictionary<string, string> { [NationalIdAttribute] = nationalId }),
            cancellationToken);
    }

    public Task SetEnabledAsync(string nationalId, bool enabled, CancellationToken cancellationToken) =>
        directory.SetEnabledAsync(Require(nationalId), enabled, cancellationToken);

    public Task SetPasswordAsync(string nationalId, PasswordRequest request, CancellationToken cancellationToken) =>
        directory.SetPasswordAsync(Require(nationalId), request, cancellationToken);

    public Task SignOutAsync(string nationalId, CancellationToken cancellationToken) => directory.SignOutAsync(Require(nationalId), cancellationToken);

    private static string Require(string? nationalId)
    {
        var value = nationalId?.Trim() ?? string.Empty;
        return NationalId().IsMatch(value)
            ? value
            : throw new AccountException("A national ID or iqama number: 10 digits starting with 1 or 2.", AccountProblem.Invalid, "nationalId");
    }

    [GeneratedRegex("^[12][0-9]{9}$")]
    private static partial Regex NationalId();
}
