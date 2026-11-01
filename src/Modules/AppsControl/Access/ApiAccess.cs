using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.PublicApi;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Access;

/// <summary>
/// Who a public API call runs as (ADR-0042). An API client acts as its service account, a user of kind SERVICE whose roles
/// give it the endpoints (VIEW on each endpoint's resource), the companies and branches it works in and its data grants,
/// exactly as a person's roles give them. The company is the one the call names, or the account's only one. On an endpoint
/// that allows it, the client may name a person registered here (username, national ID or email): that person's roles and
/// data then apply, within the company of the call and the branches both of them work in. A name that matches nobody
/// leaves the service account's access; an inactive person is refused.
/// </summary>
internal sealed class ApiAccess(AppsControlDbContext context, AccessResolver resolver) : IApiAccess
{
    /// <summary>The app whose roles apply: the internal system's (roles of no app apply everywhere).</summary>
    public const string App = "INTERNAL";

    private readonly Dictionary<(string User, string? Company), AccessSnapshot?> _snapshots = [];

    public async Task<ApiAccessResult> ResolveAsync(ApiAccessRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var account = await context.Users.AsNoTracking()
            .Where(u => u.Code == request.Account)
            .Select(u => new { u.Kind, u.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (account is not { Kind: UserKind.Service, Status: UserStatus.Active })
        {
            return ApiAccessResult.Refused(StatusCodes.Status403Forbidden, $"The client's service account {request.Account} does not exist or is not active.");
        }

        var requested = string.IsNullOrWhiteSpace(request.Company) ? null : request.Company.Trim();
        var snapshot = await SnapshotAsync(request.Account, requested, cancellationToken);
        if (snapshot is null || snapshot.Companies.Count == 0)
        {
            return ApiAccessResult.Refused(StatusCodes.Status403Forbidden, $"The client's service account {request.Account} holds no role in any company: give it roles in Apps Control.");
        }

        if (requested is not null && !snapshot.Companies.Contains(requested, StringComparer.Ordinal))
        {
            return ApiAccessResult.Refused(StatusCodes.Status403Forbidden, $"The client does not work in company {requested}.");
        }

        if (requested is null && snapshot.Companies.Count > 1)
        {
            return ApiAccessResult.Refused(
                StatusCodes.Status400BadRequest,
                $"The client works in several companies: name one in the {ApiHeaders.Company} header ({string.Join(", ", snapshot.Companies)}).");
        }

        var company = snapshot.Company!;
        if (!request.MayCallAnyEndpoint && !snapshot.Can(request.Resource, PrivilegeActions.View))
        {
            return ApiAccessResult.Refused(StatusCodes.Status403Forbidden, $"The client may not call this endpoint in company {company}: its roles do not give it {request.Resource}.");
        }

        var caller = new ApiCaller(request.ClientCode, request.Account, company, snapshot.Branches, request.Resource);
        if (string.IsNullOrWhiteSpace(request.ActingUser))
        {
            return ApiAccessResult.Allowed(caller);
        }

        if (!request.MayActForUser)
        {
            return ApiAccessResult.Refused(StatusCodes.Status400BadRequest, $"This endpoint does not act for a person: leave out the {ApiHeaders.ActingUser} header.");
        }

        var name = request.ActingUser.Trim();
        var email = name.Replace(@"\", @"\\", StringComparison.Ordinal).Replace("%", @"\%", StringComparison.Ordinal).Replace("_", @"\_", StringComparison.Ordinal);
        var people = await context.Users.AsNoTracking()
            .Where(u => u.Kind == UserKind.Person && (u.Code == name || u.NationalId == name || (u.Email != null && EF.Functions.ILike(u.Email, email))))
            .Select(u => new { u.Code, u.Status })
            .ToListAsync(cancellationToken);
        var person = people.FirstOrDefault(p => p.Code == name);
        if (person is null && people.Count > 1)
        {
            return ApiAccessResult.Refused(StatusCodes.Status400BadRequest, $"{ApiHeaders.ActingUser} matches more than one person: send the username.");
        }

        person ??= people.SingleOrDefault();
        if (person is null)
        {
            // Not registered here: the service account's own access applies.
            return ApiAccessResult.Allowed(caller);
        }

        if (person.Status != UserStatus.Active)
        {
            return ApiAccessResult.Refused(StatusCodes.Status403Forbidden, "The person the client acts for is not active.");
        }

        var theirs = await SnapshotAsync(person.Code, company, cancellationToken);
        if (theirs is null || theirs.Company != company)
        {
            return ApiAccessResult.Refused(StatusCodes.Status403Forbidden, $"The person the client acts for does not work in company {company}.");
        }

        return ApiAccessResult.Allowed(caller with { UserName = person.Code, Branches = Both(snapshot.Branches, theirs.Branches), ActingFor = person.Code });
    }

    public async Task<IReadOnlySet<string>> CallableAsync(string account, CancellationToken cancellationToken)
    {
        var callable = new HashSet<string>(StringComparer.Ordinal);
        if (!await context.Users.AsNoTracking().AnyAsync(u => u.Code == account && u.Kind == UserKind.Service && u.Status == UserStatus.Active, cancellationToken)
            || await SnapshotAsync(account, null, cancellationToken) is not { } first)
        {
            return callable;
        }

        foreach (var company in first.Companies)
        {
            if (await SnapshotAsync(account, company, cancellationToken) is { } snapshot && snapshot.Company == company)
            {
                callable.UnionWith(snapshot.Privileges.Where(p => p.Value.Contains(PrivilegeActions.View)).Select(p => p.Key));
            }
        }

        return callable;
    }

    /// <summary>The access of the account a call runs as, in the call's company (kept for the call); null for an unlimited caller.</summary>
    public async Task<AccessSnapshot?> SnapshotAsync(ApiCaller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        return caller.UserName is null ? null : await SnapshotAsync(caller.UserName, caller.Company, cancellationToken);
    }

    private async Task<AccessSnapshot?> SnapshotAsync(string user, string? company, CancellationToken cancellationToken)
    {
        if (!_snapshots.TryGetValue((user, company), out var snapshot))
        {
            snapshot = await resolver.GetForUserAsync(user, App, company, cancellationToken);
            _snapshots[(user, company)] = snapshot;
        }

        return snapshot;
    }

    /// <summary>The branches both lists allow (null: every branch).</summary>
    private static IReadOnlyList<string>? Both(IReadOnlyList<string>? a, IReadOnlyList<string>? b) =>
        a is null ? b : b is null ? a : [.. a.Intersect(b, StringComparer.Ordinal)];
}
