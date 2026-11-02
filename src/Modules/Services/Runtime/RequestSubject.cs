using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Persistence;
using DynamicCube.Platform.Documents;
using DynamicCube.Platform.Parties;
using DynamicCube.Platform.Security;
using DynamicCube.Platform.Workflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Services.Runtime;

/// <summary>
/// Service requests as workflow subjects: rules read their answers, level, party and (follow-up requests) the document they
/// were made for; actions set their status and timeline.
/// </summary>
internal sealed class RequestSubject(ServicesDbContext context, IPartyFacts parties, ICurrentUser user, IServiceProvider services, TimeProvider clock) : IWorkflowSubject
{
    public const string Type = "SERVICE_REQUEST";

    public string SubjectType => Type;

    public async Task<JsonObject> GetRuleContextAsync(Guid subjectId, CancellationToken cancellationToken) =>
        await RuleContextAsync(await LoadAsync(subjectId, cancellationToken), null, null, cancellationToken);

    /// <summary>
    /// The facts the workflow's rules read about a request; with <paramref name="data"/> (and the level), the answers as the
    /// applicant has them now instead of the saved ones (the live result, ADR-0040).
    /// </summary>
    internal async Task<JsonObject> RuleContextAsync(ServiceRequest request, JsonObject? data, string? levelCode, CancellationToken cancellationToken)
    {
        var party = await parties.GetAsync(request.BeneficiaryPartyId, refreshProfile: false, cancellationToken);
        var document = request.SourceDocumentId is { } documentId && services.GetService<IDocumentDirectory>() is { } documents
            ? await documents.GetAsync(documentId, cancellationToken)
            : null;
        return new JsonObject
        {
            ["data"] = data?.DeepClone() ?? JsonNode.Parse(request.Data),
            ["level"] = data is null ? request.LevelCode : levelCode,
            ["service"] = request.ServiceCode,
            ["number"] = request.Number,
            ["submitterPartyId"] = request.SubmitterPartyId.ToString(),
            // The beneficiary: decisions and documents read its profile, identifiers and credentials.
            ["party"] = party?.ToJson() ?? new JsonObject { ["id"] = request.BeneficiaryPartyId.ToString() },
            // What a payment step bills by default: the fees computed at submission.
            ["fees"] = JsonSerializer.SerializeToNode(request.Fees, JsonSerializerOptions.Web),
            ["feeTotal"] = request.FeeTotal,
            // A follow-up request: a document step with target SOURCE amends this document.
            ["document"] = document?.ToJson(),
        };
    }

    /// <summary>Keeps the items decided at a task; a later decision on the same item replaces the earlier one.</summary>
    public async Task ItemsDecidedAsync(Guid subjectId, IReadOnlyList<SubjectItem> items, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        var request = await LoadAsync(subjectId, cancellationToken);
        var kept = (JsonSerializer.Deserialize<List<SubjectItem>>(request.ItemResults, JsonSerializerOptions.Web) ?? [])
            .Where(old => !items.Any(i => i.Step == old.Step && i.Key == old.Key));
        request.ItemResults = JsonSerializer.Serialize(kept.Concat(items).ToList(), JsonSerializerOptions.Web);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Guid subjectId, SubjectUpdate update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        var request = await LoadAsync(subjectId, cancellationToken);
        var from = request.StatusCategory;
        var to = update.Status switch
        {
            SubjectStatus.PendingCustomer => StatusCategory.PendingCustomer,
            SubjectStatus.PendingPayment => StatusCategory.PendingPayment,
            SubjectStatus.PendingExternal => StatusCategory.PendingExternal,
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
        if (from != to)
        {
            // A request a step made has finished: the step waiting for it moves on (3f).
            await RequestLinks.TellParentAsync(services, request, clock, cancellationToken);
        }
    }

    private async Task<ServiceRequest> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await context.Requests.SingleOrDefaultAsync(r => r.Id == id, cancellationToken) ?? throw new RequestNotFoundException($"Request {id} does not exist.");
}
