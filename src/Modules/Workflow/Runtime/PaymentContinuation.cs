using DynamicCube.Platform.Payments;

namespace DynamicCube.Modules.Workflow.Runtime;

/// <summary>Continues a workflow waiting at a payment step when its bill is paid, expires or is cancelled.</summary>
internal sealed class PaymentContinuation(WorkflowEngine engine) : IPaymentListener
{
    public Task OnBillClosedAsync(BillClosed bill, CancellationToken cancellationToken) => engine.PaymentClosedAsync(bill, cancellationToken);
}
