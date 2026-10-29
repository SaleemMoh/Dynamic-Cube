using DynamicCube.Modules.AppsControl.Accounts;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.AppsControl.Domain;
using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DynamicCube.Modules.AppsControl.Endpoints;

/// <summary>
/// Sign-in accounts behind users: what this installation allows, an account's state, creating one, enabling and
/// disabling, passwords and signing out everywhere. Guarded by the Users resource.
/// </summary>
internal static class AccountEndpoints
{
    public static void Map(RouteGroupBuilder admin)
    {
        var accounts = admin.MapGroup("/accounts").AddEndpointFilter(Errors);
        accounts.MapGet("/capabilities", (AccountService service, CancellationToken ct) => service.CapabilitiesAsync(ct))
            .RequirePrivilege(AppsControlResources.Users, PrivilegeActions.View).WithName("GetAccountCapabilities");
        accounts.MapGet("/{userName}", (string userName, AccountService service, CancellationToken ct) => service.GetAsync(userName, ct))
            .RequirePrivilege(AppsControlResources.Users, PrivilegeActions.View).WithName("GetAccount");
        accounts.MapPost("/", (NewAccountRequest request, AccountService service, CancellationToken ct) => service.CreateAsync(request, ct))
            .RequirePrivilege(AppsControlResources.Users, PrivilegeActions.Create).WithName("CreateAccount");
        accounts.MapPost("/{userName}/enable", async (string userName, AccountService service, CancellationToken ct) =>
        {
            await service.SetEnabledAsync(userName, true, ct);
            return Results.NoContent();
        }).RequirePrivilege(AppsControlResources.Users, PrivilegeActions.Edit).WithName("EnableAccount");
        accounts.MapPost("/{userName}/disable", async (string userName, AccountService service, CancellationToken ct) =>
        {
            await service.SetEnabledAsync(userName, false, ct);
            return Results.NoContent();
        }).RequirePrivilege(AppsControlResources.Users, PrivilegeActions.Edit).WithName("DisableAccount");
        accounts.MapPost("/{userName}/password", async (string userName, PasswordRequest request, AccountService service, CancellationToken ct) =>
        {
            await service.SetPasswordAsync(userName, request, ct);
            return Results.NoContent();
        }).RequirePrivilege(AppsControlResources.Users, PrivilegeActions.Edit).WithName("SetAccountPassword");
        accounts.MapPost("/{userName}/sign-out", async (string userName, AccountService service, CancellationToken ct) =>
        {
            await service.SignOutAsync(userName, ct);
            return Results.NoContent();
        }).RequirePrivilege(AppsControlResources.Users, PrivilegeActions.Edit).WithName("SignOutAccount");
    }

    /// <summary>Applicants' portal accounts, by national ID. Guarded by the Portal accounts resource.</summary>
    public static void MapPortal(RouteGroupBuilder admin)
    {
        var accounts = admin.MapGroup("/portal-accounts").AddEndpointFilter(Errors);
        accounts.MapGet("/capabilities", (PortalAccountService service, CancellationToken ct) => service.CapabilitiesAsync(ct))
            .RequirePrivilege(AppsControlResources.PortalAccounts, PrivilegeActions.View).WithName("GetPortalAccountCapabilities");
        accounts.MapGet("/", (string? search, int? first, int? max, PortalAccountService service, CancellationToken ct) =>
                service.SearchAsync(search, first ?? 0, max ?? 20, ct))
            .RequirePrivilege(AppsControlResources.PortalAccounts, PrivilegeActions.View).WithName("SearchPortalAccounts");
        accounts.MapGet("/{nationalId}", (string nationalId, PortalAccountService service, CancellationToken ct) => service.GetAsync(nationalId, ct))
            .RequirePrivilege(AppsControlResources.PortalAccounts, PrivilegeActions.View).WithName("GetPortalAccount");
        accounts.MapPost("/", (NewPortalAccountRequest request, PortalAccountService service, CancellationToken ct) => service.CreateAsync(request, ct))
            .RequirePrivilege(AppsControlResources.PortalAccounts, PrivilegeActions.Create).WithName("CreatePortalAccount");
        accounts.MapPost("/{nationalId}/enable", async (string nationalId, PortalAccountService service, CancellationToken ct) =>
        {
            await service.SetEnabledAsync(nationalId, true, ct);
            return Results.NoContent();
        }).RequirePrivilege(AppsControlResources.PortalAccounts, PrivilegeActions.Edit).WithName("EnablePortalAccount");
        accounts.MapPost("/{nationalId}/disable", async (string nationalId, PortalAccountService service, CancellationToken ct) =>
        {
            await service.SetEnabledAsync(nationalId, false, ct);
            return Results.NoContent();
        }).RequirePrivilege(AppsControlResources.PortalAccounts, PrivilegeActions.Edit).WithName("DisablePortalAccount");
        accounts.MapPost("/{nationalId}/password", async (string nationalId, PasswordRequest request, PortalAccountService service, CancellationToken ct) =>
        {
            await service.SetPasswordAsync(nationalId, request, ct);
            return Results.NoContent();
        }).RequirePrivilege(AppsControlResources.PortalAccounts, PrivilegeActions.Edit).WithName("SetPortalAccountPassword");
        accounts.MapPost("/{nationalId}/sign-out", async (string nationalId, PortalAccountService service, CancellationToken ct) =>
        {
            await service.SignOutAsync(nationalId, ct);
            return Results.NoContent();
        }).RequirePrivilege(AppsControlResources.PortalAccounts, PrivilegeActions.Edit).WithName("SignOutPortalAccount");
    }

    private static async ValueTask<object?> Errors(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (AccountException ex)
        {
            return ex.Problem switch
            {
                AccountProblem.Invalid => Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Field ?? "account"] = [ex.Message] }),
                AccountProblem.NotFound => Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message),
                AccountProblem.Unavailable => Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, detail: ex.Message),
                _ => Results.Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message),
            };
        }
    }
}
