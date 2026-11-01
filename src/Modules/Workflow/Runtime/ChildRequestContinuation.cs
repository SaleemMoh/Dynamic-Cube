using DynamicCube.Platform.Services;

namespace DynamicCube.Modules.Workflow.Runtime;

/// <summary>Continues a workflow waiting at a sub-request step when the request it made finishes (3f).</summary>
internal sealed class ChildRequestContinuation(WorkflowEngine engine) : IChildRequestListener
{
    public Task ChildClosedAsync(ChildRequestClosed closed, CancellationToken cancellationToken) => engine.ChildClosedAsync(closed, cancellationToken);
}
