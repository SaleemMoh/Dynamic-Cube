namespace DynamicCube.Modules.AppsControl.Domain;

/// <summary>
/// Approval policy of a configuration area (code = area, e.g. <c>apps.lookups</c>). Areas without approval publish
/// changes on save; every change still keeps its before/after record.
/// </summary>
public sealed class ChangeArea : ConfigEntity
{
    public bool RequiresApproval { get; set; } = true;
}
