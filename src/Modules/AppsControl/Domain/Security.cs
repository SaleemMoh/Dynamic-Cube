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

/// <summary>
/// A role of one company is keyed like the legacy Apps Control: instance, company number and role code, written
/// <c>instid/cmp_num/role_code</c> (e.g. <c>12/99/ADMIN</c>). A role of no company (held in any company) keeps its code.
/// </summary>
public static class RoleKey
{
    public static string Format(string? instid, string? cmpNum, string code) =>
        string.IsNullOrEmpty(cmpNum) ? code : $"{instid}/{cmpNum}/{code}";

    /// <summary>Splits a key; a key without slashes is a role of no company (instid and cmp_num empty).</summary>
    public static bool TryParse(string? key, out string instid, out string cmpNum, out string code)
    {
        instid = cmpNum = code = string.Empty;
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        var parts = key.Split('/');
        if (parts.Length == 1)
        {
            code = key;
            return true;
        }

        if (parts.Length != 3 || parts.Any(p => p.Length == 0))
        {
            return false;
        }

        (instid, cmpNum, code) = (parts[0], parts[1], parts[2]);
        return true;
    }
}

public sealed class Role : ConfigEntity
{
    /// <summary>The company number (<c>cmp_num</c>) of a role of one company, with its instance in <c>instid</c>; null for a role held in any company.</summary>
    public string? CompanyCode { get; set; }

    /// <summary>The role's key: <c>instid/cmp_num/role_code</c>, or the code alone for a role of no company.</summary>
    public string Key => RoleKey.Format(EntityCode, CompanyCode, Code);

    /// <summary>The key of the role's company, null for a role of no company.</summary>
    public string? CompanyKey => CompanyCode is null ? null : Domain.CompanyKey.Format(EntityCode, CompanyCode);

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
