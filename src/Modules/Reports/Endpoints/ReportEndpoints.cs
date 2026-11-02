using DynamicCube.Modules.Reports.Changes;
using DynamicCube.Modules.Reports.Persistence;
using DynamicCube.Modules.Reports.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Reports;
using DynamicCube.Platform.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Reports.Endpoints;

internal static class ReportEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        // The reports the user may open, and one report: each is checked against the report's own resource.
        group.MapGet("/reports", async (ReportService reports, CancellationToken ct) => TypedResults.Ok(await reports.MineAsync(ct)))
            .RequirePrivilege(ReportResources.Root, CatalogActions.View).WithName("ListMyReports");
        group.MapGet("/reports/{code}", async (string code, ReportService reports, CancellationToken ct) => TypedResults.Ok(await reports.ViewAsync(code, ct)))
            .WithName("GetReport");
        group.MapPost("/reports/{code}/run", async (string code, RunReportRequest request, ReportService reports, CancellationToken ct) =>
            TypedResults.Ok(await reports.RunAsync(code, request, ct))).WithName("RunReport");
        group.MapPost("/reports/{code}/export", async (string code, string? format, string? lang, Dictionary<string, string?>? filters, ReportService reports, CancellationToken ct) =>
        {
            var file = await reports.ExportAsync(code, filters, format ?? "XLSX", !string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase), ct);
            return Results.File(file.Content, file.ContentType, file.FileName);
        }).WithName("ExportReport");
        group.MapPost("/reports/{code}/drill", async (string code, DrillRequest request, ReportService reports, CancellationToken ct) =>
            TypedResults.Ok(await reports.DrillAsync(code, request, ct))).WithName("DrillIntoReport");
        group.MapPost("/reports/{code}/drill/export", async (string code, string? format, string? lang, DrillRequest request, ReportService reports, CancellationToken ct) =>
        {
            var file = await reports.ExportDrillAsync(code, request, format ?? "XLSX", !string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase), ct);
            return Results.File(file.Content, file.ContentType, file.FileName);
        }).WithName("ExportReportDrill");
        group.MapPost("/reports/{code}/saved-filters", async (string code, SaveFilterRequest request, ReportService reports, CancellationToken ct) =>
            TypedResults.Ok(await reports.SaveFilterAsync(code, request, ct))).WithName("SaveReportFilters");
        group.MapDelete("/reports/{code}/saved-filters/{id:guid}", async (string code, Guid id, ReportService reports, CancellationToken ct) =>
        {
            await reports.DeleteFilterAsync(code, id, ct);
            return TypedResults.NoContent();
        }).WithName("DeleteReportFilters");

        // Dashboards (ADR-0047): shared ones (each checked against its own resource) and the user's own; widgets run one by one.
        var dashboards = group.MapGroup(string.Empty).RequirePrivilege(ReportResources.Root, CatalogActions.View);
        dashboards.MapGet("/dashboards", async (DashboardService service, CancellationToken ct) => TypedResults.Ok(await service.MineAsync(ct))).WithName("ListMyDashboards");
        dashboards.MapGet("/dashboards/{code}", async (string code, DashboardService service, CancellationToken ct) => TypedResults.Ok(await service.SharedAsync(code, ct))).WithName("GetDashboard");
        dashboards.MapPost("/dashboards/{code}/widgets/{widget}", async (string code, string widget, RunWidgetRequest request, DashboardService service, CancellationToken ct) =>
            TypedResults.Ok(await service.RunSharedAsync(code, widget, request, ct))).WithName("RunDashboardWidget");
        dashboards.MapPost("/my-dashboards", async (SavePersonalDashboardRequest request, DashboardService service, CancellationToken ct) =>
            TypedResults.Ok(await service.SavePersonalAsync(null, request, ct))).WithName("CreateMyDashboard");
        dashboards.MapPost("/my-dashboards/pin", async (PinWidgetRequest request, DashboardService service, CancellationToken ct) =>
            TypedResults.Ok(await service.PinAsync(request, ct))).WithName("PinToMyDashboard");
        dashboards.MapGet("/my-dashboards/{id:guid}", async (Guid id, DashboardService service, CancellationToken ct) => TypedResults.Ok(await service.PersonalAsync(id, ct))).WithName("GetMyDashboard");
        dashboards.MapPut("/my-dashboards/{id:guid}", async (Guid id, SavePersonalDashboardRequest request, DashboardService service, CancellationToken ct) =>
            TypedResults.Ok(await service.SavePersonalAsync(id, request, ct))).WithName("SaveMyDashboard");
        dashboards.MapDelete("/my-dashboards/{id:guid}", async (Guid id, DashboardService service, CancellationToken ct) =>
        {
            await service.DeletePersonalAsync(id, ct);
            return TypedResults.NoContent();
        }).WithName("DeleteMyDashboard");
        dashboards.MapPost("/my-dashboards/{id:guid}/widgets/{widget}", async (Guid id, string widget, RunWidgetRequest request, DashboardService service, CancellationToken ct) =>
            TypedResults.Ok(await service.RunPersonalAsync(id, widget, request, ct))).WithName("RunMyDashboardWidget");

        // Scheduled deliveries: their log, and sending a schedule now.
        group.MapGet("/admin/deliveries", async (string? schedule, int? skip, int? take, ReportsDbContext db, CancellationToken ct) =>
        {
            var query = db.Deliveries.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(schedule))
            {
                query = query.Where(d => d.ScheduleCode == schedule);
            }

            var total = await query.CountAsync(ct);
            var items = await query.OrderByDescending(d => d.StartedAt).Skip(Math.Max(0, skip ?? 0)).Take(Math.Clamp(take ?? 50, 1, 200)).ToListAsync(ct);
            return TypedResults.Ok(new { items, total });
        }).RequirePrivilege(ReportResources.Schedules, CatalogActions.View).WithName("ListReportDeliveries");
        group.MapPost("/admin/schedules/{code}/send-now", async Task<IResult> (string code, ReportsDbContext db, ReportScheduler scheduler, TimeProvider clock, CancellationToken ct) =>
        {
            var schedule = await db.Schedules.SingleOrDefaultAsync(s => s.Code == code && s.IsActive, ct);
            return schedule is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(await scheduler.RunAsync(schedule, clock.GetUtcNow(), ct));
        }).RequirePrivilege(ReportResources.Schedules, CatalogActions.Edit).WithName("SendReportScheduleNow");

        // The designer's preview: an unsaved report run within the designer's own access.
        group.MapPost("/admin/preview", async (PreviewReportRequest request, ReportService reports, CancellationToken ct) =>
            TypedResults.Ok(await reports.PreviewAsync(request, ct))).RequirePrivilege(ReportResources.Config, CatalogActions.Create).WithName("PreviewReport");
        group.MapPost("/admin/preview-chart", async (PreviewChartRequest request, ReportService reports, CancellationToken ct) =>
            TypedResults.Ok(await reports.PreviewChartAsync(request, ct))).RequirePrivilege(ReportResources.Config, CatalogActions.Create).WithName("PreviewReportChart");
    }

    /// <summary>Maps report exceptions to problem responses.</summary>
    public static async ValueTask<object?> Errors(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (ReportInputException ex)
        {
            return Results.ValidationProblem(ex.Errors.ToDictionary(e => e.Key, e => e.Value), title: "The filters are invalid.");
        }
        catch (ChangeValidationException ex)
        {
            return Results.ValidationProblem(ex.Errors.ToDictionary(e => e.Key, e => e.Value), title: "The report or dashboard is invalid.");
        }
        catch (DashboardNotFoundException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (ReportNotFoundException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (ReportForbiddenException ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("The report's query", StringComparison.Ordinal))
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "The report no longer runs.", detail: ex.Message);
        }
    }
}
