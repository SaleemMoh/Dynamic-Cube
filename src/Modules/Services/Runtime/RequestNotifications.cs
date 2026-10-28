using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Persistence;
using DynamicCube.Platform.Communication;

namespace DynamicCube.Modules.Services.Runtime;

/// <summary>The events service requests raise for notifications (§6.10: rejected, returned for documents, completed…).</summary>
internal static class RequestNotifications
{
    public const string Submitted = "REQUEST_SUBMITTED";
    public const string Resubmitted = "REQUEST_RESUBMITTED";
    public const string Returned = "REQUEST_RETURNED";
    public const string Approved = "REQUEST_APPROVED";
    public const string Rejected = "REQUEST_REJECTED";
    public const string Closed = "REQUEST_CLOSED";

    public static CommunicationEventType[] Types { get; } =
    [
        Type(Submitted, "تقديم طلب", "Request submitted"),
        Type(Resubmitted, "إعادة تقديم طلب بعد التصحيح", "Request resubmitted after correction"),
        Type(Returned, "إعادة الطلب لاستكمال النواقص", "Request returned for missing information"),
        Type(Approved, "اكتمال الطلب بالموافقة", "Request approved"),
        Type(Rejected, "رفض الطلب", "Request rejected"),
        Type(Closed, "إغلاق الطلب", "Request closed"),
    ];

    /// <summary>The event that a move to <paramref name="to"/> raises, if any.</summary>
    public static string? For(StatusCategory to) => to switch
    {
        StatusCategory.PendingCustomer => Returned,
        StatusCategory.Approved => Approved,
        StatusCategory.Rejected => Rejected,
        StatusCategory.Closed => Closed,
        _ => null,
    };

    public static void Raise(ServicesDbContext context, TimeProvider clock, string code, ServiceRequest request, string serviceNameAr, string serviceNameEn, string? comment) =>
        context.Raise(clock, code, RequestSubject.Type, request.Id, request.Number, request.BeneficiaryPartyId, new JsonObject
        {
            ["request"] = new JsonObject
            {
                ["id"] = request.Id.ToString(),
                ["number"] = request.Number,
                ["service"] = request.ServiceCode,
                ["serviceNameAr"] = serviceNameAr,
                ["serviceNameEn"] = serviceNameEn,
                ["level"] = request.LevelCode,
                ["status"] = JsonNamingPolicy.SnakeCaseUpper.ConvertName(request.StatusCategory.ToString()),
                ["displayStatus"] = request.DisplayStatus,
                ["comment"] = comment,
                ["submittedAt"] = request.SubmittedAt?.ToString("O", CultureInfo.InvariantCulture),
                ["submitterName"] = request.SubmitterName,
                ["submitterPartyId"] = request.SubmitterPartyId.ToString(),
                ["feeTotal"] = request.FeeTotal,
            },
            ["data"] = JsonNode.Parse(request.Data),
        });

    private static CommunicationEventType Type(string code, string nameAr, string nameEn) => new(code, nameAr, nameEn, "services", new JsonObject
    {
        ["request"] = new JsonObject
        {
            ["id"] = Guid.Empty.ToString(),
            ["number"] = "CLS-2026-000001",
            ["service"] = "CLASSIFICATION_NEW",
            ["serviceNameAr"] = "طلب تصنيف جديد",
            ["serviceNameEn"] = "New classification",
            ["level"] = "STANDARD",
            ["status"] = "IN_PROGRESS",
            ["displayStatus"] = "UNDER_REVIEW",
            ["comment"] = "يرجى إرفاق القوائم المالية.",
            ["submittedAt"] = "2026-01-01T09:00:00+03:00",
            ["submitterName"] = "محمد أحمد",
            ["submitterPartyId"] = Guid.Empty.ToString(),
            ["feeTotal"] = 1000,
        },
        ["data"] = new JsonObject(),
    });
}
