using DynamicCube.Platform.Persistence;

namespace DynamicCube.Modules.AppsControl.Domain;

public enum DataObjectType
{
    Table,
    View,
}

/// <summary>Registered table or view that LOVs and data scopes may query. Only registered objects and columns are queryable.</summary>
public sealed class DataObject : ConfigEntity
{
    public string DbSchema { get; set; } = string.Empty;

    public string DbObject { get; set; } = string.Empty;

    public DataObjectType ObjectType { get; set; } = DataObjectType.View;

    public List<DataObjectColumn> Columns { get; set; } = [];

    public List<DataScopeBinding> ScopeBindings { get; } = [];
}

/// <summary>Column of a data object (stored as JSON on the object).</summary>
public sealed class DataObjectColumn
{
    public string Name { get; set; } = string.Empty;

    public string DataType { get; set; } = "TEXT";

    public string? LabelAr { get; set; }

    public string? LabelEn { get; set; }

    public bool Filterable { get; set; }

    public bool Searchable { get; set; }
}

/// <summary>Applies an LOV's grants to a column of a data object, so the same rule filters lists, inboxes and reports.</summary>
public sealed class DataScopeBinding : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid DataObjectId { get; set; }

    public string ColumnName { get; set; } = string.Empty;

    public Guid LovId { get; set; }

    public Lov? Lov { get; set; }

    /// <summary>Resource codes the binding applies to; empty means everywhere.</summary>
    public List<string> AppliesTo { get; set; } = [];
}

public enum LovSourceType
{
    Class,
    DataObject,
    Integration,
    Sql,
}

#pragma warning disable CA1720 // "Single" is the business term for single selection.
public enum SelectionMode
{
    Single,
    Multi,
}
#pragma warning restore CA1720

public enum LovSearchMode
{
    Full,
    Typeahead,
}

/// <summary>List of values: a registered data source that feeds dropdowns and, through grants, filters data per role.</summary>
public sealed class Lov : ConfigEntity
{
    public LovSourceType SourceType { get; set; }

    public Guid? ClassId { get; set; }

    public LookupClass? Class { get; set; }

    public Guid? DataObjectId { get; set; }

    public DataObject? DataObject { get; set; }

    public string? IntegrationOperationCode { get; set; }

    public string? SqlText { get; set; }

    public string? ValueColumn { get; set; }

    public string? DisplayArColumn { get; set; }

    public string? DisplayEnColumn { get; set; }

    /// <summary>Column holding the parent value for cascading lists (data object / SQL sources).</summary>
    public string? ParentColumn { get; set; }

    /// <summary>JSONLogic evaluated per value (class sources: over <c>attributes</c>, <c>code</c>, <c>parent</c>).</summary>
    public string? StaticFilter { get; set; }

    public SelectionMode DefaultSelectionMode { get; set; }

    public LovSearchMode SearchMode { get; set; }

    public int MaxRows { get; set; } = 500;

    /// <summary>When true, users without any grant see nothing; otherwise they see all values.</summary>
    public bool RequiresGrant { get; set; }

    /// <summary>Available to the external (internet-facing) app.</summary>
    public bool IsPublic { get; set; }

    public List<LovBinding> Bindings { get; } = [];
}

/// <summary>Binds an LOV to a field of a screen or form.</summary>
public sealed class LovBinding : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid LovId { get; set; }

    public Guid ResourceId { get; set; }

    public Resource? Resource { get; set; }

    public string FieldCode { get; set; } = string.Empty;

    public SelectionMode SelectionMode { get; set; }
}

public enum GrantType
{
    All,
    Values,
    Rule,
    None,
}

/// <summary>Values of an LOV a role may see: globally, or for one resource (override).</summary>
public sealed class LovGrant : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid RoleId { get; set; }

    public Guid LovId { get; set; }

    public Lov? Lov { get; set; }

    public Guid? ResourceId { get; set; }

    public Resource? Resource { get; set; }

    public GrantType GrantType { get; set; }

    public List<string> GrantedValues { get; set; } = [];

    /// <summary>
    /// JSONLogic over the user context returning the granted value(s): a value, an array of values,
    /// <c>true</c> (all) or <c>false</c>/null (none). Example: <c>{"var":"user.orgUnitAttributes.REGION"}</c>.
    /// </summary>
    public string? Rule { get; set; }

    public string? DefaultValue { get; set; }

    /// <summary>The company whose role override this grant belongs to; empty: the role's own grant.</summary>
    public string? CompanyCode { get; set; }
}
