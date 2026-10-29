using System.Security.Claims;
using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DynamicCube.Modules.AppsControl.Access;

/// <summary>
/// Resolves the current user's account, roles, privileges and context. On the internal host the account is created
/// (or linked by username) on first sign-in; bootstrap users receive their configured roles.
/// </summary>
public sealed partial class AccessResolver(
    AppsControlDbContext context,
    ICurrentUser currentUser,
    IHttpContextAccessor httpContextAccessor,
    IMemoryCache cache,
    AccessCache accessCache,
    IOptions<AppsControlOptions> options,
    AppsControlHostOptions host,
    TimeProvider timeProvider,
    ILogger<AccessResolver> logger)
{
    // A page fires several API calls at once; resolving one user at a time per process avoids duplicate work and
    // racing account creation on the first sign-in.
    private static readonly SemaphoreSlim BuildLock = new(1, 1);

    private AccessSnapshot? _current;

    public async Task<AccessSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        if (_current is not null)
        {
            return _current;
        }

        if (!currentUser.IsAuthenticated || !host.ManagesUsers)
        {
            return _current = AccessSnapshot.Anonymous(currentUser.AppCode);
        }

        var key = $"apps-control:access:{accessCache.Version}:{currentUser.AppCode}:{currentUser.UserId}";
        if (cache.TryGetValue(key, out AccessSnapshot? cached) && cached is not null)
        {
            return _current = cached;
        }

        await BuildLock.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue(key, out cached) && cached is not null)
            {
                return _current = cached;
            }

            var snapshot = await BuildAsync(cancellationToken);
            cache.Set(key, snapshot, TimeSpan.FromSeconds(Math.Max(1, options.Value.AccessCacheSeconds)));
            return _current = snapshot;
        }
        finally
        {
            BuildLock.Release();
        }
    }

    private async Task<AccessSnapshot> BuildAsync(CancellationToken cancellationToken)
    {
        var user = await ProvisionAsync(cancellationToken);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var app = await context.Apps.AsNoTracking().Where(a => a.Code == currentUser.AppCode).Select(a => (Guid?)a.Id).SingleOrDefaultAsync(cancellationToken);

        var roles = await context.UserRoles.AsNoTracking()
            .Where(r => r.UserId == user.Id && r.Role!.IsActive && (r.Role.AppId == null || r.Role.AppId == app)
                && (r.EffectiveFrom == null || r.EffectiveFrom <= today) && (r.EffectiveTo == null || r.EffectiveTo >= today))
            .Select(r => new { r.RoleId, r.Role!.Code })
            .ToListAsync(cancellationToken);
        var roleIds = roles.Select(r => r.RoleId).Distinct().ToList();

        var privilegeRows = await context.Privileges.AsNoTracking()
            .Where(p => roleIds.Contains(p.RoleId) && p.Resource!.IsActive)
            .Select(p => new { p.Resource!.Code, p.Actions })
            .ToListAsync(cancellationToken);
        var privileges = privilegeRows
            .GroupBy(p => p.Code, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlySet<string>)g.SelectMany(p => p.Actions).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);

        var assignments = await context.UserAssignments.AsNoTracking()
            .Where(a => a.UserId == user.Id && a.OrgUnit!.IsActive
                && (a.EffectiveFrom == null || a.EffectiveFrom <= today) && (a.EffectiveTo == null || a.EffectiveTo >= today))
            .OrderByDescending(a => a.IsPrimary)
            .Select(a => new { UnitCode = a.OrgUnit!.Code, a.OrgUnit.Path, a.OrgUnit.Attributes, PositionCode = a.Position != null ? a.Position.Code : null })
            .ToListAsync(cancellationToken);

        var subtree = new List<string>();
        foreach (var path in assignments.Select(a => a.Path).Distinct())
        {
            subtree.AddRange(await context.OrgUnits.AsNoTracking()
                .Where(u => u.Path.IsDescendantOf(path) && u.IsActive)
                .Select(u => u.Code)
                .ToListAsync(cancellationToken));
        }

        var primary = assignments.FirstOrDefault();
        var userContext = new UserContext(
            user.Id,
            user.Code,
            currentUser.AppCode,
            roles.Select(r => r.Code).Distinct().ToList(),
            assignments.Where(a => a.PositionCode != null).Select(a => a.PositionCode!).Distinct().ToList(),
            assignments.Select(a => a.UnitCode).ToList(),
            primary?.UnitCode,
            primary is null ? [] : JsonNode.Parse(primary.Attributes) as JsonObject ?? [],
            subtree.Distinct().ToList());

        return new AccessSnapshot(userContext, user.NameAr, user.NameEn, user.Status, user.PreferredLanguage, roleIds, privileges);
    }

    private async Task<UserAccount> ProvisionAsync(CancellationToken cancellationToken)
    {
        var subject = currentUser.UserId;
        var username = currentUser.UserName ?? subject;
        var principal = httpContextAccessor.HttpContext?.User;
        var now = timeProvider.GetUtcNow();

        var user = await context.Users.Include(u => u.Roles).SingleOrDefaultAsync(u => u.IdpSubject == subject, cancellationToken)
            ?? await context.Users.Include(u => u.Roles).SingleOrDefaultAsync(u => u.Code == username, cancellationToken);

        var changed = false;
        if (user is not null && user.IdpSubject is not null && user.IdpSubject != subject)
        {
            if (!options.Value.RelinkAccountsByUserName)
            {
                LogSubjectMismatch(logger, username);
                throw new IdentityMismatchException(username);
            }

            LogRelinked(logger, username);
            user.IdpSubject = subject;
            user.LastLoginAt = now;
            changed = true;
        }
        else if (user is null)
        {
            var name = principal?.FindFirstValue("name") ?? username;
            user = new UserAccount
            {
                Code = username,
                IdpSubject = subject,
                NameAr = principal?.FindFirstValue("name_ar") ?? name,
                NameEn = name,
                Email = principal?.FindFirstValue("email"),
                NationalId = principal?.FindFirstValue("national_id"),
                Owner = Ownership.Entity,
                LastLoginAt = now,
            };
            context.Users.Add(user);
            changed = true;
            LogProvisioned(logger, username);
        }
        else if (user.IdpSubject is null)
        {
            user.IdpSubject = subject;
            user.LastLoginAt = now;
            changed = true;
        }
        else if (user.LastLoginAt is null || now - user.LastLoginAt > TimeSpan.FromHours(12))
        {
            user.LastLoginAt = now;
            changed = true;
        }

        if (options.Value.BootstrapUsers.TryGetValue(username, out var bootstrapRoles) && bootstrapRoles.Length > 0)
        {
            var roleIds = await context.Roles.Where(r => bootstrapRoles.Contains(r.Code)).Select(r => r.Id).ToListAsync(cancellationToken);
            foreach (var roleId in roleIds.Where(id => user.Roles.All(r => r.RoleId != id || r.OrgUnitId != null)))
            {
                user.Roles.Add(new UserRole { UserId = user.Id, RoleId = roleId });
                changed = true;
                LogBootstrapRole(logger, username, roleId);
            }
        }

        if (changed)
        {
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Two first requests raced; the other one created the account.
                context.ChangeTracker.Clear();
                return await context.Users.AsNoTracking().SingleAsync(u => u.IdpSubject == subject, cancellationToken);
            }
        }

        return user;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created Apps Control account for {UserName} on first sign-in")]
    private static partial void LogProvisioned(ILogger logger, string userName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sign-in refused: account {UserName} is linked to another identity-provider subject")]
    private static partial void LogSubjectMismatch(ILogger logger, string userName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Account {UserName} relinked to a new identity-provider subject")]
    private static partial void LogRelinked(ILogger logger, string userName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bootstrap: assigned role {RoleId} to {UserName}")]
    private static partial void LogBootstrapRole(ILogger logger, string userName, Guid roleId);
}

/// <summary>Which zone the module serves: the internal host manages employee accounts, the external host does not.</summary>
public sealed record AppsControlHostOptions(bool ManagesUsers);
