namespace DynamicCube.Platform.Files;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Root directory (local disk or mounted NAS share).</summary>
    public string RootPath { get; set; } = "storage";
}
