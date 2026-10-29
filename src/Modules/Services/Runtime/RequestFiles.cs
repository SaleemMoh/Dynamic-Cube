using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Services.Runtime;

/// <summary>A file uploaded with a request, as the applicant and staff see it.</summary>
public sealed record RequestFileInfo(Guid Id, string QuestionCode, string FileName, string ContentType, long Length, DateTimeOffset UploadedAt);

/// <summary>
/// Rules of file questions: accepted extensions (the question's, or the usual documents and images), the largest file,
/// how many files, and the content type stored for each extension (the browser's is not trusted).
/// </summary>
public static partial class RequestFiles
{
    /// <summary>No file question may accept more than this many megabytes per file.</summary>
    public const int LargestMb = 25;

    public const int DefaultMb = 10;

    public const int DefaultMaxFiles = 5;

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.Ordinal)
    {
        ["pdf"] = "application/pdf",
        ["jpg"] = "image/jpeg",
        ["jpeg"] = "image/jpeg",
        ["png"] = "image/png",
        ["gif"] = "image/gif",
        ["webp"] = "image/webp",
        ["tif"] = "image/tiff",
        ["tiff"] = "image/tiff",
        ["doc"] = "application/msword",
        ["docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ["xls"] = "application/vnd.ms-excel",
        ["xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ["ppt"] = "application/vnd.ms-powerpoint",
        ["pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ["txt"] = "text/plain",
        ["csv"] = "text/csv",
        ["zip"] = "application/zip",
        ["dwg"] = "application/acad",
    };

    /// <summary>Accepted when a question names no extensions.</summary>
    public static readonly IReadOnlyList<string> DefaultExtensions = ["pdf", "jpg", "jpeg", "png", "doc", "docx", "xls", "xlsx"];

    [GeneratedRegex("^[a-z0-9]{1,10}$")]
    public static partial Regex ExtensionPattern();

    public static IReadOnlyList<string> Extensions(QuestionSnapshot question) =>
        question.Validation.Extensions.Count > 0 ? question.Validation.Extensions : DefaultExtensions;

    public static long MaxBytes(QuestionSnapshot question) => (long)(question.Validation.MaxSizeMb ?? DefaultMb) * 1024 * 1024;

    public static int MaxFiles(QuestionSnapshot question) => question.Multiple ? question.Validation.MaxFiles ?? DefaultMaxFiles : 1;

    /// <summary>The extension of a file name (lower case, without the dot), or empty.</summary>
    public static string ExtensionOf(string fileName) => Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();

    public static string ContentTypeOf(string extension) => ContentTypes.GetValueOrDefault(extension, "application/octet-stream");

    public static Task<List<RequestFileInfo>> ListAsync(ServicesDbContext context, Guid requestId, CancellationToken cancellationToken) =>
        context.RequestFiles.AsNoTracking()
            .Where(f => f.RequestId == requestId)
            .OrderBy(f => f.UploadedAt)
            .Select(f => new RequestFileInfo(f.Id, f.QuestionCode, f.FileName, f.ContentType, f.Length, f.UploadedAt))
            .ToListAsync(cancellationToken);

    /// <summary>The ids answered to a file question (a list, or a single id).</summary>
    public static List<string> Ids(JsonNode? answer) => answer switch
    {
        JsonArray list => [.. list.Select(n => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!)],
        JsonValue v when v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) => [s],
        _ => [],
    };

    /// <summary>
    /// Keeps, in the answers of file questions, only files uploaded with this request for that question (a client cannot
    /// point at someone else's files).
    /// </summary>
    public static void KeepOwnFiles(ResolvedVersion version, JsonObject data, IEnumerable<RequestFile> files)
    {
        var byQuestion = files.GroupBy(f => f.QuestionCode).ToDictionary(g => g.Key, g => g.Select(f => f.Id.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase));
        foreach (var question in version.Questions.Values.Where(q => q.Type == QuestionType.File))
        {
            if (data[question.Code] is null)
            {
                continue;
            }

            var own = byQuestion.GetValueOrDefault(question.Code) ?? [];
            var ids = Ids(data[question.Code]).Where(own.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (ids.Count == 0)
            {
                data.Remove(question.Code);
            }
            else
            {
                data[question.Code] = new JsonArray([.. ids.Select(id => (JsonNode)JsonValue.Create(id)!)]);
            }
        }
    }
}
