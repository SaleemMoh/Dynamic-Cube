namespace DynamicCube.Platform.Files;

/// <summary>Binary storage for attachments and generated documents. Implementations: file system (default), S3-compatible (later).</summary>
public interface IFileStorage
{
    Task<StoredFile> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

/// <summary>Result of storing a file. <see cref="Key"/> is opaque and is what callers persist.</summary>
public sealed record StoredFile(string Key, string FileName, string ContentType, long Length, string Sha256);
