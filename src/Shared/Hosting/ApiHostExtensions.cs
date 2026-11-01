using System.Reflection;
using DynamicCube.Platform.Persistence;
using DynamicCube.Platform.PublicApi;
using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace DynamicCube.Hosting;

/// <summary>Shared setup for <c>Internal.Api</c> and <c>External.Api</c>. Each host registers only its own zone's endpoints.</summary>
public static class ApiHostExtensions
{
    public static WebApplicationBuilder AddDynamicCubeApi(this WebApplicationBuilder builder, ApiHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        builder.AddDynamicCubeDefaults(options.AppCode);
        builder.Services.AddMigrationCheck();

        builder.Services.AddSingleton(options);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<IdentityMismatchHandler>();
        builder.Services.AddOpenApi();

        // Keys live in PostgreSQL so every instance (and restart) can unprotect what another one protected.
        builder.Services.AddDataProtection()
            .SetApplicationName("dynamic-cube")
            .PersistKeysToDbContext<PlatformDbContext>();

        var auth = builder.Configuration.GetSection(options.AuthenticationSection);
        var audience = auth["Audience"];

        // Connected apps' tokens (ADR-0036 step 7) carry their own audience and reach only the connected-app endpoints;
        // so do partner systems' tokens for the public API (ADR-0042) on the host that serves it.
        var connectAudience = auth["ConnectAudience"];
        var publicAudience = builder.Configuration["PublicApi:Audience"] is { Length: > 0 } configured ? configured : PublicApiAuthorization.DefaultAudience;
        var audiences = new[] { audience, connectAudience, options.PublicApi ? publicAudience : null }.OfType<string>().Where(a => a.Length > 0).Distinct().ToArray();
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.Authority = auth["Authority"];
                jwt.Audience = audience;
                jwt.RequireHttpsMetadata = auth.GetValue("RequireHttpsMetadata", true);
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters.NameClaimType = "preferred_username";
                if (audiences.Length > 1)
                {
                    jwt.TokenValidationParameters.ValidAudiences = audiences;
                }
            });
        builder.Services.AddAuthorization(authorization =>
        {
            var connect = string.IsNullOrEmpty(connectAudience) ? ConnectedAppAuthorization.DefaultAudience : connectAudience;
            authorization.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireAssertion(context => !ConnectedAppAuthorization.IsConnectedOnly(context.User, audience, connect)
                    && !PublicApiAuthorization.IsPublicOnly(context.User, audience, publicAudience))
                .Build();
            authorization.AddPolicy(ConnectedAppAuthorization.Policy, policy => policy.RequireAuthenticatedUser().RequireClaim("aud", connect));
            authorization.AddPolicy(PublicApiAuthorization.Policy, policy => policy.RequireAuthenticatedUser().RequireClaim("aud", publicAudience));
        });

        return builder;
    }

    public static WebApplication MapDynamicCubeDefaults(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseSerilogRequestLogging();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseAuthorization();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(HostDefaults.ReadyTag) }).AllowAnonymous();

        var api = app.MapGroup("/api");
        api.MapInfoEndpoints();
        return app;
    }

    private static void MapInfoEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/info", (ApiHostOptions options) => Results.Ok(new HostInfo(
                options.AppCode,
                Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown")))
            .AllowAnonymous()
            .WithName("GetHostInfo");

        api.MapGet("/me", (ICurrentUser user) => Results.Ok(new CurrentUserInfo(user.UserId, user.UserName, user.AppCode)))
            .RequireAuthorization()
            .WithName("GetCurrentUser");
    }
}

public sealed record HostInfo(string App, string Version);

public sealed record CurrentUserInfo(string UserId, string? UserName, string App);
