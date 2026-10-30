namespace DynamicCube.Modules.Documents.Domain;

/// <summary>A label for a data or line field, used by the registry screens and the public verification page.</summary>
public sealed class DocumentField
{
    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    /// <summary>Shown on the public verification page.</summary>
    public bool IsPublic { get; set; }
}

/// <summary>
/// A kind of document (certificate, license, permit…): how it is numbered (<c>PREFIX-YYYY-000001</c>), how long it is
/// valid, whether a holder has one at a time (issuing again amends it) and the template it is printed with.
/// </summary>
public sealed class DocumentType : ConfigEntity
{
    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    public string NumberPrefix { get; set; } = string.Empty;

    /// <summary>Months from issue to expiry; null for documents that do not expire.</summary>
    public int? ValidityMonths { get; set; }

    /// <summary>One document per holder: issuing again amends the active one (same number, new version).</summary>
    public bool OnePerHolder { get; set; }

    /// <summary>An amendment starts the validity again (otherwise the expiry date is kept).</summary>
    public bool AmendResetsValidity { get; set; }

    public Guid? TemplateId { get; set; }

    public DocumentTemplate? Template { get; set; }

    /// <summary>Days before expiry when the holder is reminded (e.g. 90 and 30).</summary>
    public int[] AlertDaysBefore { get; set; } = [];

    /// <summary>Labels of the data fields (JSON list of <see cref="DocumentField"/>).</summary>
    public string Fields { get; set; } = "[]";

    /// <summary>Labels of the line-item fields (JSON list of <see cref="DocumentField"/>).</summary>
    public string LineFields { get; set; } = "[]";
}

/// <summary>An HTML + Liquid layout documents are printed with (A4 or A5, portrait or landscape).</summary>
public sealed class DocumentTemplate : ConfigEntity
{
    public string Html { get; set; } = string.Empty;

    /// <summary>
    /// The layout as the visual designer keeps it (JSON: page and placed elements), when the template was designed there;
    /// <see cref="Html"/> is generated from it and is what is printed. Empty for templates written as HTML.
    /// </summary>
    public string? Design { get; set; }

    public string PageSize { get; set; } = "A4";

    public bool Landscape { get; set; }
}
