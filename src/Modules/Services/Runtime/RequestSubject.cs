using System.Text.Json.Nodes;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Persistence;
using DynamicCube.Platform.Parties;
using DynamicCube.Platform.Security;
using DynamicCube.Platform.Workflow;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Services.Runtime;

/// <summary>Service requests as workflow subjects: rules read their answers, level and party; actions set their status and timeline.</summary>
internal sealed class RequestSubject(ServicesDbContext context, IPartyFacts parties, ICurrentUser user, TimeProvider clock) : IWorkflowSubject
{
    public const string Type = "SERVICE_REQUEST";

    public string SubjectType => Type;

    public async Task<JsonObject> GetRuleContextAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var request = await LoadAsync(subjectId, cancellationToken);
        var party = await parties.GetAsync(request.BeneficiaryPartyId, refreshProfile: false, cancellationToken);
        return new JsonObject
        {
            ["data"] = JsonNode.Parse(request.Data),
            ["level"] = request.LevelCode,
            ["service"] = request.ServiceCode,
            ["number"] = request.Number,
            ["submitterPartyId"] = request.SubmitterPartyId.ToString(),
            // The beneficiary: decisions and documents read its profile, identifiers and credentials.
            ["party"] = party?.ToJson() ?? new JsonObject { ["id"] = request.BeneficiaryPartyId.ToString() },
        };
    }

    public async Task UpdateAsync(Guid subjectId, SubjectUpdate update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        var request = await LoadAsync(subjectId, cancellationToken);
        var from = request.StatusCategory;
        var to = update.Status switch
        {
            SubjectStatus.PendingCustomer => StatusCategory.PendingCustomer,
            SubjectStatus.Approved => StatusCategory.Approved,
            SubjectStatus.Rejected => StatusCategory.Rejected,
            SubjectStatus.Closed => StatusCategory.Closed,
            _ => StatusCategory.InProgress,
        };
        if (from == to && request.DisplayStatus == update.DisplayStatus && update.Comment is null)
        {
            return;
        }

        var now = clock.GetUtcNow();
        request.StatusCategory = to;
        request.DisplayStatus = update.DisplayStatus;
        if (to is StatusCategory.Approved or StatusCategory.Rejected or StatusCategory.Closed)
        {
            request.ClosedAt = now;
        }

        context.RequestEvents.Add(new RequestEvent { RequestId = request.Id, At = now, UserId = user.UserId, Type = update.EventType, FromStatus = from, ToStatus = to, Comment = update.Comment });
        if (from != to && RequestNotifications.For(to) is { } code)
        {
            var service = await context.Services.AsNoTracking().Where(s => s.Id == request.ServiceId).Select(s => new { s.NameAr, s.NameEn }).SingleAsync(cancellationToken);
            RequestNotifications.Raise(context, clock, code, request, service.NameAr, service.NameEn, update.Comment);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<ServiceRequest> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await context.Requests.SingleOrDefaultAsync(r => r.Id == id, cancellationToken) ?? throw new RequestNotFoundException($"Request {id} does not exist.");
}
