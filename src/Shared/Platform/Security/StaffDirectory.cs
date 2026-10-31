namespace DynamicCube.Platform.Security;

/// <summary>An employee as other modules see them: who they are and which roles, units and positions they hold.</summary>
/// <param name="OrgUnits">Units the employee works in, the primary one first.</param>
/// <param name="Placements">Each unit with the position held there (null when unknown).</param>
public sealed record StaffMember(
    string UserName,
    string NameAr,
    string NameEn,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> OrgUnits,
    IReadOnlyList<string> Positions,
    string? Email = null,
    string? Mobile = null,
    IReadOnlyList<StaffPlacement>? Placements = null,
    IReadOnlyList<RoleHolding>? Holdings = null)
{
    /// <summary>
    /// Whether the employee holds a role in a company and branch (ADR-0036): held there, in every company or every branch.
    /// Without holdings (older callers), holding the role anywhere counts.
    /// </summary>
    public bool Holds(string role, string? company, string? branch) =>
        Holdings is null
            ? Roles.Contains(role)
            : Holdings.Any(h => h.Role == role
                && (company is null || h.Company == RoleHolding.All || h.Company == company)
                && (branch is null || h.Branches.Contains(RoleHolding.All) || h.Branches.Contains(branch)));
}

/// <summary>A role an employee holds in a company (or <c>*</c>, every company) and branches of it (or <c>["*"]</c>).</summary>
public sealed record RoleHolding(string Role, string Company, IReadOnlyList<string> Branches)
{
    public const string All = "*";
}

/// <summary>Where an employee works: a unit and the position held in it (if any).</summary>
public sealed record StaffPlacement(string OrgUnit, string? Position);

/// <summary>
/// Employees holding a role, working in an org unit and/or a position (each criterion optional; all must hold). A unit
/// and a position together mean the position held in that unit. <paramref name="OrgUnits"/> widens <paramref name="OrgUnit"/>
/// to any of several units. <paramref name="Company"/> and <paramref name="Branch"/> narrow <paramref name="Role"/> to holders
/// in that company and branch.
/// </summary>
public sealed record StaffQuery(
    string? Role = null,
    string? OrgUnit = null,
    string? Position = null,
    IReadOnlyList<string>? OrgUnits = null,
    string? Company = null,
    string? Branch = null);

/// <summary>An organization unit and the unit above it.</summary>
public sealed record OrgUnitNode(string Code, string? ParentCode, string NameAr, string NameEn);

/// <summary>Active employees and what they hold today (implemented by Apps Control).</summary>
public interface IStaffDirectory
{
    /// <summary>The signed-in employee, or null outside the internal system.</summary>
    Task<StaffMember?> CurrentAsync(CancellationToken cancellationToken);

    Task<StaffMember?> FindAsync(string userName, CancellationToken cancellationToken);

    Task<IReadOnlyList<StaffMember>> SearchAsync(StaffQuery query, CancellationToken cancellationToken);

    /// <summary>The active organization units, as a tree through <see cref="OrgUnitNode.ParentCode"/>.</summary>
    Task<IReadOnlyList<OrgUnitNode>> OrgUnitsAsync(CancellationToken cancellationToken);
}
