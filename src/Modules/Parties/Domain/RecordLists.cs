using DynamicCube.Platform.Parties;
using DynamicCube.Platform.Persistence;

namespace DynamicCube.Modules.Parties.Domain;

/// <summary>The type of a record list column (the same names as the services' answer types, so rows map by code).</summary>
public enum RecordColumnType
{
    Text,
    Number,
    Date,
    Checkbox,
    Select,
}

public sealed class RecordOption
{
    public string Value { get; set; } = string.Empty;

    public string LabelAr { get; set; } = string.Empty;

    public string LabelEn { get; set; } = string.Empty;
}

/// <summary>A column of a record list.</summary>
public sealed class RecordColumn
{
    public string Code { get; set; } = string.Empty;

    public string LabelAr { get; set; } = string.Empty;

    public string LabelEn { get; set; } = string.Empty;

    public RecordColumnType Type { get; set; }

    public bool Required { get; set; }

    /// <summary>Shown in the table of records (the first four when none is marked).</summary>
    public bool InTable { get; set; }

    public List<RecordOption> Options { get; set; } = [];
}

/// <summary>Where an imported column's value is in an item of the operation's result.</summary>
public sealed class RecordFieldMap
{
    public string Column { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;
}

/// <summary>
/// How a list is filled from another system: an operation called with the party's identifiers, the list of items in its
/// result, the column that identifies an item (so a later import updates it) and where each column is in an item.
/// </summary>
public sealed class RecordImport
{
    public string OperationCode { get; set; } = string.Empty;

    public List<IdentifierBinding> Inputs { get; set; } = [];

    /// <summary>The items in the operation's result, e.g. <c>projects</c> or <c>data.contracts</c>.</summary>
    public string ItemsPath { get; set; } = string.Empty;

    /// <summary>The column whose value identifies an item (e.g. the contract number).</summary>
    public string KeyColumn { get; set; } = string.Empty;

    public List<RecordFieldMap> Fields { get; set; } = [];
}

/// <summary>
/// A list of records a party keeps across requests (an establishment's projects, owners, branches…): its columns, whether
/// the applicant adds records by hand, and how it is imported from another system. Rows questions of services can let the
/// applicant pick from it.
/// </summary>
public sealed class RecordList : ConfigEntity
{
    public PartyKind PartyKind { get; set; } = PartyKind.Establishment;

    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    public string? Icon { get; set; }

    public List<RecordColumn> Columns { get; set; } = [];

    /// <summary>The applicant may add, change and remove records typed by hand (imported ones stay as imported).</summary>
    public bool AllowManual { get; set; } = true;

    public RecordImport? Import { get; set; }

    public int DisplayOrder { get; set; }
}

/// <summary>A record of a party in a record list: typed by hand or imported (then identified by its key).</summary>
public sealed class PartyRecord : IAuditable, IHasAuditColumns
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid PartyId { get; set; }

    public string ListCode { get; set; } = string.Empty;

    /// <summary>Imported records: the value of the list's key column.</summary>
    public string? Key { get; set; }

    /// <summary>The record's values by column code (JSON object).</summary>
    public string Data { get; set; } = "{}";

    public RecordSource Source { get; set; }

    public DateTimeOffset? ImportedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}
