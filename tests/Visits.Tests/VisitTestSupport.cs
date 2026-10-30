using System.Collections.Concurrent;
using System.Net;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.Integration.Changes;
using DynamicCube.Modules.Integration.Domain;
using DynamicCube.Modules.Visits.Changes;
using DynamicCube.Modules.Visits.Domain;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Security;
using DynamicCube.Platform.Visits;
using DynamicCube.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Visits.Tests;

public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>The gateway credentials of the test host plus the callback key of the external visit types.</summary>
internal sealed class TestSecrets : ISecretStore
{
    public const string CallbackSecret = "VISIT_CALLBACK";
    public const string CallbackKey = "visit-signing-key";

    public string? Find(string name, string key) => (name, key) switch
    {
        (TestHost.SecretName, "clientId") => "consumer-key",
        (TestHost.SecretName, "clientSecret") => "consumer-secret",
        (CallbackSecret, "key") => CallbackKey,
        _ => null,
    };
}

/// <summary>Remembers the visits closed (completed or cancelled), as the workflow would be told.</summary>
internal sealed class RecordingListener : IVisitListener
{
    public ConcurrentQueue<VisitClosed> Closed { get; } = new();

    public Task OnVisitClosedAsync(VisitClosed visit, CancellationToken cancellationToken)
    {
        Closed.Enqueue(visit);
        return Task.CompletedTask;
    }
}

internal sealed class VisitHost : IAsyncDisposable
{
    /// <summary>The smallest valid PNG (1×1), as base64.</summary>
    public const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    public VisitHost(DatabaseFixture fixture)
    {
        Host = new TestHost(fixture.RequireConnectionString(), s =>
        {
            s.AddSingleton<ISecretStore, TestSecrets>();
            s.AddSingleton(Clock);
            s.AddSingleton<TimeProvider>(Clock);
            s.AddSingleton<IVisitListener>(Listener);
        });
    }

    public TestHost Host { get; }

    public TestClock Clock { get; } = new();

    public RecordingListener Listener { get; } = new();

    /// <summary>A role and an employee holding it, returned as (role, user name).</summary>
    public async Task<(string Role, string Inspector)> InspectorAsync()
    {
        var role = TestHost.Unique("INSPECTOR");
        var inspector = TestHost.Unique("inspector").ToLowerInvariant();
        await Host.PublishAsync(
            AppsControlAreas.Security,
            new Change(AppsControlChangeTypes.Role, role, ChangeOperation.Create, new { nameAr = "مفتش", nameEn = "Inspector" }),
            new Change(AppsControlChangeTypes.User, inspector, ChangeOperation.Create, new { nameAr = "المفتش", nameEn = "Inspector", roles = new[] { new { roleCode = role, companyCode = "*", branches = new[] { "*" } } } }));
        return (role, inspector);
    }

    /// <summary>A field visit type: pass/fail, a required note, a score, a checkbox, one photo, location and signature.</summary>
    public async Task<string> FieldTypeAsync(string? role, VisitMode mode = VisitMode.Field, bool strict = true)
    {
        var code = TestHost.Unique("SITE");
        await Host.PublishAsync(VisitChangeTypes.Area, new Change(VisitChangeTypes.Type, code, ChangeOperation.Create, new VisitTypePayload
        {
            NameAr = "معاينة الموقع",
            NameEn = "Site inspection",
            Mode = mode,
            Outcomes = [Outcome("PASS", OutcomeKind.Pass), Outcome("FAIL", OutcomeKind.Fail)],
            Fields =
            [
                new() { Code = "notes", LabelAr = "الملاحظات", LabelEn = "Notes", Type = VisitFieldType.Textarea, Required = true },
                new() { Code = "score", LabelAr = "الدرجة", LabelEn = "Score", Type = VisitFieldType.Number },
                new() { Code = "signage", LabelAr = "اللوحة", LabelEn = "Signage", Type = VisitFieldType.Select, Options = [new() { Code = "OK", LabelAr = "سليمة", LabelEn = "OK" }, new() { Code = "MISSING", LabelAr = "غير موجودة", LabelEn = "Missing" }] },
                new() { Code = "safe", LabelAr = "آمن", LabelEn = "Safe", Type = VisitFieldType.Checkbox },
            ],
            PhotosRequired = strict ? 1 : 0,
            RequireLocation = strict,
            RequireSignature = strict,
            InspectorRole = role,
            DueDays = 4,
        }));
        return code;
    }

    /// <summary>An external visit type: booked through one operation, results read through another or notified.</summary>
    public async Task<string> ExternalTypeAsync(bool polled = true)
    {
        Host.Provider.Respond = request => (request.Method.Method, request.Url.AbsolutePath) switch
        {
            (_, "/oauth/v1/token") => (HttpStatusCode.OK, MomrahSamples.Token("token-1")),
            ("POST", "/v1/inspections") => (HttpStatusCode.OK, Momrah($$"""{ "inspectionId": "{{NextReference}}" }""")),
            _ => (HttpStatusCode.OK, Momrah(Result)),
        };
        var create = await MomrahSamples.PublishAsync(Host, operation: (connection, envelope) => new OperationPayload
        {
            NameAr = "حجز معاينة",
            NameEn = "Book an inspection",
            ConnectionCode = connection,
            EnvelopeProfileCode = envelope,
            Method = "POST",
            PathTemplate = "/v1/inspections",
            IsIdempotent = false,
            Parameters = [Body("visitNumber"), Body("siteAddress"), Body("activity")],
            Outputs = [new() { Name = "reference", Path = "inspectionId" }],
        });
        var result = await MomrahSamples.PublishAsync(Host, operation: (connection, envelope) => new OperationPayload
        {
            NameAr = "نتيجة المعاينة",
            NameEn = "Inspection result",
            ConnectionCode = connection,
            EnvelopeProfileCode = envelope,
            Method = "GET",
            PathTemplate = "/v1/inspections/{reference}",
            IsIdempotent = true,
            Parameters = [new() { Name = "reference", Location = ParameterLocation.Path }],
            Outputs = [new() { Name = "status", Path = "status" }, new() { Name = "outcome", Path = "result" }, new() { Name = "findings", Path = "findings" }, new() { Name = "report", Path = "summary" }],
        });
        var code = TestHost.Unique("CIS");
        await Host.PublishAsync(VisitChangeTypes.Area, new Change(VisitChangeTypes.Type, code, ChangeOperation.Create, new VisitTypePayload
        {
            NameAr = "معاينة السلامة",
            NameEn = "Safety inspection",
            Mode = VisitMode.External,
            Outcomes = [Outcome("PASS", OutcomeKind.Pass), Outcome("FAIL", OutcomeKind.Fail)],
            External = new ExternalVisitSettings
            {
                CreateOperation = create,
                Inputs = new()
                {
                    ["visitNumber"] = System.Text.Json.Nodes.JsonNode.Parse("""{"var": "visit.number"}"""),
                    ["siteAddress"] = System.Text.Json.Nodes.JsonNode.Parse("""{"var": "visit.siteAddress"}"""),
                    ["activity"] = System.Text.Json.Nodes.JsonNode.Parse("""{"var": "data.activity"}"""),
                },
                ReferenceOutput = "reference",
                ResultOperation = polled ? result : null,
                PollMinutes = 10,
                Result = new ExternalResultFields { Reference = "inspectionId", Status = "status", CompletedValue = "COMPLETED", Outcome = "outcome", Data = "findings", Report = "report" },
                OutcomeMap = new() { ["COMPLIANT"] = "PASS", ["NON_COMPLIANT"] = "FAIL" },
                CallbackSecret = TestSecrets.CallbackSecret,
            },
        }));
        return code;
    }

    /// <summary>The reference the fake external system gives the next booking.</summary>
    public string NextReference { get; set; } = "INSP-1";

    /// <summary>What the fake external system answers for a result.</summary>
    public string Result { get; set; } = """{ "status": "SCHEDULED" }""";

    public Task<VisitInfo> RequestAsync(string type, Guid subject, System.Text.Json.Nodes.JsonObject? facts = null) =>
        Host.AsAsync("officer", sp => sp.GetRequiredService<IVisits>().CreateAsync(
            new VisitRequest(type, "TEST", subject, "T-1", Guid.CreateVersion7(), "مؤسسة الاختبار", "طلب اختبار", "Test request", "الرياض، حي العليا", 24.7, 46.67, "اتصل قبل الوصول", null, facts ?? new System.Text.Json.Nodes.JsonObject { ["data"] = new System.Text.Json.Nodes.JsonObject { ["activity"] = "مطعم" } }),
            CancellationToken.None));

    public Task<T> AsAsync<T>(string user, Func<IServiceProvider, Task<T>> action) => Host.AsAsync(user, action);

    public async ValueTask DisposeAsync() => await Host.DisposeAsync();

    private static VisitOutcome Outcome(string code, OutcomeKind kind) => new() { Code = code, NameAr = code == "PASS" ? "مطابق" : "غير مطابق", NameEn = code == "PASS" ? "Compliant" : "Not compliant", Kind = kind };

    private static string Momrah(string result) => $$"""{ "statusDetails": [{ "code": 200, "message": "Ok" }], "data": { "responseCode": "1", "responseMessage": "success", "result": {{result}} } }""";

    private static OperationParameter Body(string name) => new() { Name = name, Location = ParameterLocation.Body };
}
