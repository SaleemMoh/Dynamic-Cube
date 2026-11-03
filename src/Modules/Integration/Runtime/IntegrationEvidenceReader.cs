using DynamicCube.Modules.Integration.Persistence;
using DynamicCube.Platform.Integration;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Integration.Runtime;

/// <summary>The calls the gateway logged for a subject (e.g. a request), newest first: its integration evidence.</summary>
internal sealed class IntegrationEvidenceReader(IntegrationDbContext context) : IIntegrationEvidence
{
    /// <summary>Most calls listed for one subject.</summary>
    private const int MaxCalls = 200;

    public async Task<IReadOnlyList<IntegrationEvidence>> OfSubjectAsync(string subjectType, Guid subjectId, CancellationToken cancellationToken)
    {
        var calls = await context.CallLogs.AsNoTracking()
            .Where(c => c.SubjectType == subjectType && c.SubjectId == subjectId)
            .OrderByDescending(c => c.StartedAt)
            .Take(MaxCalls)
            .ToListAsync(cancellationToken);
        var codes = calls.Select(c => c.OperationCode).Distinct().ToList();
        var names = await context.Operations.AsNoTracking().Where(o => codes.Contains(o.Code))
            .Select(o => new { o.Code, o.NameAr, o.NameEn })
            .ToDictionaryAsync(o => o.Code, cancellationToken);
        return [.. calls.Select(c => new IntegrationEvidence(
            c.Id,
            c.OperationCode,
            names.TryGetValue(c.OperationCode, out var n) ? n.NameAr : c.OperationCode,
            names.TryGetValue(c.OperationCode, out var m) ? m.NameEn : c.OperationCode,
            c.StartedAt,
            c.DurationMs,
            c.Outcome,
            c.HttpStatus,
            c.Code,
            c.Message,
            c.FromCache,
            c.Method,
            c.Url,
            c.RequestBody,
            c.ResponseBody,
            c.UserId))];
    }
}
