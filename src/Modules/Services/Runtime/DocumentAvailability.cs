using System.Globalization;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Platform.Documents;

namespace DynamicCube.Modules.Services.Runtime;

/// <summary>Whether a follow-up service is open on a document today, and from when to when.</summary>
internal sealed record AvailabilityResult(bool IsAvailable, DateOnly? OpensOn, DateOnly? ClosesOn, EligibilityFailure? Failure);

/// <summary>The availability of a version (accepted statuses, a window around the issue or expiry date) applied to a document.</summary>
internal static class DocumentAvailability
{
    private static readonly Dictionary<string, (string Ar, string En)> StatusNames = new(StringComparer.Ordinal)
    {
        ["ACTIVE"] = ("سارية", "active"),
        ["EXPIRED"] = ("منتهية", "expired"),
        ["SUSPENDED"] = ("موقوفة", "suspended"),
        ["CANCELLED"] = ("ملغاة", "cancelled"),
    };

    public static AvailabilityResult Check(AvailabilityDefinition? availability, DocumentFacts document, DateOnly today)
    {
        var statuses = availability?.Statuses is { Count: > 0 } list ? list : ["ACTIVE"];
        var opens = Day(availability?.From, document);
        var closes = Day(availability?.To, document);
        if (!statuses.Contains(document.Status))
        {
            var (ar, en) = StatusNames.GetValueOrDefault(document.Status, (document.Status, document.Status.ToLowerInvariant()));
            return new(false, opens, closes, new("DOCUMENT_STATUS", $"لا تتاح هذه الخدمة لأن الوثيقة {ar}.", $"This service is not available because the document is {en}."));
        }

        // A window counted from the expiry date needs one.
        if ((availability?.From?.Of == DocumentDate.Expires || availability?.To?.Of == DocumentDate.Expires) && document.ExpiresAt is null)
        {
            return new(false, null, null, new("NO_EXPIRY", "الوثيقة بلا تاريخ انتهاء، فلا تتاح لها هذه الخدمة.", "The document has no expiry date, so this service does not apply to it."));
        }

        if (opens is { } from && today < from)
        {
            return new(false, opens, closes, new("NOT_YET_OPEN", $"تتاح هذه الخدمة ابتداءً من {Text(from)}.", $"This service opens on {Text(from)}."));
        }

        return closes is { } to && today > to
            ? new(false, opens, closes, new("WINDOW_CLOSED", $"انتهت فترة إتاحة هذه الخدمة في {Text(to)}.", $"This service closed on {Text(to)}."))
            : new(true, opens, closes, null);
    }

    private static DateOnly? Day(DateBound? bound, DocumentFacts document)
    {
        var date = bound?.Of switch
        {
            DocumentDate.Issued => document.IssuedAt,
            DocumentDate.Expires => document.ExpiresAt,
            _ => null,
        };
        return date is { } at ? DateOnly.FromDateTime(at.UtcDateTime).AddDays(bound!.Days) : null;
    }

    private static string Text(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
