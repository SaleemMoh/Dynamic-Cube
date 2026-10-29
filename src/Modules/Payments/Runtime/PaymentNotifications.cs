using System.Globalization;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Payments.Domain;
using DynamicCube.Modules.Payments.Persistence;
using DynamicCube.Platform.Communication;

namespace DynamicCube.Modules.Payments.Runtime;

/// <summary>The events bills raise for notifications: issued (with how to pay), paid, expired unpaid and cancelled.</summary>
internal static class PaymentNotifications
{
    public const string SubjectType = "BILL";

    public const string Issued = "BILL_ISSUED";
    public const string Paid = "BILL_PAID";
    public const string Expired = "BILL_EXPIRED";
    public const string Cancelled = "BILL_CANCELLED";

    public static CommunicationEventType[] Types { get; } =
    [
        Type(Issued, "إصدار فاتورة سداد", "Bill issued"),
        Type(Paid, "سداد فاتورة", "Bill paid"),
        Type(Expired, "انتهاء مهلة سداد فاتورة", "Bill expired unpaid"),
        Type(Cancelled, "إلغاء فاتورة", "Bill cancelled"),
    ];

    public static void Raise(PaymentsDbContext context, TimeProvider clock, string code, Bill bill, PaymentProvider provider, string? payUrl) =>
        context.Raise(clock, code, SubjectType, bill.Id, bill.Number, bill.PayerPartyId, Facts(bill, provider, payUrl));

    private static JsonObject Facts(Bill bill, PaymentProvider provider, string? payUrl) => new()
    {
        ["bill"] = new JsonObject
        {
            ["id"] = bill.Id.ToString(),
            ["number"] = bill.Number,
            ["providerBillNumber"] = bill.ProviderBillNumber,
            ["billerCode"] = provider.BillerCode,
            ["provider"] = provider.Code,
            ["providerNameAr"] = provider.NameAr,
            ["providerNameEn"] = provider.NameEn,
            ["subjectType"] = bill.SubjectType,
            ["subjectId"] = bill.SubjectId.ToString(),
            ["subjectNumber"] = bill.SubjectNumber,
            ["descriptionAr"] = bill.DescriptionAr,
            ["descriptionEn"] = bill.DescriptionEn,
            ["total"] = bill.Total,
            ["status"] = bill.Status.ToString().ToUpperInvariant(),
            ["dueOn"] = Day(bill.DueAt),
            ["paidOn"] = bill.PaidAt is { } paid ? Day(paid) : null,
            ["paymentReference"] = bill.PaymentReference,
            ["payUrl"] = payUrl,
        },
    };

    // Dates as the payer reads them (Saudi time).
    private static string Day(DateTimeOffset at) => at.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static CommunicationEventType Type(string code, string nameAr, string nameEn) => new(code, nameAr, nameEn, "payments", new JsonObject
    {
        ["bill"] = new JsonObject
        {
            ["id"] = "00000000-0000-0000-0000-000000000000",
            ["number"] = "BILL-2026-000001",
            ["providerBillNumber"] = "9100000001",
            ["billerCode"] = "123",
            ["provider"] = "SADAD",
            ["providerNameAr"] = "سداد",
            ["providerNameEn"] = "SADAD",
            ["subjectType"] = "SERVICE_REQUEST",
            ["subjectId"] = "00000000-0000-0000-0000-000000000000",
            ["subjectNumber"] = "CLS-2026-000001",
            ["descriptionAr"] = "رسوم طلب التصنيف",
            ["descriptionEn"] = "Classification request fees",
            ["total"] = 5000,
            ["status"] = "PENDING",
            ["dueOn"] = "2026-11-07",
            ["paidOn"] = null,
            ["paymentReference"] = null,
            ["payUrl"] = "https://pay.example.gov.sa/9100000001",
        },
    });
}
