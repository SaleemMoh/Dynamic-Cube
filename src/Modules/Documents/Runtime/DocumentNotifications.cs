using System.Globalization;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Documents.Domain;
using DynamicCube.Modules.Documents.Persistence;
using DynamicCube.Platform.Communication;

namespace DynamicCube.Modules.Documents.Runtime;

/// <summary>The events issued documents raise for notifications (issued, amended, status changes, nearing expiry, expired).</summary>
internal static class DocumentNotifications
{
    public const string SubjectType = "DOCUMENT";

    public const string Issued = "DOCUMENT_ISSUED";
    public const string Amended = "DOCUMENT_AMENDED";
    public const string Suspended = "DOCUMENT_SUSPENDED";
    public const string Reinstated = "DOCUMENT_REINSTATED";
    public const string Cancelled = "DOCUMENT_CANCELLED";
    public const string Expiring = "DOCUMENT_EXPIRING";
    public const string Expired = "DOCUMENT_EXPIRED";

    public static CommunicationEventType[] Types { get; } =
    [
        Type(Issued, "إصدار مستند", "Document issued"),
        Type(Amended, "تعديل مستند", "Document amended"),
        Type(Suspended, "إيقاف مستند", "Document suspended"),
        Type(Reinstated, "إعادة تفعيل مستند", "Document reinstated"),
        Type(Cancelled, "إلغاء مستند", "Document cancelled"),
        Type(Expiring, "اقتراب انتهاء مستند (حسب أيام التذكير)", "Document nearing expiry (on its reminder days)"),
        Type(Expired, "انتهاء مستند", "Document expired"),
    ];

    /// <param name="daysLeft">For reminders: the reminder day reached and the days left.</param>
    public static void Raise(DocumentsDbContext context, TimeProvider clock, string code, IssuedDocument document, DocumentType type, string verifyUrl, string? reason, (int Alert, int DaysLeft)? daysLeft = null) =>
        context.Raise(clock, code, SubjectType, document.Id, document.Number, document.HolderPartyId, new JsonObject
        {
            ["document"] = new JsonObject
            {
                ["id"] = document.Id.ToString(),
                ["number"] = document.Number,
                ["type"] = type.Code,
                ["typeNameAr"] = type.NameAr,
                ["typeNameEn"] = type.NameEn,
                ["status"] = document.Status.ToString().ToUpperInvariant(),
                ["version"] = document.CurrentVersion,
                ["issuedOn"] = DocumentDatesText(document.IssuedAt),
                ["expiresOn"] = document.ExpiresAt is { } expires ? DocumentDatesText(expires) : null,
                ["holderNameAr"] = document.HolderNameAr,
                ["holderNameEn"] = document.HolderNameEn,
                ["reason"] = reason,
                ["verifyUrl"] = verifyUrl,
                ["alertDays"] = daysLeft?.Alert,
                ["daysLeft"] = daysLeft?.DaysLeft,
            },
        });

    // Dates as the applicant reads them (Saudi time, no daylight saving).
    private static string DocumentDatesText(DateTimeOffset at) => at.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static CommunicationEventType Type(string code, string nameAr, string nameEn) => new(code, nameAr, nameEn, "documents", new JsonObject
    {
        ["document"] = new JsonObject
        {
            ["id"] = Guid.Empty.ToString(),
            ["number"] = "CC-2026-000001",
            ["type"] = "CLASSIFICATION_CERT",
            ["typeNameAr"] = "شهادة التصنيف",
            ["typeNameEn"] = "Classification certificate",
            ["status"] = "ACTIVE",
            ["version"] = 1,
            ["issuedOn"] = "2026-01-01",
            ["expiresOn"] = "2028-01-01",
            ["holderNameAr"] = "مؤسسة المثال",
            ["holderNameEn"] = "Sample Establishment",
            ["reason"] = "اعتماد الطلب",
            ["verifyUrl"] = "https://portal.example.gov.sa/verify/ABCDEFGHJKLMNPQRSTUV",
            ["alertDays"] = 90,
            ["daysLeft"] = 90,
        },
    });
}
