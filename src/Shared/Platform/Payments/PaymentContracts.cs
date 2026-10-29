namespace DynamicCube.Platform.Payments;

/// <summary>One fee item of a bill, in SAR: net amount, VAT and total.</summary>
public sealed record BillLine(string Code, string NameAr, string NameEn, decimal Net, decimal Vat, decimal Total);

/// <summary>
/// A bill to issue for something payable (a request's fees): the payer, the lines and how long it stays payable.
/// </summary>
/// <param name="Provider">The payment provider (default: the installation's default provider).</param>
/// <param name="DueDays">Days the bill stays payable (default: the provider's).</param>
public sealed record BillRequest(
    string? Provider,
    string SubjectType,
    Guid SubjectId,
    string? SubjectNumber,
    Guid PayerPartyId,
    string PayerName,
    string DescriptionAr,
    string DescriptionEn,
    IReadOnlyList<BillLine> Lines,
    int? DueDays = null);

/// <summary>A bill as other modules and the applicant see it.</summary>
/// <param name="ProviderBillNumber">The number the payer pays with (e.g. the SADAD bill number).</param>
/// <param name="PayUrl">Where the payer can pay online, when the provider offers it.</param>
public sealed record BillInfo(
    Guid Id,
    string Number,
    string ProviderCode,
    string? ProviderBillNumber,
    string? BillerCode,
    string Status,
    IReadOnlyList<BillLine> Lines,
    decimal Total,
    DateTimeOffset IssuedAt,
    DateTimeOffset DueAt,
    DateTimeOffset? PaidAt,
    string? PaymentReference,
    string? PayUrl,
    string? InstructionsAr,
    string? InstructionsEn);

/// <summary>Issues bills and lists them (implemented by the Payments module); runs inside the caller's unit of work.</summary>
public interface IPayments
{
    Task<BillInfo> CreateBillAsync(BillRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<BillInfo>> OfSubjectAsync(string subjectType, Guid subjectId, CancellationToken cancellationToken);
}

/// <summary>A bill was paid, or will not be (it expired or was cancelled).</summary>
public sealed record BillClosed(Guid BillId, string BillNumber, string SubjectType, Guid SubjectId, bool Paid, string Status, DateTimeOffset At, string? PaymentReference);

/// <summary>
/// Told when a bill closes, in the same unit of work as the payment (the workflow continues a request waiting for it).
/// </summary>
public interface IPaymentListener
{
    Task OnBillClosedAsync(BillClosed bill, CancellationToken cancellationToken);
}

/// <summary>The bill cannot be issued (no provider, the provider refused it…).</summary>
public sealed class PaymentException(string message) : Exception(message);
