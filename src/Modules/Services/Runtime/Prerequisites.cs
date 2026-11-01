using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Persistence;
using DynamicCube.Platform.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Services.Runtime;

/// <summary>
/// A version's prerequisites (3f) for an applicant: each request prerequisite needs one of the applicant's requests of the
/// service in one of its statuses (default APPROVED), made or closed recently enough; each document prerequisite needs one
/// of the applicant's documents of the type in one of its statuses (default ACTIVE), valid long enough. Every unmet one
/// is a failure with its message and the service that meets it.
/// </summary>
internal sealed class Prerequisites(ServicesDbContext context, IServiceProvider services, TimeProvider clock)
{
    public async Task<List<EligibilityFailure>> CheckAsync(VersionDefinition definition, Guid partyId, CancellationToken cancellationToken)
    {
        var failures = new List<EligibilityFailure>();
        var now = clock.GetUtcNow();
        foreach (var prerequisite in definition.Prerequisites)
        {
            var met = prerequisite.Kind == PrerequisiteKind.Request
                ? await RequestMetAsync(prerequisite, partyId, now, cancellationToken)
                : await DocumentMetAsync(prerequisite, partyId, now, cancellationToken);
            if (!met)
            {
                var start = prerequisite.StartService ?? (prerequisite.Kind == PrerequisiteKind.Request ? prerequisite.Service : null);
                failures.Add(new EligibilityFailure("PREREQUISITE_" + prerequisite.Code, prerequisite.MessageAr, prerequisite.MessageEn, start));
            }
        }

        return failures;
    }

    /// <summary>The statuses as request statuses (IN_PROGRESS → InProgress); APPROVED when none are given.</summary>
    internal static List<StatusCategory> RequestStatuses(IReadOnlyCollection<string> statuses) =>
        statuses.Count == 0
            ? [StatusCategory.Approved]
            : [.. statuses.Select(s => Enum.TryParse<StatusCategory>(s.Replace("_", string.Empty, StringComparison.Ordinal), ignoreCase: true, out var status) ? (StatusCategory?)status : null).OfType<StatusCategory>()];

    private async Task<bool> RequestMetAsync(PrerequisiteDefinition prerequisite, Guid partyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var statuses = RequestStatuses(prerequisite.Statuses);
        var query = context.Requests.AsNoTracking()
            .Where(r => r.BeneficiaryPartyId == partyId && r.ServiceCode == prerequisite.Service && statuses.Contains(r.StatusCategory));
        if (prerequisite.WithinDays is { } days)
        {
            var since = now.AddDays(-days);
            query = query.Where(r => (r.ClosedAt ?? r.SubmittedAt ?? r.CreatedAt) >= since);
        }

        return await query.AnyAsync(cancellationToken);
    }

    private async Task<bool> DocumentMetAsync(PrerequisiteDefinition prerequisite, Guid partyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (services.GetService<IDocumentDirectory>() is not { } documents || prerequisite.DocumentType is null)
        {
            return false;
        }

        var statuses = prerequisite.Statuses.Count == 0 ? ["ACTIVE"] : prerequisite.Statuses;
        var until = now.AddDays(prerequisite.ValidForDays ?? 0);
        return (await documents.OfHolderAsync(partyId, prerequisite.DocumentType, cancellationToken))
            .Any(d => statuses.Contains(d.Status, StringComparer.Ordinal) && (prerequisite.ValidForDays is null || d.ExpiresAt is null || d.ExpiresAt >= until));
    }
}
