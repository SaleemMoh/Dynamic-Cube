using DynamicCube.Platform.Persistence;

namespace DynamicCube.Modules.AppsControl.Domain;

public enum ResourceType
{
    Screen,
    Api,
    Report,
    Service,
    WorkflowAction,
    Field,
}

/// <summary>Standard privilege actions.</summary>
public static class PrivilegeActions
{
    public const string View = "VIEW";
    public const string Create = "CREATE";
    public const string Edit = "EDIT";
    public const string Delete = "DELETE";
    public const string Approve = "APPROVE";
    public const string Export = "EXPORT";
    public const string Print = "PRINT";

    public static IReadOnlyList<string> All { get; } = [View, Create, Edit, Delete, Approve, Export, Print];
}

/// <summary>Anything securable: screens, APIs, reports, services, workflow actions, fields.</summary>
public sealed class Resource : ConfigEntity
{
    public Guid? AppId { get; set; }

    public App? App { get; set; }

    public ResourceType ResourceType { get; set; }

    public Guid? ParentId { get; set; }

    public Resource? Parent { get; set; }

    public List<string> AvailableActions { get; set; } = [PrivilegeActions.View];
}

public sealed class Role : ConfigEntity
{
    /// <summary>Null means the role spans all apps.</summary>
    public Guid? AppId { get; set; }

    public App? App { get; set; }

    public string? ReferenceCode { get; set; }

    public List<Privilege> Privileges { get; } = [];

    public List<LovGrant> Grants { get; } = [];

    /// <summary>Menu items the role shows; each gives the role VIEW on the screen the item opens.</summary>
    public List<RoleMenu> Menus { get; } = [];

    /// <summary>
    /// Companies where the role differs (ADR-0036): there its privileges, menus and data permissions are the rows of that
    /// company (<see cref="Privilege.CompanyCode"/>…) instead of the role's own (rows without a company).
    /// </summary>
    public List<string> OverrideCompanies { get; set; } = [];
}

/// <summary>A menu item assigned to a role.</summary>
public sealed class RoleMenu : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid RoleId { get; set; }

    public Guid MenuId { get; set; }

    public Menu? Menu { get; set; }

    /// <summary>The company whose override this row belongs to; empty: the role's own menus.</summary>
    public string? CompanyCode { get; set; }
}

/// <summary>Actions a role may perform on a resource, with optional per-field rules.</summary>
public sealed class Privilege : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid RoleId { get; set; }

    public Guid ResourceId { get; set; }

    public Resource? Resource { get; set; }

    public List<string> Actions { get; set; } = [];

    /// <summary>JSON object: field code → <c>HIDDEN</c> | <c>READONLY</c>.</summary>
    public string? FieldRules { get; set; }

    /// <summary>The company whose override this row belongs to; empty: the role's own privileges.</summary>
    public string? CompanyCode { get; set; }
}
