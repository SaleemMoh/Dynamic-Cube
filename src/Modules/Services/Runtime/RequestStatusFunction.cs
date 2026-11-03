using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Persistence;
using DynamicCube.Platform.PublicApi;
using DynamicCube.Platform.Workflow;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Services.Runtime;

/// <summary>
/// A submitted request's status by its number (ADR-0042): the service, the status the applicant sees, where it is in its
/// workflow now and its timeline (without comments or people). A request outside the caller's companies and branches,
/// or a draft, is not found.
/// </summary>
internal sealed class RequestStatusFunction(ServicesDbContext context, IWorkflowProgress workflows) : IApiFunction
{
    public const string Code = "REQUEST_STATUS";

    public ApiFunctionInfo Info { get; } = new(
        Code,
        "حالة طلب",
        "Request status",
        "حالة الطلب برقمه: الخدمة، والحالة كما يراها المتقدم، والخطوة الحالية في مسار العمل وموعدها، وسجل الطلب.",
        "A request's status by its number: the service, the status the applicant sees, the current workflow step with its deadline, and the request's timeline.",
        [new("number", true, "رقم الطلب (مثل CLS-2026-000123)", "The request number (e.g. CLS-2026-000123)")],
        Sample());

    public async Task<ApiFunctionResult> InvokeAsync(IReadOnlyDictionary<string, string?> inputs, ApiCaller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(caller);
        var number = inputs.GetValueOrDefault("number")?.Trim();
        if (string.IsNullOrEmpty(number))
        {
            throw new ApiInputException(new Dictionary<string, string[]> { ["number"] = ["The request number is required."] });
        }

        var request = await context.Requests.AsNoTracking().SingleOrDefaultAsync(r => r.Number == number, cancellationToken);
        if (request is null || request.StatusCategory == StatusCategory.Draft || !caller.Sees(request.CompanyCode, request.BranchCode))
        {
            return ApiFunctionResult.NotFound;
        }

        var service = await context.Services.AsNoTracking().Where(s => s.Code == request.ServiceCode)
            .Select(s => new { s.NameAr, s.NameEn }).SingleOrDefaultAsync(cancellationToken);
        var events = await context.RequestEvents.AsNoTracking().Where(e => e.RequestId == request.Id && e.Type != RequestEventTypes.Created)
            .OrderBy(e => e.At).ThenBy(e => e.Id).Select(e => new { e.At, e.Type, e.ToStatus }).ToListAsync(cancellationToken);
        var progress = await workflows.ProgressAsync(RequestSubject.Type, request.Id, cancellationToken);
        return new ApiFunctionResult(new JsonObject
        {
            ["number"] = request.Number,
            ["service"] = new JsonObject { ["code"] = request.ServiceCode, ["nameAr"] = service?.NameAr, ["nameEn"] = service?.NameEn },
            ["status"] = Upper(request.StatusCategory.ToString()),
            ["displayStatus"] = request.DisplayStatus,
            ["company"] = request.CompanyCode,
            ["branch"] = request.BranchCode,
            ["submittedAt"] = request.SubmittedAt,
            ["closedAt"] = request.ClosedAt,
            ["workflow"] = progress is null ? null : Workflow(progress),
            ["timeline"] = new JsonArray([.. events.Select(e => (JsonNode)new JsonObject
            {
                ["at"] = e.At,
                ["type"] = e.Type,
                ["status"] = e.ToStatus is { } to ? Upper(to.ToString()) : null,
            })]),
        });
    }

    private static JsonObject Workflow(WorkflowProgress progress) => new()
    {
        ["status"] = progress.Status,
        ["startedAt"] = progress.StartedAt,
        ["completedAt"] = progress.CompletedAt,
        ["dueAt"] = progress.DueAt,
        ["outcome"] = progress.Outcome,
        ["stepsDone"] = progress.StepsDone,
        ["currentSteps"] = new JsonArray([.. progress.CurrentSteps.Select(s => (JsonNode)new JsonObject
        {
            ["code"] = s.Code,
            ["nameAr"] = s.NameAr,
            ["nameEn"] = s.NameEn,
            ["since"] = s.Since,
            ["dueAt"] = s.DueAt,
        })]),
    };

    /// <summary><c>PendingCustomer</c> → <c>PENDING_CUSTOMER</c>, as statuses are written everywhere else.</summary>
    private static string Upper(string name) => JsonNamingPolicy.SnakeCaseUpper.ConvertName(name);

    private static JsonObject Sample() => new()
    {
        ["number"] = "CLS-2026-000123",
        ["service"] = new JsonObject { ["code"] = "CLASSIFICATION", ["nameAr"] = "تصنيف", ["nameEn"] = "Classification" },
        ["status"] = "IN_PROGRESS",
        ["displayStatus"] = "UNDER_REVIEW",
        ["company"] = "NHC",
        ["branch"] = "RIYADH",
        ["submittedAt"] = "2026-10-01T09:30:00+03:00",
        ["closedAt"] = null,
        ["workflow"] = new JsonObject
        {
            ["status"] = "RUNNING",
            ["startedAt"] = "2026-10-01T09:30:01+03:00",
            ["completedAt"] = null,
            ["dueAt"] = "2026-10-08T15:00:00+03:00",
            ["outcome"] = null,
            ["stepsDone"] = 2,
            ["currentSteps"] = new JsonArray(new JsonObject
            {
                ["code"] = "REVIEW",
                ["nameAr"] = "مراجعة الطلب",
                ["nameEn"] = "Review the request",
                ["since"] = "2026-10-01T09:30:02+03:00",
                ["dueAt"] = "2026-10-03T15:00:00+03:00",
            }),
        },
        ["timeline"] = new JsonArray(new JsonObject { ["at"] = "2026-10-01T09:30:00+03:00", ["type"] = "SUBMITTED", ["status"] = "SUBMITTED" }),
    };
}
