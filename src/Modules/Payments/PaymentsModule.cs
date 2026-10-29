using DynamicCube.Modules.Payments.Changes;
using DynamicCube.Modules.Payments.Endpoints;
using DynamicCube.Modules.Payments.Persistence;
using DynamicCube.Modules.Payments.Runtime;
using DynamicCube.Platform;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Communication;
using DynamicCube.Platform.Payments;
using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Payments;

public static class PaymentsModule
{
    public const string ApiPrefix = "/api/payments/v1";

    /// <summary>
    /// Registers payments: providers (change sets), bills registered through Integration Manager operations, signed payment
    /// notifications, expiry, and <see cref="IPayments"/> for the workflow's payment steps.
    /// </summary>
    public static IServiceCollection AddPayments(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<PaymentsDbContext>(configuration, PaymentsDbContext.Schema);
        services.AddChangeSets<PaymentsDbContext>();
        services.AddChangeArea(PaymentChangeTypes.Area, "المدفوعات", "Payments");
        services.AddSystemCatalog(PaymentsCatalog.Contribution);

        services.AddScoped<PaymentProviderApplier>();
        services.AddScoped<IConfigApplier>(sp => sp.GetRequiredService<PaymentProviderApplier>());
        services.AddScoped<IChangeApplier<PaymentsDbContext>>(sp => sp.GetRequiredService<PaymentProviderApplier>());
        services.AddScoped<PaymentService>();
        services.AddScoped<IPayments>(sp => sp.GetRequiredService<PaymentService>());
        services.AddScoped<PaymentCallbacks>();
        services.AddScoped<BillRegistry>();
        services.AddCommunicationEvents(PaymentNotifications.Types);
        return services;
    }

    public static IEndpointRouteBuilder MapPaymentsInternal(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiPrefix).RequireAuthorization().AddEndpointFilter(PaymentEndpoints.Errors).WithTags("Payments");
        PaymentEndpoints.MapInternal(group);
        return endpoints;
    }

    /// <summary>External host: the providers' payment notifications.</summary>
    public static IEndpointRouteBuilder MapPaymentsExternal(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiPrefix).AddEndpointFilter(PaymentEndpoints.Errors).WithTags("Payments");
        PaymentEndpoints.MapCallbacks(group);
        return endpoints;
    }

    /// <summary>Expires unpaid bills past their due date (called periodically by the worker); returns how many expired.</summary>
    public static Task<int> ExpireBillsAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<BillRegistry>().ExpireAsync(cancellationToken);
}

/// <summary>Payment resources and menus contributed to Apps Control.</summary>
internal static class PaymentsCatalog
{
    public const string MenuGroup = "PAYMENTS";

    public static SystemCatalogContribution Contribution { get; } = new()
    {
        Module = "payments",
        Resources =
        [
            new(PaymentResources.Bills, "الفواتير", "Bills", "INTERNAL", null, [CatalogActions.View, CatalogActions.Edit]),
            new(PaymentResources.Config, "إعداد المدفوعات", "Payment configuration", "INTERNAL", null, CatalogActions.Edits),
        ],
        MenuGroup = new CatalogMenuGroup(MenuGroup, "المدفوعات", "Payments", "INTERNAL", "payments", 660),
        Menus =
        [
            new("payments.bills", "الفواتير", "Bills", "/payments", PaymentResources.Bills, "receipt_long", 10),
            new("payments.providers", "مزودو الدفع", "Payment providers", "/payments/providers", PaymentResources.Config, "account_balance", 20),
        ],
    };
}
