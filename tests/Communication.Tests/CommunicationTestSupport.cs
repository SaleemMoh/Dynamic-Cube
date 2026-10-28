using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.Communication.Changes;
using DynamicCube.Modules.Communication.Domain;
using DynamicCube.Modules.Communication.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Communication;
using DynamicCube.Platform.Parties;
using DynamicCube.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DynamicCube.Modules.Communication.Tests;

public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 1, 9, 0, 0, TimeSpan.FromHours(3));

    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
}

/// <summary>Parties with an email and mobile in their profile, except those listed in <see cref="WithoutContacts"/>.</summary>
internal sealed class TestParties : IPartyFacts
{
    public HashSet<Guid> WithoutContacts { get; } = [];

    public Task<PartyFacts?> GetAsync(Guid partyId, bool refreshProfile, CancellationToken cancellationToken) =>
        Task.FromResult<PartyFacts?>(new PartyFacts(
            partyId, PartyKind.Establishment, "مؤسسة " + partyId.ToString()[..4], "Establishment " + partyId.ToString()[..4],
            new Dictionary<string, string>(),
            WithoutContacts.Contains(partyId)
                ? new Dictionary<string, JsonNode?>()
                : new Dictionary<string, JsonNode?> { ["EMAIL"] = $"p{partyId.ToString("N")[..6]}@example.com", ["MOBILE"] = "9665" + partyId.ToString("N")[..8] },
            [], []));
}

/// <summary>Records what it is asked to send; fails while <see cref="Failing"/> is set.</summary>
internal sealed class TestChannel(string code, bool configured = true) : IMessageChannel
{
    public string Code => code;

    public bool IsConfigured => configured;

    public bool Failing { get; set; }

    public ConcurrentQueue<OutboundMessage> Sent { get; } = new();

    public Task SendAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        if (Failing)
        {
            throw new MessageDeliveryException("Server down.");
        }

        Sent.Enqueue(message);
        return Task.CompletedTask;
    }
}

internal sealed class CommunicationHost : IAsyncDisposable
{
    /// <summary>An event type of this host only, so rules of tests running alongside do not match it.</summary>
    public string TestEvent { get; } = Unique("TEST_APPROVED");

    public CommunicationHost(DatabaseFixture fixture)
    {
        Host = new TestHost(fixture.RequireConnectionString(), s =>
        {
            s.AddSingleton(Clock);
            s.AddSingleton<TimeProvider>(Clock);
            s.AddSingleton<IPartyFacts>(Parties);
            s.RemoveAll<IMessageChannel>();
            s.AddSingleton<IMessageChannel>(Email);
            s.AddSingleton<IMessageChannel>(Sms);
            s.AddCommunicationEvents(new CommunicationEventType(TestEvent, "اعتماد", "Approved", "tests", new JsonObject
            {
                ["request"] = new JsonObject { ["number"] = "T-1", ["grade"] = 3, ["reviewer"] = "user1" },
            }));
        });
    }

    public TestHost Host { get; }

    public TestClock Clock { get; } = new();

    public TestParties Parties { get; } = new();

    public TestChannel Email { get; } = new(Channels.Email);

    /// <summary>Not configured: SMS messages are skipped.</summary>
    public TestChannel Sms { get; } = new(Channels.Sms, configured: false);

    public static string Unique(string prefix) => TestHost.Unique(prefix);

    public async Task<string> TemplateAsync(bool email = true, bool sms = true, bool inApp = true, string? link = "/requests/{{ subject.id }}")
    {
        var code = Unique("TPL");
        await Host.PublishAsync(CommunicationChangeTypes.Area, new Change(CommunicationChangeTypes.Template, code, ChangeOperation.Create, new
        {
            nameAr = "قالب",
            nameEn = "Template",
            emailSubjectAr = email ? "اعتماد الطلب {{ request.number }}" : null,
            emailSubjectEn = email ? "Request {{ request.number }} approved" : null,
            emailBodyAr = email ? "<p>عزيزنا {{ recipient.name }}، تم اعتماد طلبكم بالدرجة {{ request.grade }} & شكراً</p>" : null,
            emailBodyEn = email ? "<p>Dear {{ recipient.name }}, grade {{ request.grade }}</p>" : null,
            smsAr = sms ? "تم اعتماد الطلب {{ request.number }}" : null,
            smsEn = sms ? "Request {{ request.number }} approved" : null,
            inAppTitleAr = inApp ? "اعتماد الطلب" : null,
            inAppTitleEn = inApp ? "Request approved" : null,
            inAppBodyAr = inApp ? "الطلب {{ request.number }} بالدرجة {{ request.grade }}" : null,
            inAppBodyEn = inApp ? "Request {{ request.number }}, grade {{ request.grade }}" : null,
            link = inApp ? link : null,
        }));
        return code;
    }

    public async Task<string> RuleAsync(string template, string[] channels, object[] recipients, object? condition = null, string language = "AR")
    {
        var code = Unique("RULE");
        await Host.PublishAsync(CommunicationChangeTypes.Area, new Change(CommunicationChangeTypes.Rule, code, ChangeOperation.Create, new
        {
            nameAr = "قاعدة",
            nameEn = "Rule",
            eventCode = TestEvent,
            templateCode = template,
            channels,
            recipients,
            condition,
            language,
        }));
        return code;
    }

    public async Task<string> RoleWithUsersAsync(params (string User, string? Email)[] users)
    {
        var role = Unique("NOTIFY");
        await Host.PublishAsync(AppsControlAreas.Security, new Change(AppsControlChangeTypes.Role, role, ChangeOperation.Create, new { nameAr = "دور", nameEn = "Role" }));
        await Host.PublishAsync(AppsControlAreas.Security, [.. users.Select(u => new Change(AppsControlChangeTypes.User, u.User, ChangeOperation.Create, new
        {
            nameAr = "موظف " + u.User,
            nameEn = "Employee " + u.User,
            email = u.Email,
            roles = new[] { new { roleCode = role } },
        }))]);
        return role;
    }

    /// <summary>Raises the test event and lets the dispatcher handle it (as the worker would); returns the event.</summary>
    public async Task<CommunicationEvent> RaiseAsync(Guid party, int grade = 3, string? reviewer = null)
    {
        var subject = Guid.CreateVersion7();
        var e = new CommunicationEvent(Guid.CreateVersion7(), TestEvent, "TEST", subject, "T-" + subject.ToString("N")[^8..], party, new JsonObject
        {
            ["request"] = new JsonObject { ["number"] = "T-" + subject.ToString("N")[^8..], ["grade"] = grade, ["reviewer"] = reviewer },
        });
        await DispatchAsync(e);
        return e;
    }

    public Task DispatchAsync(CommunicationEvent e) => Host.AsAsync("worker", sp => sp.GetRequiredService<EventDispatcher>().HandleAsync(e, CancellationToken.None));

    public Task<MessagePage> MessagesAsync(Guid subject) =>
        Host.AsAsync("admin", sp => sp.GetRequiredService<CommunicationService>().SearchAsync(null, null, null, null, subject, 0, 100, CancellationToken.None));

    /// <summary>Delivers a pending email or SMS (as the worker would from the outbox).</summary>
    public Task SendAsync(Guid message) => Host.AsAsync("worker", sp => sp.GetRequiredService<MessageSender>().HandleAsync(new SendMessage(message), CancellationToken.None));

    public async ValueTask DisposeAsync() => await Host.DisposeAsync();
}
