using DynamicCube.Platform.Files;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace DynamicCube.Hosting;

/// <summary>
/// Tells when uploads and issued documents cannot be written: the storage folder is missing or belongs to another user
/// (a container volume owned by root, a NAS share without rights). Reported by the readiness check, so it shows at start
/// rather than as a failed upload.
/// </summary>
internal sealed class FileStorageHealthCheck(IOptions<FileStorageOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(options.Value.RootPath);
        var probe = Path.Combine(root, $".write-check-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(probe, string.Empty, cancellationToken);
            File.Delete(probe);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return HealthCheckResult.Unhealthy($"Files cannot be written to {root}: {exception.Message} Give the folder to the user the process runs as.");
        }
    }
}
