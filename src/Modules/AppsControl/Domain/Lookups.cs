using DynamicCube.Platform.Persistence;

namespace DynamicCube.Modules.AppsControl.Domain;

public enum AttributeDataType
{
    Text,
    Number,
    Date,
    Bool,
    DomainRef,
    Json,
}

public enum AttributeGroup
{
    Attribute,
    Parameter,
}

/// <summary>Lookup type, e.g. <c>SECTOR</c>, <c>REGION</c>, <c>REQUEST_STATUS</c>.</summary>
public sealed class LookupClass : ConfigEntity
{
    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    /// <summary>Class whose values are the parents of this class's values (e.g. CITY → REGION).</summary>
    public Guid? ParentClassId { get; set; }

    public LookupClass? ParentClass { get; set; }

    public bool IsHierarchical { get; set; }

    public bool AllowMultiAssign { get; set; }

    public List<AttributeDefinition> Attributes { get; } = [];
}

/// <summary>Typed attribute definition of a class; values live in <see cref="Domain.Attributes"/>.</summary>
public sealed class AttributeDefinition : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ClassId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public AttributeDataType DataType { get; set; }

    /// <summary>Code of the referenced class when <see cref="DataType"/> is <see cref="AttributeDataType.DomainRef"/>.</summary>
    public string? RefClassCode { get; set; }

    public AttributeGroup AttributeGroup { get; set; }

    public bool IsRequired { get; set; }

    /// <summary>JSON validation hints: <c>min</c>, <c>max</c>, <c>maxLength</c>, <c>pattern</c>.</summary>
    public string? Validation { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsReportable { get; set; } = true;
}

/// <summary>Lookup value of a class.</summary>
public sealed class DomainValue : ConfigEntity
{
    public Guid ClassId { get; set; }

    public LookupClass? Class { get; set; }

    public Guid? ParentDomainId { get; set; }

    public DomainValue? ParentDomain { get; set; }

    public string? DetailsAr { get; set; }

    public string? DetailsEn { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsDefault { get; set; }

    /// <summary>JSON object validated against the class attribute definitions.</summary>
    public string Attributes { get; set; } = "{}";

    public DateOnly? EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public List<DomainExternalCode> ExternalCodes { get; } = [];
}

public enum CodeDirection
{
    In,
    Out,
    Both,
}

/// <summary>Translates a domain value to and from an integration provider's code.</summary>
public sealed class DomainExternalCode : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid DomainId { get; set; }

    public string ProviderCode { get; set; } = string.Empty;

    public string ExternalCode { get; set; } = string.Empty;

    public CodeDirection Direction { get; set; } = CodeDirection.Both;
}
