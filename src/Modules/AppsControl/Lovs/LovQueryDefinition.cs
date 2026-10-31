using System.Text.Json.Nodes;

namespace DynamicCube.Modules.AppsControl.Lovs;

/// <summary>
/// A list of values built visually (source type <c>QUERY</c>): rows of a registered data object or a class, joined to
/// others, filtered, sorted and mapped to the value, its labels and its parent. Only registered columns can be used and
/// every value is a bound parameter, so the definition compiles to safe SQL (see <see cref="LovQueryCompiler"/>).
/// </summary>
public sealed class LovQueryDefinition
{
    public LovQuerySource From { get; set; } = new();

    public List<LovQueryJoin> Joins { get; set; } = [];

    public LovQueryMap Map { get; set; } = new();

    public LovQueryGroup? Where { get; set; }

    public List<LovQuerySort> Sort { get; set; } = [];

    /// <summary>Each value once, even when the joins repeat it.</summary>
    public bool Distinct { get; set; }
}

/// <summary>A table to read: a registered data object (<c>DATA_OBJECT</c>) or the values of a class (<c>CLASS</c>).</summary>
public class LovQuerySource
{
    public string Kind { get; set; } = LovQueryKinds.Class;

    public string Code { get; set; } = string.Empty;

    /// <summary>Short name the columns of this source are referred by: <c>alias.column</c>.</summary>
    public string Alias { get; set; } = "a";
}

public sealed class LovQueryJoin : LovQuerySource
{
    /// <summary><c>INNER</c>: only rows with a match; <c>LEFT</c>: every row, with the joined columns empty when none.</summary>
    public string Type { get; set; } = "LEFT";

    /// <summary>Column pairs that must be equal (all of them).</summary>
    public List<LovQueryOn> On { get; set; } = [];
}

public sealed class LovQueryOn
{
    public string Left { get; set; } = string.Empty;

    public string Right { get; set; } = string.Empty;
}

/// <summary>Which columns give the value, its labels and (for cascading lists) its parent.</summary>
public sealed class LovQueryMap
{
    public string? Value { get; set; }

    public string? LabelAr { get; set; }

    public string? LabelEn { get; set; }

    public string? Parent { get; set; }
}

/// <summary>Conditions that must all (<c>ALL</c>) or any (<c>ANY</c>) hold, with nested groups.</summary>
public sealed class LovQueryGroup
{
    public string Match { get; set; } = "ALL";

    public List<LovQueryCondition> Conditions { get; set; } = [];

    public List<LovQueryGroup> Groups { get; set; } = [];
}

/// <summary>
/// A condition on a column: compared with a fixed <see cref="Value"/>, or with a fact of the signed-in user
/// (<see cref="User"/>: their company, branches, organization units, user name, app or roles).
/// </summary>
public sealed class LovQueryCondition
{
    public string Column { get; set; } = string.Empty;

    public string Op { get; set; } = "EQ";

    public JsonNode? Value { get; set; }

    public string? User { get; set; }
}

public sealed class LovQuerySort
{
    public string Column { get; set; } = string.Empty;

    public string Direction { get; set; } = "ASC";
}

public static class LovQueryKinds
{
    public const string Class = "CLASS";
    public const string DataObject = "DATA_OBJECT";

    /// <summary>
    /// A table or view not registered yet (code <c>schema.name</c>): the builder offers it and previews it; saving the list
    /// registers it as a data object in the same change, so a saved list never reads an unregistered table.
    /// </summary>
    public const string Table = "TABLE";
}

/// <summary>How a column compares: as text, number, date, true/false, or not at all (JSON).</summary>
public enum LovColumnCategory
{
    Text,
    Number,
    Date,
    Bool,
    Json,
}

/// <summary>A column a query may use, with how it compares.</summary>
public sealed record LovSourceColumn(string Name, string DataType, LovColumnCategory Category, string? LabelAr, string? LabelEn);

/// <summary>A source the builder offers: where it lives in the database and its usable columns.</summary>
/// <param name="ObjectType">For a table not registered yet: <c>TABLE</c> or <c>VIEW</c>, as it is registered when the list is saved.</param>
public sealed record LovQuerySourceInfo(string Kind, string Code, string NameAr, string NameEn, string Schema, string DbObject, IReadOnlyList<LovSourceColumn> Columns, string? ObjectType = null);

/// <summary>Facts of the signed-in user a condition may compare with; null where the user has every value (all branches).</summary>
public sealed record LovUserFacts(
    string? Company,
    IReadOnlyList<string>? Branches,
    string? OrgUnit,
    IReadOnlyList<string> OrgUnits,
    IReadOnlyList<string> SubtreeOrgUnits,
    string UserName,
    string App,
    IReadOnlyList<string> Roles)
{
    public static readonly string[] Names = ["COMPANY", "BRANCHES", "ORG_UNIT", "ORG_UNITS", "SUBTREE_ORG_UNITS", "USERNAME", "APP", "ROLES"];

    /// <summary>The user's values of a fact, or null when they have all of them.</summary>
    public IReadOnlyList<string>? Get(string name) => name switch
    {
        "COMPANY" => Company is null ? [] : [Company],
        "BRANCHES" => Branches,
        "ORG_UNIT" => OrgUnit is null ? [] : [OrgUnit],
        "ORG_UNITS" => OrgUnits,
        "SUBTREE_ORG_UNITS" => SubtreeOrgUnits,
        "USERNAME" => [UserName],
        "APP" => [App],
        "ROLES" => Roles,
        _ => [],
    };
}
