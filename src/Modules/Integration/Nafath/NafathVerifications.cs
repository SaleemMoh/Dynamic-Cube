using System.Text.Json;
using DynamicCube.Modules.Integration.Domain;
using DynamicCube.Modules.Integration.Persistence;
using DynamicCube.Platform.Nafath;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DynamicCube.Modules.Integration.Nafath;

/// <summary>
/// Verifications in the person's Nafath app (ADR-0043): the request is sent, the number shown; while it waits Nafath's
/// status is asked, and the signed answer posted to the callback makes it verified (the status alone is never taken as
/// proof of who answered).
/// </summary>
internal sealed class NafathVerifications(
    IntegrationDbContext context,
    NafathClient client,
    IOptions<NafathOptions> options,
    IDataProtectionProvider protection,
    TimeProvider clock) : INafathVerifications
{
    public const string Purpose = "DynamicCube.Nafath.v1";

    private readonly IDataProtector _protector = protection.CreateProtector(Purpose);

    public bool IsConfigured => options.Value.IsConfigured;

    public async Task<NafathChallenge> StartAsync(string nationalId, string? purpose, string subjectType, Guid? subjectId, string? userIp, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nationalId);
        var now = clock.GetUtcNow();
        var request = new NafathRequest
        {
            Kind = NafathRequestKind.Verification,
            Status = NafathRequestStatus.Waiting,
            Service = purpose ?? options.Value.VerificationService,
            SubjectType = subjectType,
            SubjectId = subjectId,
            ProtectedNationalId = _protector.Protect(nationalId.Trim()),
            CreatedAt = now,
            ExpiresAt = now.AddSeconds(options.Value.RequestSeconds),
        };
        var (transId, random) = await client.CreateMfaRequestAsync(nationalId.Trim(), request.Service, request.Id.ToString(), userIp, cancellationToken);
        request.TransId = transId;
        request.Random = random;
        context.NafathRequests.Add(request);
        await context.SaveChangesAsync(cancellationToken);
        return new NafathChallenge(request.Id, random, request.ExpiresAt);
    }

    public async Task<NafathCheck?> CheckAsync(Guid id, string? userIp, CancellationToken cancellationToken)
    {
        var request = await context.NafathRequests.SingleOrDefaultAsync(r => r.Id == id && r.Kind == NafathRequestKind.Verification, cancellationToken);
        if (request is null)
        {
            return null;
        }

        if (request.Status == NafathRequestStatus.Waiting)
        {
            if (clock.GetUtcNow() > request.ExpiresAt.AddSeconds(30))
            {
                Finish(request, NafathRequestStatus.Expired);
            }
            else if (request.TransId is not null && request.Random is not null)
            {
                var status = await client.MfaStatusAsync(_protector.Unprotect(request.ProtectedNationalId!), request.TransId, request.Random, userIp, cancellationToken);
                if (status is "REJECTED" or "EXPIRED")
                {
                    Finish(request, status == "REJECTED" ? NafathRequestStatus.Rejected : NafathRequestStatus.Expired);
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        return ToCheck(request);
    }

    /// <summary>Nafath's signed answer to a verification, posted to the callback: it must name the person the request was sent to.</summary>
    public async Task<bool> AnswerAsync(Guid id, string? transId, NafathPerson person, CancellationToken cancellationToken)
    {
        var request = await context.NafathRequests.SingleOrDefaultAsync(r => r.Id == id && r.Kind == NafathRequestKind.Verification, cancellationToken);
        if (request is null || (transId is not null && request.TransId != transId) || request.Status is NafathRequestStatus.Rejected)
        {
            return false;
        }

        if (!string.Equals(_protector.Unprotect(request.ProtectedNationalId!), person.Identifier, StringComparison.Ordinal))
        {
            return false;
        }

        request.ProtectedPerson = _protector.Protect(JsonSerializer.Serialize(person));
        Finish(request, NafathRequestStatus.Verified);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private NafathCheck ToCheck(NafathRequest request) => new(
        request.Id,
        request.SubjectType,
        request.SubjectId,
        request.Status switch
        {
            NafathRequestStatus.Verified => NafathStatus.Verified,
            NafathRequestStatus.Rejected => NafathStatus.Rejected,
            NafathRequestStatus.Expired => NafathStatus.Expired,
            _ => NafathStatus.Waiting,
        },
        request.Random ?? string.Empty,
        request.ExpiresAt,
        request.ProtectedPerson is null ? null : JsonSerializer.Deserialize<NafathPerson>(_protector.Unprotect(request.ProtectedPerson)),
        request.CompletedAt);

    private void Finish(NafathRequest request, NafathRequestStatus status)
    {
        request.Status = status;
        request.CompletedAt = clock.GetUtcNow();
    }
}
