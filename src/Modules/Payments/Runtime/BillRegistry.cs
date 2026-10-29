using DynamicCube.Modules.Payments.Domain;
using DynamicCube.Modules.Payments.Persistence;
using DynamicCube.Platform.Payments;
using DynamicCube.Platform.Persistence;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Payments.Runtime;

public sealed record BillSummary(Guid Id, string Number, string ProviderCode, string? ProviderBillNumber, string SubjectType, Guid SubjectId, string? SubjectNumber, string PayerName, decimal Total, string Status, DateTimeOffset IssuedAt, DateTimeOffset DueAt, DateTimeOffset? PaidAt);

public sealed record BillPage(IReadOnlyList<BillSummary> Items, int Total);

public sealed record BillDetail(BillSummary Bill, BillInfo Info, string DescriptionAr, string DescriptionEn, Guid PayerPartyId, decimal Net, decimal Vat, decimal? PaidAmount, IReadOnlyList<BillEventView> Events);

public sealed record BillEventView(DateTimeOffset At, string Type, string Actor, string? Comment);

public sealed record MarkPaidRequest(string? Reference, string? Comment);

public sealed record CancelBillRequest(string? Reason);

public sealed class BillNotFoundException(string message) : Exception(message);

/// <summary>Staff side of bills: search, details with history, recording a payment made elsewhere, cancelling.</summary>
internal sealed class BillRegistry(PaymentsDbContext context, PaymentService payments, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
{
    public async Task<BillPage> SearchAsync(string? search, BillStatus? status, int skip, int take, CancellationToken cancellationToken)
    {
        var query = context.Bills.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(b => EF.Functions.ILike(b.Number, term) || EF.Functions.ILike(b.ProviderBillNumber ?? string.Empty, term)
                || EF.Functions.ILike(b.SubjectNumber ?? string.Empty, term) || EF.Functions.ILike(b.PayerName, term));
        }

        if (status is not null)
        {
            query = query.Where(b => b.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var bills = await query.OrderByDescending(b => b.IssuedAt).Skip(skip).Take(Math.Clamp(take, 1, 200)).ToListAsync(cancellationToken);
        return new BillPage([.. bills.Select(Summary)], total);
    }

    public async Task<BillDetail> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var bill = await context.Bills.AsNoTracking().SingleOrDefaultAsync(b => b.Id == id, cancellationToken) ?? throw new BillNotFoundException($"Bill {id} does not exist.");
        var provider = await context.Providers.AsNoTracking().SingleAsync(p => p.Id == bill.ProviderId, cancellationToken);
        var events = await context.Events.AsNoTracking().Where(e => e.BillId == id).OrderBy(e => e.At)
            .Select(e => new BillEventView(e.At, e.Type, e.Actor, e.Comment)).ToListAsync(cancellationToken);
        return new BillDetail(Summary(bill), PaymentService.Info(bill, provider), bill.DescriptionAr, bill.DescriptionEn, bill.PayerPartyId, bill.Net, bill.Vat, bill.PaidAmount, events);
    }

    /// <summary>A payment received outside the provider's notifications (e.g. a bank transfer), with its reference.</summary>
    public async Task<BillDetail> MarkPaidAsync(Guid id, MarkPaidRequest request, CancellationToken cancellationToken)
    {
        var reference = string.IsNullOrWhiteSpace(request.Reference) ? throw new BillValidationException("reference", "Give the payment reference.") : request.Reference.Trim();
        await using (var work = await unitOfWork.BeginAsync(cancellationToken))
        {
            var bill = await PendingAsync(id, cancellationToken);
            await payments.MarkPaidAsync(bill, bill.Total, reference, clock.GetUtcNow(), user.UserName ?? user.UserId, BillEventTypes.MarkedPaid, Trim(request.Comment) ?? reference, cancellationToken);
            await work.CommitAsync(cancellationToken);
        }

        return await GetAsync(id, cancellationToken);
    }

    public async Task<BillDetail> CancelAsync(Guid id, CancelBillRequest request, CancellationToken cancellationToken)
    {
        var reason = Trim(request.Reason) ?? throw new BillValidationException("reason", "Give the reason.");
        await using (var work = await unitOfWork.BeginAsync(cancellationToken))
        {
            var bill = await PendingAsync(id, cancellationToken);
            await payments.CloseUnpaidAsync(bill, BillStatus.Cancelled, user.UserName ?? user.UserId, reason, cancellationToken);
            await work.CommitAsync(cancellationToken);
        }

        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Expires unpaid bills past their due date (run periodically by the worker); returns how many expired.</summary>
    public async Task<int> ExpireAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var due = await context.Bills.Where(b => b.Status == BillStatus.Pending && b.DueAt <= now).OrderBy(b => b.DueAt).Select(b => b.Id).Take(100).ToListAsync(cancellationToken);
        foreach (var id in due)
        {
            // One bill per transaction: a failure leaves the others to expire.
            await using var work = await unitOfWork.BeginAsync(cancellationToken);
            var bill = await context.Bills.SingleAsync(b => b.Id == id, cancellationToken);
            if (bill.Status == BillStatus.Pending)
            {
                await payments.CloseUnpaidAsync(bill, BillStatus.Expired, "system", null, cancellationToken);
            }

            await work.CommitAsync(cancellationToken);
        }

        return due.Count;
    }

    private async Task<Bill> PendingAsync(Guid id, CancellationToken cancellationToken)
    {
        var bill = await context.Bills.SingleOrDefaultAsync(b => b.Id == id, cancellationToken) ?? throw new BillNotFoundException($"Bill {id} does not exist.");
        return bill.Status == BillStatus.Pending ? bill : throw new BillConflictException($"Bill {bill.Number} is {bill.Status.ToString().ToLowerInvariant()}.");
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static BillSummary Summary(Bill b) =>
        new(b.Id, b.Number, b.ProviderCode, b.ProviderBillNumber, b.SubjectType, b.SubjectId, b.SubjectNumber, b.PayerName, b.Total, b.Status.ToString().ToUpperInvariant(), b.IssuedAt, b.DueAt, b.PaidAt);
}

public sealed class BillConflictException(string message) : Exception(message);

public sealed class BillValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
