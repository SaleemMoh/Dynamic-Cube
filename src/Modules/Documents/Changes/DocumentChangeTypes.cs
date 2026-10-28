namespace DynamicCube.Modules.Documents.Changes;

/// <summary>Entity types, change-set area and guarding resources of document configuration.</summary>
public static class DocumentChangeTypes
{
    public const string DocumentType = "doc.document-type";
    public const string Template = "doc.template";

    public const string Area = "doc.documents";
}

public static class DocumentResources
{
    /// <summary>The issued-document registry; EDIT allows suspending, reinstating and cancelling documents.</summary>
    public const string Registry = "documents.registry";

    public const string Config = "documents.config";
}
