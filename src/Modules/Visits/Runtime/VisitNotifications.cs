using System.Globalization;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Visits.Domain;
using DynamicCube.Modules.Visits.Persistence;
using DynamicCube.Platform.Communication;

namespace DynamicCube.Modules.Visits.Runtime;

/// <summary>
/// The events visits raise for notifications: requested (for supervisors to schedule), scheduled (the applicant's date),
/// assigned (the inspector's job), completed and cancelled.
/// </summary>
internal static class VisitNotifications
{
    public const string SubjectType = "VISIT";

    public const string Requested = "VISIT_REQUESTED";
    public const string Scheduled = "VISIT_SCHEDULED";
    public const string Assigned = "VISIT_ASSIGNED";
    public const string Completed = "VISIT_COMPLETED";
    public const string Cancelled = "VISIT_CANCELLED";

    public static CommunicationEventType[] Types { get; } =
    [
        Type(Requested, "طلب زيارة ميدانية", "Visit requested"),
        Type(Scheduled, "تحديد موعد زيارة", "Visit scheduled"),
        Type(Assigned, "إسناد زيارة لمفتش", "Visit assigned to an inspector"),
        Type(Completed, "إتمام زيارة", "Visit completed"),
        Type(Cancelled, "إلغاء زيارة", "Visit cancelled"),
    ];

    public static void Raise(VisitsDbContext context, TimeProvider clock, string code, Visit visit, VisitType type) =>
        context.Raise(clock, code, SubjectType, visit.Id, visit.Number, visit.PartyId, Facts(visit, type));

    private static JsonObject Facts(Visit visit, VisitType type)
    {
        var outcome = type.Outcomes.FirstOrDefault(o => o.Code == visit.Outcome);
        return new JsonObject
        {
            ["visit"] = new JsonObject
            {
                ["id"] = visit.Id.ToString(),
                ["number"] = visit.Number,
                ["type"] = type.Code,
                ["typeNameAr"] = type.NameAr,
                ["typeNameEn"] = type.NameEn,
                ["mode"] = VisitService.Upper(visit.Mode),
                ["status"] = VisitService.Upper(visit.Status),
                ["subjectType"] = visit.SubjectType,
                ["subjectId"] = visit.SubjectId.ToString(),
                ["subjectNumber"] = visit.SubjectNumber,
                ["titleAr"] = visit.TitleAr,
                ["titleEn"] = visit.TitleEn,
                ["siteAddress"] = visit.SiteAddress,
                ["inspector"] = visit.Inspector,
                ["inspectorName"] = visit.InspectorName,
                ["inspectorRole"] = type.InspectorRole,
                ["scheduledOn"] = visit.ScheduledAt is { } at ? Day(at) : null,
                ["scheduledTime"] = visit.ScheduledAt is { } time ? time.ToOffset(TimeSpan.FromHours(3)).ToString("HH:mm", CultureInfo.InvariantCulture) : null,
                ["dueOn"] = visit.DueAt is { } due ? Day(due) : null,
                ["outcome"] = visit.Outcome,
                ["outcomeNameAr"] = outcome?.NameAr,
                ["outcomeNameEn"] = outcome?.NameEn,
                ["outcomeKind"] = VisitService.Upper(outcome?.Kind),
                ["cancelReason"] = visit.CancelReason,
            },
        };
    }

    // Dates as the applicant reads them (Saudi time).
    private static string Day(DateTimeOffset at) => at.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static CommunicationEventType Type(string code, string nameAr, string nameEn) => new(code, nameAr, nameEn, "visits", new JsonObject
    {
        ["visit"] = new JsonObject
        {
            ["id"] = "00000000-0000-0000-0000-000000000000",
            ["number"] = "VIS-2026-000001",
            ["type"] = "SITE_INSPECTION",
            ["typeNameAr"] = "معاينة الموقع",
            ["typeNameEn"] = "Site inspection",
            ["mode"] = "FIELD",
            ["status"] = "SCHEDULED",
            ["subjectType"] = "SERVICE_REQUEST",
            ["subjectId"] = "00000000-0000-0000-0000-000000000000",
            ["subjectNumber"] = "CLS-2026-000001",
            ["titleAr"] = "طلب تصنيف جديد",
            ["titleEn"] = "New classification request",
            ["siteAddress"] = "الرياض، حي العليا",
            ["inspector"] = "inspector1",
            ["inspectorName"] = "المفتش الأول",
            ["inspectorRole"] = "FIELD_INSPECTOR",
            ["scheduledOn"] = "2026-11-03",
            ["scheduledTime"] = "10:00",
            ["dueOn"] = "2026-11-05",
            ["outcome"] = null,
            ["outcomeNameAr"] = null,
            ["outcomeNameEn"] = null,
            ["outcomeKind"] = null,
            ["cancelReason"] = null,
        },
    });
}
