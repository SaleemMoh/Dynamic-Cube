using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Modules.AppsControl.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Configuration;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.AppsControl.Access;

/// <summary>Entities and business apps for every module's configuration rows, and the signed-in user's scope.</summary>
internal sealed class ConfigScopes(AppsControlDbContext context, AccessResolver access, ICurrentUser currentUser) : IConfigScopes
{
    public async Task<ConfigScope> CurrentAsync(CancellationToken cancellationToken) =>
        currentUser.IsAuthenticated ? (await access.GetAsync(cancellationToken)).Scope : ConfigScope.Everything;

    public async Task<ConfigScope> OfUserAsync(string user, CancellationToken cancellationToken)
    {
        var account = await context.Users.AsNoTracking()
            .Where(u => u.IdpSubject == user || u.Code == user)
            .Select(u => new { u.EntityCode, Roles = u.Roles.Where(r => r.Role!.IsActive).Select(r => new { r.Role!.BusinessApp, r.Role.SharedApps }).ToList() })
            .FirstOrDefaultAsync(cancellationToken);
        return account is null
            ? ConfigScope.Everything
            : new ConfigScope(
                account.EntityCode is { } entity ? new HashSet<string>(StringComparer.Ordinal) { entity } : null,
                account.Roles.Count > 0 && account.Roles.All(r => r.BusinessApp is not null)
                    ? account.Roles.SelectMany(r => r.SharedApps.Prepend(r.BusinessApp!)).ToHashSet(StringComparer.Ordinal)
                    : null);
    }

    public async Task<(string? EntityCode, string? BusinessApp)> ResolveAsync(
        string? entityCode, string? businessApp, ValidationErrors errors, CancellationToken cancellationToken, ChangeSet? draft = null)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (entityCode is not null && Pending(draft, AppsControlChangeTypes.Entity, entityCode) is null
            && !await context.Entities.AnyAsync(e => e.Code == entityCode, cancellationToken))
        {
            errors.Add("entityCode", $"There is no organization '{entityCode}'.");
        }

        if (businessApp is null)
        {
            return (entityCode, null);
        }

        // An app created earlier in the same change set: its entity is the one its payload names, else the default.
        string? appEntity;
        if (Pending(draft, AppsControlChangeTypes.BusinessApp, businessApp) is { } pending)
        {
            appEntity = JsonNode.Parse(pending)?["entityCode"]?.GetValue<string>() is { Length: > 0 } named ? named : GovernmentEntity.SingletonCode;
        }
        else
        {
            var app = await context.BusinessApps.Where(a => a.Code == businessApp).Select(a => new { a.EntityCode }).SingleOrDefaultAsync(cancellationToken);
            if (app is null)
            {
                errors.Add("businessApp", $"There is no app '{businessApp}'.");
                return (entityCode, businessApp);
            }

            appEntity = app.EntityCode;
        }

        if (entityCode is not null && appEntity is not null && appEntity != entityCode)
        {
            errors.Add("businessApp", $"App '{businessApp}' belongs to organization {appEntity}, not {entityCode}.");
        }

        return (entityCode ?? appEntity, businessApp);
    }

    public async Task<IReadOnlySet<string>> AppsOnChannelAsync(string channel, CancellationToken cancellationToken) =>
        (await context.BusinessApps.AsNoTracking()
            .Where(a => a.IsActive && a.Kind == AppKind.DynamicCube && a.Channels.Contains(channel))
            .Select(a => a.Code)
            .ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);

    /// <summary>The payload of a row created earlier in the draft, or null.</summary>
    private static string? Pending(ChangeSet? draft, string entityType, string code) =>
        draft?.Items.LastOrDefault(i => i.EntityType == entityType && i.EntityCode == code && i.Operation == ChangeOperation.Create)?.After;
}
