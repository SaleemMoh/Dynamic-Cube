using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.Services.Changes;
using DynamicCube.Modules.Workflow.Changes;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Parties;
using DynamicCube.Platform.Workflow;
using DynamicCube.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Workflow.Tests;

/// <summary>A clock tests move forward.</summary>
public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 1, 9, 0, 0, TimeSpan.FromHours(3));

    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
}

/// <summary>A workflow subject held in memory: rule facts in, status updates recorded.</summary>
public sealed class TestSubject : IWorkflowSubject
{
    public const string Type = "TEST";

    public string SubjectType => Type;

    public Dictionary<Guid, JsonObject> Data { get; } = [];

    public Dictionary<Guid, List<SubjectUpdate>> Updates { get; } = [];

    public SubjectUpdate Last(Guid id) => Updates[id][^1];

    /// <summary>The party a subject is for (documents are issued to it); the subject id itself by default.</summary>
    public Dictionary<Guid, Guid> Parties { get; } = [];

    public Task<JsonObject> GetRuleContextAsync(Guid subjectId, CancellationToken cancellationToken) =>
        Task.FromResult(new JsonObject
        {
            ["data"] = Data.TryGetValue(subjectId, out var data) ? data.DeepClone() : new JsonObject(),
            ["level"] = null,
            ["party"] = new JsonObject { ["id"] = (Parties.TryGetValue(subjectId, out var party) ? party : subjectId).ToString() },
        });

    public Task UpdateAsync(Guid subjectId, SubjectUpdate update, CancellationToken cancellationToken)
    {
        lock (Updates)
        {
            if (!Updates.TryGetValue(subjectId, out var list))
            {
                Updates[subjectId] = list = [];
            }

            list.Add(update);
        }

        return Task.CompletedTask;
    }
}

/// <summary>Every party exists, as an establishment named after its id.</summary>
public sealed class TestParties : IPartyFacts
{
    public Task<PartyFacts?> GetAsync(Guid partyId, bool refreshProfile, CancellationToken cancellationToken) =>
        Task.FromResult<PartyFacts?>(new PartyFacts(
            partyId, PartyKind.Establishment, "منشأة " + partyId.ToString()[..4], "Establishment " + partyId.ToString()[..4], new Dictionary<string, string>(), new Dictionary<string, JsonNode?>(), [], []));
}

/// <summary>A test host with the test subject and clock, plus helpers to define staff and workflows.</summary>
internal sealed class WorkflowHost : IAsyncDisposable
{
    public WorkflowHost(DatabaseFixture fixture, Action<IServiceCollection>? configure = null)
    {
        Host = new TestHost(fixture.RequireConnectionString(), s =>
        {
            s.AddSingleton(Subject);
            s.AddSingleton<IWorkflowSubject>(Subject);
            s.AddSingleton(Clock);
            s.AddSingleton<TimeProvider>(Clock);
            s.AddSingleton<IPartyFacts>(new TestParties());
            configure?.Invoke(s);
        });
    }

    public TestHost Host { get; }

    public TestSubject Subject { get; } = new();

    public TestClock Clock { get; } = new();

    public static string Unique(string prefix) => TestHost.Unique(prefix);

    public static string User(string prefix) => TestHost.Unique(prefix).ToLowerInvariant();

    /// <summary>Creates roles (one per prefix); returns their codes in order.</summary>
    public async Task<string[]> RolesAsync(params string[] prefixes)
    {
        var roles = prefixes.Select(Unique).ToArray();
        await Host.PublishAsync(AppsControlAreas.Security, [.. roles.Select(r => new Change(AppsControlChangeTypes.Role, r, ChangeOperation.Create, new { nameAr = "دور " + r, nameEn = "Role " + r }))]);
        return roles;
    }

    /// <summary>Creates employees with their roles.</summary>
    public Task UsersAsync(params (string User, string[] Roles)[] users) =>
        Host.PublishAsync(AppsControlAreas.Security, [.. users.Select(u => new Change(AppsControlChangeTypes.User, u.User, ChangeOperation.Create, new
        {
            nameAr = "موظف " + u.User,
            nameEn = "Employee " + u.User,
            roles = u.Roles.Select(r => new { roleCode = r, companyCode = "*", branches = new[] { "*" } }).ToArray(),
        }))]);

    /// <summary>Publishes a service; it comes with its workflow (same code), whose version 1 completes requests without review.</summary>
    public async Task<string> ServiceAsync()
    {
        var code = Unique("SVC");
        await Host.PublishAsync(ServiceChangeTypes.Area, new Change(ServiceChangeTypes.Service, code, ChangeOperation.Create, new
        {
            nameAr = "خدمة " + code,
            nameEn = "Service " + code,
            archetype = "ATTESTATION",
            partyKinds = new[] { "ESTABLISHMENT" },
        }));
        return code;
    }

    /// <summary>Publishes a service and version 2 of its workflow with these nodes; returns the workflow code.</summary>
    public async Task<string> WorkflowAsync(object[] nodes, int? slaWorkingDays = null)
    {
        var code = await ServiceAsync();
        await Host.PublishAsync(
            WorkflowChangeTypes.Area,
            new Change(WorkflowChangeTypes.Version, code + "/2", ChangeOperation.Create, new { nameAr = "الإصدار 2", nameEn = "Version 2", definition = new { slaWorkingDays, nodes } }));
        return code;
    }

    public async Task<Guid> StartAsync(string workflow, JsonObject? data = null, string? level = null, int? sla = null)
    {
        var id = Guid.CreateVersion7();
        Subject.Data[id] = data ?? [];
        await Host.AsAsync("submitter", sp => sp.GetRequiredService<IWorkflowRuntime>().StartAsync(
            new WorkflowStart(workflow, TestSubject.Type, id, "T-" + id.ToString("N")[..8], "خدمة", "Service", "منشأة", "Establishment", "SVC", level, sla),
            CancellationToken.None));
        return id;
    }

    public Task<T> AsAsync<T>(string user, Func<Runtime.ActionCenter, Task<T>> action) =>
        Host.AsAsync(user, sp => action(sp.GetRequiredService<Runtime.ActionCenter>()));

    public Task AsAsync(string user, Func<Runtime.ActionCenter, Task> action) =>
        Host.AsAsync(user, sp => action(sp.GetRequiredService<Runtime.ActionCenter>()));

    /// <summary>The open tasks of a subject.</summary>
    public Task<List<Domain.WorkTask>> OpenTasksAsync(Guid subject) =>
        Host.AsAsync("admin", sp => sp.GetRequiredService<Persistence.WorkflowDbContext>().Tasks
            .Where(t => t.SubjectId == subject && t.Status == Domain.WorkTaskStatus.Open).OrderBy(t => t.NodeCode).ToListAsync());

    public Task<Domain.WorkflowInstance> InstanceAsync(Guid subject) =>
        Host.AsAsync("admin", sp => sp.GetRequiredService<Persistence.WorkflowDbContext>().Instances.SingleAsync(i => i.SubjectId == subject));

    public async ValueTask DisposeAsync() => await Host.DisposeAsync();

    // Node builders (JSON as authored in a change set).
    public static object Start(string next) => new { code = "START", type = "START", nameAr = "بداية", nameEn = "Start", next };

    public static object End(string code, string outcome) => new { code, type = "END", nameAr = "نهاية", nameEn = "End", outcome };

    public static object Split(string code, params string[] branches) => new { code, type = "PARALLEL_SPLIT", nameAr = "تفرع", nameEn = "Split", branches };

    public static object Join(string code, string next, string mode = "ALL", int? count = null) => new { code, type = "JOIN", nameAr = "دمج", nameEn = "Join", next, mode, count };

    public static object Gateway(string code, object[] routes, string @default) => new { code, type = "GATEWAY", nameAr = "توجيه", nameEn = "Gateway", routes, @default };

    public static object Human(string code, object assignment, object[] actions, string[]? exclude = null, object? sla = null) =>
        new { code, type = "HUMAN_TASK", nameAr = "مهمة " + code, nameEn = "Task " + code, assignment, actions, excludeActorsOf = exclude ?? [], sla };

    public static object Role(string role, string mode = "QUEUE", string? distributor = null) => new { target = "ROLE", role, mode, distributorRole = distributor };

    public static object Act(string code, string to, string kind = "COMPLETE") => new { code, kind, nameAr = "إجراء " + code, nameEn = "Action " + code, to };

    public static object Return(string code = "RETURN") => new { code, kind = "RETURN", nameAr = "إعادة", nameEn = "Return" };
}
