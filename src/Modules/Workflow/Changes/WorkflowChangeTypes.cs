namespace DynamicCube.Modules.Workflow.Changes;

/// <summary>Entity types, change-set area and guarding resources of workflow configuration.</summary>
public static class WorkflowChangeTypes
{
    public const string Workflow = "wf.workflow";
    public const string Version = "wf.workflow-version";

    public const string Area = "wf.workflows";
}

public static class WorkflowResources
{
    /// <summary>The Action Center: inboxes and task work (who may act on a task is decided by the workflow).</summary>
    public const string ActionCenter = "action-center";

    /// <summary>All open tasks, with reassignment (supervisors).</summary>
    public const string Supervision = "action-center.supervision";

    public const string Config = "workflows.config";
}
