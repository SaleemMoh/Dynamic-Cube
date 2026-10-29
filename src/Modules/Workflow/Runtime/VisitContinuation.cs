using DynamicCube.Platform.Visits;

namespace DynamicCube.Modules.Workflow.Runtime;

/// <summary>Continues a workflow waiting at a visit step when its visit is completed or cancelled.</summary>
internal sealed class VisitContinuation(WorkflowEngine engine) : IVisitListener
{
    public Task OnVisitClosedAsync(VisitClosed visit, CancellationToken cancellationToken) => engine.VisitClosedAsync(visit, cancellationToken);
}
