using DynamicCube.Modules.Communication.Changes;
using DynamicCube.Modules.Communication.Endpoints;
using DynamicCube.Modules.Communication.Persistence;
using DynamicCube.Modules.Communication.Runtime;
using DynamicCube.Platform;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Communication;
using DynamicCube.Platform.Lookups;
using DynamicCube.Platform.Security;
using DynamicCube.Platform.Templates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DynamicCube.Modules.Communication;

public static class CommunicationModule
{
    public const string ApiPrefix = "/api/communication/v1";

    /// <summary>
    /// Registers notifications: message templates and notification rules (change sets) matched against the
    /// <see cref="CommunicationEvent"/>s modules raise, delivery by email (SMTP), SMS (an Integration Manager operation) and
    /// in-app, and the message log. Needs the Party Registry and Apps Control's staff directory.
    /// </summary>
    public static IServiceCollection AddCommunication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<CommunicationDbContext>(configuration, CommunicationDbContext.Schema);
        services.AddChangeSets<CommunicationDbContext>();
        services.AddChangeArea(CommunicationChangeTypes.Area, "التنبيهات والرسائل", "Notifications");
        services.AddSystemCatalog(CommunicationCatalog.Contribution);
        services.Configure<CommunicationOptions>(configuration.GetSection(CommunicationOptions.SectionName));

        AddApplier<MessageTemplateApplier>(services);
        AddApplier<NotificationRuleApplier>(services);
        services.TryAddSingleton<LiquidRenderer>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IMessageChannel, EmailChannel>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IMessageChannel, SmsChannel>());
        services.AddScoped<CommunicationService>();
        services.AddOutboxHandler<CommunicationEvent, EventDispatcher>();
        services.AddOutboxHandler<SendMessage, MessageSender>();
        return services;
    }

    public static IEndpointRouteBuilder MapCommunicationInternal(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiPrefix).RequireAuthorization().AddEndpointFilter(CommunicationEndpoints.Errors).WithTags("Communication");
        CommunicationEndpoints.MapInternal(group);
        return endpoints;
    }

    /// <summary>External host: the acting party's notifications.</summary>
    public static IEndpointRouteBuilder MapCommunicationExternal(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiPrefix).RequireAuthorization().AddEndpointFilter(CommunicationEndpoints.Errors).WithTags("Communication");
        CommunicationEndpoints.MapExternal(group);
        return endpoints;
    }

    private static void AddApplier<T>(IServiceCollection services)
        where T : class, IConfigApplier, IChangeApplier<CommunicationDbContext>
    {
        services.AddScoped<T>();
        services.AddScoped<IConfigApplier>(sp => sp.GetRequiredService<T>());
        services.AddScoped<IChangeApplier<CommunicationDbContext>>(sp => sp.GetRequiredService<T>());
    }
}

/// <summary>Communication resources and menus contributed to Apps Control.</summary>
internal static class CommunicationCatalog
{
    public const string MenuGroup = "COMMUNICATION";

    public static SystemCatalogContribution Contribution { get; } = new()
    {
        Module = "communication",
        Classes =
        [
            new CatalogClass("COM_CHANNEL", "قنوات الإرسال", "Dispatch channels", [],
            [
                new CatalogValue("IN_APP", "داخل المنصة", "In-app"),
                new CatalogValue("EMAIL", "البريد الإلكتروني", "Email"),
                new CatalogValue("SMS", "رسالة نصية", "SMS"),
            ]),
        ],
        Resources =
        [
            new(CommunicationResources.Config, "إعداد التنبيهات", "Notification configuration", "INTERNAL", null, CatalogActions.Edits),
            new(CommunicationResources.Messages, "سجل الرسائل", "Message log", "INTERNAL", null, [CatalogActions.View, CatalogActions.Edit]),
        ],
        MenuGroup = new CatalogMenuGroup(MenuGroup, "التنبيهات والرسائل", "Notifications", "INTERNAL", "campaign", 660),
        Menus =
        [
            new("communication.messages", "سجل الرسائل", "Message log", "/communication", CommunicationResources.Messages, "outgoing_mail", 10),
            new("communication.rules", "قواعد التنبيه", "Notification rules", "/communication/rules", CommunicationResources.Config, "notifications_active", 20),
            new("communication.templates", "قوالب الرسائل", "Message templates", "/communication/templates", CommunicationResources.Config, "edit_note", 30),
        ],
    };
}
