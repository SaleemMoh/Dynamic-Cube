using System.Text.Json.Nodes;
using DynamicCube.Platform.Outbox;
using DynamicCube.Platform.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Platform.Communication;

/// <summary>
/// Something happened that people may be told about (a request was returned, a certificate is about to expire). Modules
/// raise it in the same transaction as the change; the Communication module matches it against its notification rules.
/// </summary>
/// <param name="Id">Identifies the occurrence: messages are created once per event even when it is delivered twice.</param>
/// <param name="Code">The event type, e.g. <c>REQUEST_RETURNED</c> (see <see cref="CommunicationEventType"/>).</param>
/// <param name="PartyId">The applicant or holder it concerns: rules read its facts as <c>party.*</c> and may notify it.</param>
/// <param name="Facts">What templates and conditions read, e.g. <c>request.*</c>, <c>document.*</c>, <c>task.*</c>.</param>
public sealed record CommunicationEvent(Guid Id, string Code, string SubjectType, Guid SubjectId, string? SubjectNumber, Guid? PartyId, JsonObject Facts) : IOutboxMessage
{
    public static string MessageType => "comm.event";
}

/// <summary>
/// A kind of event a module raises, offered to notification rules. <paramref name="Sample"/> is a typical facts object:
/// rule conditions and templates are tried against it when they are published, and the template editor lists its fields.
/// </summary>
public sealed record CommunicationEventType(string Code, string NameAr, string NameEn, string Module, JsonObject Sample);

public static class CommunicationEvents
{
    /// <summary>Raises an event; it is saved by the next <c>SaveChanges</c> of the context, with the change it reports.</summary>
    public static void Raise(this ModuleDbContext context, TimeProvider clock, string code, string subjectType, Guid subjectId, string? subjectNumber, Guid? partyId, JsonObject facts) =>
        context.Enqueue(new CommunicationEvent(Guid.CreateVersion7(), code, subjectType, subjectId, subjectNumber, partyId, facts), clock);

    /// <summary>Offers event types to notification rules.</summary>
    public static IServiceCollection AddCommunicationEvents(this IServiceCollection services, params CommunicationEventType[] types)
    {
        foreach (var type in types)
        {
            services.AddSingleton(type);
        }

        return services;
    }
}
