using DynamicCube.Modules.Workflow.Changes;
using DynamicCube.Modules.Workflow.Endpoints;
using DynamicCube.Modules.Workflow.Persistence;
using DynamicCube.Modules.Workflow.Runtime;
using DynamicCube.Platform;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Communication;
using DynamicCube.Platform.Payments;
using DynamicCube.Platform.Security;
using DynamicCube.Platform.Services;
using DynamicCube.Platform.Workflow;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Workflow;

public static class WorkflowModule
{
    public const string ApiPrefix = "/api/workflow/v1";

    /// <summary>
    /// Registers the workflow engine and the Action Center. Needs Apps Control (<see cref="IStaffDirectory"/>,
    /// <see cref="Platform.Calendars.IWorkingCalendarProvider"/>) and the modules owning subjects (<see cref="IWorkflowSubject"/>).
    /// </summary>
    public static IServiceCollection AddWorkflow(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<WorkflowDbContext>(configuration, WorkflowDbContext.Schema);
        services.AddChangeSets<WorkflowDbContext>();
        services.AddChangeArea(WorkflowChangeTypes.Area, "مسارات العمل", "Workflows");
        services.AddSystemCatalog(WorkflowCatalog.Contribution);

        AddApplier<WorkflowApplier>(services);
        AddApplier<WorkflowVersionApplier>(services);
        services.AddSingleton<DefinitionCache>();
        services.AddScoped<Assignments>();
        services.AddScoped<WorkflowEngine>();
        services.AddScoped<IWorkflowRuntime>(sp => sp.GetRequiredService<WorkflowEngine>());
        services.AddScoped<IPaymentListener, PaymentContinuation>();
        services.AddScoped<ActionCenter>();
        services.AddScoped<SlaMonitor>();
        services.AddScoped<ServiceWorkflows>();
        services.AddScoped<IServiceWorkflows>(sp => sp.GetRequiredService<ServiceWorkflows>());
        services.AddCommunicationEvents(TaskNotifications.Types);
        return services;
    }

    /// <summary>Internal host: inboxes, task work, delegations and instance timelines.</summary>
    public static IEndpointRouteBuilder MapWorkflowInternal(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiPrefix).RequireAuthorization().AddEndpointFilter(WorkflowEndpoints.Errors).WithTags("Workflow");
        WorkflowEndpoints.Map(group);
        return endpoints;
    }

    /// <summary>Marks overdue workflow tasks and instances (called periodically by the worker); returns how many changed.</summary>
    /// <summary>Gives every service configured before 1f its workflow (run by the migrator); returns how many were given one.</summary>
    public static Task<int> BackfillServiceWorkflowsAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<ServiceWorkflows>().BackfillAsync(services.GetRequiredService<IServiceCatalog>(), cancellationToken);

    public static Task<int> CheckSlaAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<SlaMonitor>().RunAsync(cancellationToken);

    private static void AddApplier<T>(IServiceCollection services)
        where T : class, IConfigApplier, IChangeApplier<WorkflowDbContext>
    {
        services.AddScoped<T>();
        services.AddScoped<IConfigApplier>(sp => sp.GetRequiredService<T>());
        services.AddScoped<IChangeApplier<WorkflowDbContext>>(sp => sp.GetRequiredService<T>());
    }
}

/// <summary>Workflow resources and menus contributed to Apps Control.</summary>
internal static class WorkflowCatalog
{
    public const string MenuGroup = "ACTION_CENTER";

    private static readonly string[] None = [];

    public static SystemCatalogContribution Contribution { get; } = new()
    {
        Module = "workflow",
        Resources =
        [
            // Configuration roles maintain workflows; they do not work on requests.
            new(WorkflowResources.ActionCenter, "مركز الإجراءات", "Action Center", "INTERNAL", null, [CatalogActions.View],
                RoleActions: new Dictionary<string, string[]> { [BuiltInRoles.ConfigMaker] = None, [BuiltInRoles.ConfigChecker] = None }),
            new(WorkflowResources.Supervision, "متابعة المهام", "Task supervision", "INTERNAL", WorkflowResources.ActionCenter, [CatalogActions.View, CatalogActions.Edit],
                RoleActions: new Dictionary<string, string[]> { [BuiltInRoles.ConfigMaker] = None, [BuiltInRoles.ConfigChecker] = None }),
            new(WorkflowResources.Config, "إعداد مسارات العمل", "Workflow configuration", "INTERNAL", null, CatalogActions.Edits),
        ],
        MenuGroup = new CatalogMenuGroup(MenuGroup, "مركز الإجراءات", "Action Center", "INTERNAL", "assignment_turned_in", 500),
        Menus =
        [
            new("action-center.inbox", "المهام", "Tasks", "/action-center", WorkflowResources.ActionCenter, "inbox", 10),
            new("action-center.supervision", "متابعة المهام", "Task supervision", "/action-center/supervision", WorkflowResources.Supervision, "monitoring", 20),
            new("workflows.workflows", "مسارات العمل", "Workflows", "/workflows", WorkflowResources.Config, "account_tree", 30),
            new("workflows.versions", "إصدارات المسارات", "Workflow versions", "/workflows/versions", WorkflowResources.Config, "history_edu", 40),
        ],
    };
}
