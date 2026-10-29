using System.Text.Json.Nodes;
using DynamicCube.Modules.Visits.Changes;
using DynamicCube.Modules.Visits.Domain;
using DynamicCube.Modules.Visits.Persistence;
using DynamicCube.Platform.Files;
using DynamicCube.Platform.Persistence;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Visits.Runtime;

/// <summary>A photo sent with a field report: base64 content (a data URL is accepted) and an optional caption.</summary>
public sealed record FieldPhoto(string? FileName, string? Content, string? Caption, DateTimeOffset? TakenAt);

/// <summary>
/// What the inspector recorded, sent by the field app (possibly long after, from its outbox). <paramref name="SubmissionId"/>
/// identifies the report on the device: a report sent again is not recorded twice.
/// </summary>
public sealed record FieldReport(
    string? SubmissionId,
    string? Outcome,
    JsonObject? Data,
    string? Report,
    IReadOnlyList<FieldPhoto>? Photos,
    string? Signature,
    string? SignedBy,
    double? Latitude,
    double? Longitude,
    double? Accuracy,
    DateTimeOffset? CompletedAt);

/// <summary>
/// The inspector's side, behind the field app: their visits (with what each needs recorded, so the app works offline),
/// starting a visit and sending its report with photos, location and the site representative's signature.
/// </summary>
internal sealed class FieldVisits(VisitsDbContext context, VisitService visits, IFileStorage files, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
{
    /// <summary>Largest photo (after the app's compression) and signature accepted.</summary>
    public const int MaxPhotoBytes = 3 * 1024 * 1024;

    public const int MaxSignatureBytes = 512 * 1024;

    /// <summary>Open visits assigned to the signed-in inspector, and those they completed in the last week.</summary>
    public async Task<IReadOnlyList<VisitDetail>> MineAsync(CancellationToken cancellationToken)
    {
        var me = Me;
        var since = clock.GetUtcNow().AddDays(-7);
        var rows = await context.Visits.AsNoTracking()
            .Where(v => v.Inspector == me && (v.Status == VisitStatus.Scheduled || v.Status == VisitStatus.InProgress || (v.Status == VisitStatus.Completed && v.CompletedAt >= since)))
            .OrderBy(v => v.ScheduledAt).Take(200).ToListAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var result = new List<VisitDetail>(rows.Count);
        foreach (var visit in rows)
        {
            result.Add(await VisitRegistry.DetailAsync(context, visit, now, cancellationToken));
        }

        return result;
    }

    public async Task<VisitDetail> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await VisitRegistry.DetailAsync(context, await MineAsync(id, tracked: false, cancellationToken), clock.GetUtcNow(), cancellationToken);

    /// <summary>The inspector arrived and began; a visit already under way stays as it is.</summary>
    public async Task<VisitDetail> StartAsync(Guid id, CancellationToken cancellationToken)
    {
        await using (var work = await unitOfWork.BeginAsync(cancellationToken))
        {
            var visit = await MineAsync(id, tracked: true, cancellationToken);
            if (visit.Status == VisitStatus.Scheduled)
            {
                visit.Status = VisitStatus.InProgress;
                visit.StartedAt = clock.GetUtcNow();
                visits.Note(visit, VisitEventTypes.Started, Me, null);
                await context.SaveChangesAsync(cancellationToken);
            }
            else if (visit.Status != VisitStatus.InProgress)
            {
                throw new VisitConflictException($"Visit {visit.Number} is {visit.Status.ToString().ToLowerInvariant()}.");
            }

            await work.CommitAsync(cancellationToken);
        }

        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Records the inspector's report and completes the visit; the same report sent again changes nothing.</summary>
    public async Task<VisitDetail> ReportAsync(Guid id, FieldReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        var submission = VisitService.Clip(report.SubmissionId, 100) ?? throw new VisitValidationException("submissionId", "Required.");
        await using (var work = await unitOfWork.BeginAsync(cancellationToken))
        {
            var visit = await MineAsync(id, tracked: true, cancellationToken);
            if (visit.Status == VisitStatus.Completed && visit.SubmissionId == submission)
            {
                return await GetAsync(id, cancellationToken);
            }

            if (visit.Status is not (VisitStatus.Scheduled or VisitStatus.InProgress))
            {
                throw new VisitConflictException($"Visit {visit.Number} is {visit.Status.ToString().ToLowerInvariant()}.");
            }

            var type = await context.Types.AsNoTracking().SingleAsync(t => t.Id == visit.TypeId, cancellationToken);
            var errors = new Dictionary<string, List<string>>();
            var data = VisitForms.Check(type, report.Outcome, report.Data, errors);
            var photos = Photos(report.Photos ?? [], type, errors);
            var signature = Signature(report.Signature, type, errors);
            if (type.RequireSignature && string.IsNullOrWhiteSpace(report.SignedBy))
            {
                VisitForms.Add(errors, "signedBy", "Give the name of who signed.");
            }

            var located = report.Latitude is >= -90 and <= 90 && report.Longitude is >= -180 and <= 180;
            if (type.RequireLocation && !located)
            {
                VisitForms.Add(errors, "location", "The location is required.");
            }

            if (errors.Count > 0)
            {
                throw new VisitValidationException(errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
            }

            var now = clock.GetUtcNow();
            foreach (var (bytes, contentType, name, caption, takenAt) in photos)
            {
                using var stream = new MemoryStream(bytes);
                var stored = await files.SaveAsync(stream, name, contentType, cancellationToken);
                context.Photos.Add(new VisitPhoto
                {
                    VisitId = visit.Id,
                    FileKey = stored.Key,
                    FileName = stored.FileName,
                    ContentType = contentType,
                    Length = stored.Length,
                    Caption = VisitService.Clip(caption, 500),
                    At = takenAt ?? now,
                    CreatedBy = Me,
                });
            }

            if (signature is not null)
            {
                using var stream = new MemoryStream(signature);
                visit.SignatureKey = (await files.SaveAsync(stream, $"{visit.Number}-signature.png", "image/png", cancellationToken)).Key;
                visit.SignedBy = VisitService.Clip(report.SignedBy, 200);
            }

            if (located)
            {
                visit.CapturedLatitude = report.Latitude;
                visit.CapturedLongitude = report.Longitude;
                visit.CapturedAccuracy = report.Accuracy is >= 0 and < 100_000 ? report.Accuracy : null;
            }

            visit.StartedAt ??= now;
            visit.SubmissionId = submission;
            visit.CompletedAt = report.CompletedAt is { } at && at <= now && at >= visit.StartedAt.Value.AddDays(-1) ? at : now;
            await visits.CompleteAsync(visit, type, report.Outcome!, data, report.Report, Me, VisitEventTypes.Completed, null, cancellationToken);
            await work.CommitAsync(cancellationToken);
        }

        return await GetAsync(id, cancellationToken);
    }

    public async Task<(Stream Content, string ContentType, string FileName)> PhotoAsync(Guid id, Guid photoId, CancellationToken cancellationToken)
    {
        await MineAsync(id, tracked: false, cancellationToken);
        var photo = await context.Photos.AsNoTracking().SingleOrDefaultAsync(p => p.Id == photoId && p.VisitId == id, cancellationToken) ?? throw new VisitNotFoundException("The photo does not exist.");
        return (await files.OpenReadAsync(photo.FileKey, cancellationToken), photo.ContentType, photo.FileName);
    }

    private string Me => user.UserName ?? user.UserId;

    private async Task<Visit> MineAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        var query = tracked ? context.Visits : context.Visits.AsNoTracking();
        var me = Me;
        return await query.SingleOrDefaultAsync(v => v.Id == id && v.Inspector == me, cancellationToken)
            ?? throw new VisitNotFoundException($"Visit {id} is not assigned to you.");
    }

    private static List<(byte[] Bytes, string ContentType, string Name, string? Caption, DateTimeOffset? TakenAt)> Photos(IReadOnlyList<FieldPhoto> photos, VisitType type, Dictionary<string, List<string>> errors)
    {
        if (photos.Count < type.PhotosRequired)
        {
            VisitForms.Add(errors, "photos", $"Attach at least {type.PhotosRequired} photo(s).");
        }

        if (photos.Count > VisitTypeApplier.MaxPhotos)
        {
            VisitForms.Add(errors, "photos", $"At most {VisitTypeApplier.MaxPhotos} photos.");
            return [];
        }

        var result = new List<(byte[], string, string, string?, DateTimeOffset?)>();
        for (var i = 0; i < photos.Count; i++)
        {
            var bytes = Decode(photos[i].Content, MaxPhotoBytes);
            var contentType = bytes is null ? null : ImageType(bytes);
            if (bytes is null || contentType is null)
            {
                VisitForms.Add(errors, $"photos[{i}]", $"Must be a JPEG, PNG or WebP image of at most {MaxPhotoBytes / 1024 / 1024} MB.");
                continue;
            }

            var extension = contentType switch { "image/png" => ".png", "image/webp" => ".webp", _ => ".jpg" };
            result.Add((bytes, contentType, $"photo-{i + 1}{extension}", photos[i].Caption, photos[i].TakenAt));
        }

        return result;
    }

    private static byte[]? Signature(string? content, VisitType type, Dictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            if (type.RequireSignature)
            {
                VisitForms.Add(errors, "signature", "The signature is required.");
            }

            return null;
        }

        var bytes = Decode(content, MaxSignatureBytes);
        if (bytes is null || ImageType(bytes) != "image/png")
        {
            VisitForms.Add(errors, "signature", "Must be a PNG image of at most 512 KB.");
            return null;
        }

        return bytes;
    }

    /// <summary>Base64 or a data URL, decoded; null when not valid or too large.</summary>
    private static byte[]? Decode(string? content, int max)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var text = content.Trim();
        var comma = text.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ? text.IndexOf(',', StringComparison.Ordinal) : -1;
        if (comma >= 0)
        {
            text = text[(comma + 1)..];
        }

        if (text.Length > (max / 3 * 4) + 4)
        {
            return null;
        }

        var buffer = new byte[(text.Length / 4 * 3) + 3];
        return Convert.TryFromBase64String(text, buffer, out var written) && written <= max ? buffer[..written] : null;
    }

    /// <summary>The image type from the file's first bytes (what it claims to be is not trusted).</summary>
    private static string? ImageType(byte[] bytes) => bytes switch
    {
        [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => "image/png",
        [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..] => "image/webp",
        _ => null,
    };
}
