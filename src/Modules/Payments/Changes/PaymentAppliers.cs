using System.Text.RegularExpressions;
using DynamicCube.Modules.Payments.Domain;
using DynamicCube.Modules.Payments.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Integration;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Payments.Changes;

public sealed class PaymentProviderPayload : ConfigPayload
{
    public string? CreateOperation { get; set; }

    public string? BillNumberOutput { get; set; }

    public string? BillerCode { get; set; }

    public int DueDays { get; set; } = 7;

    public string? PayUrlTemplate { get; set; }

    public string? InstructionsAr { get; set; }

    public string? InstructionsEn { get; set; }

    public string? CallbackSecret { get; set; }

    public CallbackFields? Callback { get; set; }

    public bool IsDefault { get; set; }
}

internal sealed partial class PaymentProviderApplier(PaymentsDbContext db)
    : ConfigApplier<PaymentsDbContext, ConfigRefs<PaymentsDbContext>, PaymentProvider, PaymentProviderPayload>(db)
{
    public override string EntityType => PaymentChangeTypes.Provider;

    public override string Area => PaymentChangeTypes.Area;

    public override string ResourceCode => PaymentResources.Config;

    protected override Regex CodePattern => ValidationErrors.IdentifierCode();

    protected override ConfigRefs<PaymentsDbContext> CreateRefs(PaymentsDbContext context, ChangeSet? draft) => new(context, draft);

    protected override IQueryable<PaymentProvider> Query(PaymentsDbContext context) => context.Providers;

    protected override async Task ApplyAsync(PaymentProvider entity, PaymentProviderPayload payload, ConfigRefs<PaymentsDbContext> refs, ValidationErrors errors, PaymentProvider? existing, CancellationToken cancellationToken)
    {
        errors.MaxLength("createOperation", payload.CreateOperation, 100);
        errors.MaxLength("billerCode", payload.BillerCode, 50);
        errors.MaxLength("instructionsAr", payload.InstructionsAr, 2000);
        errors.MaxLength("instructionsEn", payload.InstructionsEn, 2000);
        if (payload.DueDays is < 1 or > 90)
        {
            errors.Add("dueDays", "Must be between 1 and 90.");
        }

        if (Blank(payload.PayUrlTemplate) is { } url && !WebUrl().IsMatch(url))
        {
            errors.Add("payUrlTemplate", "Use an http(s) address; {billNumber} and {amount} are replaced.");
        }

        if (Blank(payload.CallbackSecret) is { } secret && !SecretName().IsMatch(secret))
        {
            errors.Add("callbackSecret", "A secret name: upper-case letters, digits and _.");
        }

        var callback = payload.Callback ?? new CallbackFields();
        foreach (var (field, path) in new[] { ("billNumber", callback.BillNumber), ("status", callback.Status), ("amount", callback.Amount), ("reference", callback.Reference), ("paidAt", callback.PaidAt) })
        {
            if (!JsonPaths.IsValid(path))
            {
                errors.Add($"callback.{field}", "Not a valid path.");
            }
        }

        errors.Required("callback.paidValue", callback.PaidValue);
        var output = Blank(payload.BillNumberOutput) ?? "billNumber";

        // One default provider: bills that name none go to it.
        if (payload.IsDefault && payload.IsActive && await refs.Context.Providers.AnyAsync(p => p.IsDefault && p.IsActive && p.Code != entity.Code, cancellationToken))
        {
            errors.Add("isDefault", "Another provider is already the default.");
        }

        entity.CreateOperation = Blank(payload.CreateOperation);
        entity.BillNumberOutput = output;
        entity.BillerCode = Blank(payload.BillerCode);
        entity.DueDays = payload.DueDays;
        entity.PayUrlTemplate = Blank(payload.PayUrlTemplate);
        entity.InstructionsAr = Blank(payload.InstructionsAr);
        entity.InstructionsEn = Blank(payload.InstructionsEn);
        entity.CallbackSecret = Blank(payload.CallbackSecret);
        entity.Callback = callback;
        entity.IsDefault = payload.IsDefault;
    }

    protected override Task<PaymentProviderPayload> ToPayloadAsync(PaymentsDbContext context, PaymentProvider entity, CancellationToken cancellationToken) =>
        Task.FromResult(new PaymentProviderPayload
        {
            CreateOperation = entity.CreateOperation,
            BillNumberOutput = entity.BillNumberOutput,
            BillerCode = entity.BillerCode,
            DueDays = entity.DueDays,
            PayUrlTemplate = entity.PayUrlTemplate,
            InstructionsAr = entity.InstructionsAr,
            InstructionsEn = entity.InstructionsEn,
            CallbackSecret = entity.CallbackSecret,
            Callback = entity.Callback,
            IsDefault = entity.IsDefault,
        });

    protected override async Task BeforeDeleteAsync(PaymentsDbContext context, PaymentProvider entity, CancellationToken cancellationToken)
    {
        if (await context.Bills.AnyAsync(b => b.ProviderId == entity.Id, cancellationToken))
        {
            throw new ChangeSetException($"Provider '{entity.Code}' has bills; deactivate it instead.");
        }
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^https?://[^\\s]{1,490}$", RegexOptions.IgnoreCase)]
    private static partial Regex WebUrl();

    [GeneratedRegex("^[A-Z][A-Z0-9_]{0,99}$")]
    private static partial Regex SecretName();
}
