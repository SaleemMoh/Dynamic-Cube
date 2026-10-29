namespace DynamicCube.Modules.Payments.Changes;

/// <summary>Entity types, change-set area and guarding resources of payments.</summary>
public static class PaymentChangeTypes
{
    public const string Provider = "pay.provider";

    public const string Area = "pay.payments";
}

public static class PaymentResources
{
    /// <summary>Bills; EDIT allows recording a payment made outside the provider and cancelling a bill.</summary>
    public const string Bills = "payments.bills";

    public const string Config = "payments.config";
}
