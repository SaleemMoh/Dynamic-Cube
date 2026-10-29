using DynamicCube.Platform.Payments;
using DynamicCube.Platform.Persistence;

namespace DynamicCube.Modules.Payments.Domain;

/// <summary>
/// Where bills are paid (SADAD, Tahseel, a payment gateway): the Integration Manager operation that registers a bill with
/// the provider, how long bills stay payable, the payer's instructions, and how the provider's payment notifications are
/// read and verified. Without an operation, bills are numbered locally and staff record payments.
/// </summary>
public sealed class PaymentProvider : ConfigEntity
{
    /// <summary>Operation that registers a bill; it receives <c>billNumber</c>, <c>amount</c>, <c>dueDate</c>, <c>payerId</c>, <c>payerName</c> and <c>description</c>.</summary>
    public string? CreateOperation { get; set; }

    /// <summary>Output of the operation holding the number the payer pays with (default <c>billNumber</c>).</summary>
    public string BillNumberOutput { get; set; } = "billNumber";

    /// <summary>The biller code shown to the payer (SADAD).</summary>
    public string? BillerCode { get; set; }

    public int DueDays { get; set; } = 7;

    /// <summary>Online payment page, with <c>{billNumber}</c> and <c>{amount}</c> placeholders.</summary>
    public string? PayUrlTemplate { get; set; }

    /// <summary>How to pay, shown on the request page.</summary>
    public string? InstructionsAr { get; set; }

    public string? InstructionsEn { get; set; }

    /// <summary>Secret name: payment notifications must carry an HMAC-SHA256 signature made with <c>Secrets:{name}:key</c>.</summary>
    public string? CallbackSecret { get; set; }

    public CallbackFields Callback { get; set; } = new();

    /// <summary>Used when a bill names no provider.</summary>
    public bool IsDefault { get; set; }
}

/// <summary>Where a payment notification carries its values (dot paths into its JSON body).</summary>
public sealed class CallbackFields
{
    public string BillNumber { get; set; } = "billNumber";

    public string Status { get; set; } = "status";

    /// <summary>The status value of a payment (other values are recorded and ignored).</summary>
    public string PaidValue { get; set; } = "PAID";

    public string Amount { get; set; } = "amount";

    public string Reference { get; set; } = "paymentReference";

    public string PaidAt { get; set; } = "paidAt";
}

public enum BillStatus
{
    Pending,
    Paid,
    Expired,
    Cancelled,
}

/// <summary>A bill: what is payable, by whom, for what, until when, and how it was paid.</summary>
public sealed class Bill : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>The platform's number, e.g. <c>BILL-2026-000001</c> (also sent to the provider).</summary>
    public string Number { get; set; } = string.Empty;

    public Guid ProviderId { get; set; }

    public string ProviderCode { get; set; } = string.Empty;

    /// <summary>The number the payer pays with (e.g. the SADAD number).</summary>
    public string? ProviderBillNumber { get; set; }

    public string SubjectType { get; set; } = string.Empty;

    public Guid SubjectId { get; set; }

    public string? SubjectNumber { get; set; }

    public Guid PayerPartyId { get; set; }

    public string PayerName { get; set; } = string.Empty;

    public string DescriptionAr { get; set; } = string.Empty;

    public string DescriptionEn { get; set; } = string.Empty;

    public List<BillLine> Lines { get; set; } = [];

    public decimal Net { get; set; }

    public decimal Vat { get; set; }

    public decimal Total { get; set; }

    public BillStatus Status { get; set; }

    public DateTimeOffset IssuedAt { get; set; }

    public DateTimeOffset DueAt { get; set; }

    public DateTimeOffset? PaidAt { get; set; }

    public decimal? PaidAmount { get; set; }

    public string? PaymentReference { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>The bill's history, including payment notifications that were refused.</summary>
public sealed class BillEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid BillId { get; set; }

    public DateTimeOffset At { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Actor { get; set; } = string.Empty;

    public string? Comment { get; set; }
}

public static class BillEventTypes
{
    public const string Issued = "ISSUED";
    public const string Paid = "PAID";
    public const string MarkedPaid = "MARKED_PAID";
    public const string Expired = "EXPIRED";
    public const string Cancelled = "CANCELLED";
    public const string NotificationIgnored = "NOTIFICATION_IGNORED";
    public const string NotificationRefused = "NOTIFICATION_REFUSED";
}

/// <summary>Counters for bill numbers (one row per year).</summary>
public sealed class BillNumberSequence
{
    public string Key { get; set; } = string.Empty;

    public long Value { get; set; }
}
