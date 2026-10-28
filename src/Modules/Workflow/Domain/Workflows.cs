namespace DynamicCube.Modules.Workflow.Domain;

/// <summary>The workflow of one service: requests of the service run on its latest published version.</summary>
public sealed class WorkflowConfig : ConfigEntity
{
    /// <summary>The service it belongs to (one workflow per service).</summary>
    public string? ServiceCode { get; set; }

    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }
}

/// <summary>
/// One numbered version of a workflow (entity code <c>WORKFLOW/N</c>). Published versions never change; instances stay
/// on the version they started on.
/// </summary>
public sealed class WorkflowVersion : ConfigEntity
{
    public Guid WorkflowId { get; set; }

    public WorkflowConfig? Workflow { get; set; }

    public int Number { get; set; }

    /// <summary>The <see cref="Definitions.WorkflowDefinition"/> (JSON).</summary>
    public string Definition { get; set; } = "{}";

    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Retired versions take no new instances; running ones finish on them.</summary>
    public bool IsRetired { get; set; }
}
