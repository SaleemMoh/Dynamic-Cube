namespace DynamicCube.Platform.Security;

/// <summary>An employee as other modules see them: who they are and which roles, units and positions they hold.</summary>
public sealed record StaffMember(
    string UserName,
    string NameAr,
    string NameEn,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> OrgUnits,
    IReadOnlyList<string> Positions);

/// <summary>Employees holding a role, working in an org unit and/or a position (each criterion optional; all must hold).</summary>
public sealed record StaffQuery(string? Role = null, string? OrgUnit = null, string? Position = null);

/// <summary>Active employees and what they hold today (implemented by Apps Control).</summary>
public interface IStaffDirectory
{
    /// <summary>The signed-in employee, or null outside the internal system.</summary>
    Task<StaffMember?> CurrentAsync(CancellationToken cancellationToken);

    Task<StaffMember?> FindAsync(string userName, CancellationToken cancellationToken);

    Task<IReadOnlyList<StaffMember>> SearchAsync(StaffQuery query, CancellationToken cancellationToken);
}
