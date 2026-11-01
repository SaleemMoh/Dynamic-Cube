using System.Text.Json.Nodes;
using DynamicCube.Modules.Documents.Persistence;
using DynamicCube.Modules.Workflow.Domain;
using DynamicCube.Modules.Workflow.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Workflow;
using DynamicCube.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Workflow.Tests;

/// <summary>
/// The NHC demo data (deploy/demo-data/nhc) loads into an empty installation, and a new certificate request goes through
/// the normal track: the branch requests manager assigns, three analyst/auditor pairs evaluate, the grade is calculated by
/// the rules of the guide, the general manager approves and the certificate is issued. The individuals' demo service
/// issues a qualification card.
/// </summary>
public sealed class NhcDemoTests
{
    [Fact]
    public async Task The_demo_loads_and_a_normal_track_request_is_classified_per_domain()
    {
        // Its own database: the demo uses the same codes as the classification samples other tests load.
        var database = new DatabaseFixture();
        await database.InitializeAsync();
        try
        {
            if (database.ConnectionString is null)
            {
                Assert.Skip($"Set {DatabaseFixture.EnvironmentVariable} to run PostgreSQL integration tests.");
            }

            await using var host = new WorkflowHost(database);
            var variables = new Dictionary<string, string> { ["GATEWAY_URL"] = "http://localhost:5090", ["GATEWAY_PUBLIC_URL"] = "http://localhost:5090" };
            await host.Host.LoadAsync(PackageLoader.ReadFolder(Path.Combine(Root(), "deploy/demo-data/nhc"), variables));

            var subject = await host.StartAsync("CLS_NEW", Request(), level: "NORMAL", sla: 60);
            await ClaimAndActAsync(host, subject, "DISTRIBUTE", "ruh.manager", "DISTRIBUTE");
            (await host.OpenTasksAsync(subject)).Select(t => t.NodeCode).OrderBy(c => c, StringComparer.Ordinal).ShouldBe(["FIN_ANALYSIS", "GOV_ANALYSIS", "PROJECTS_ANALYSIS"]);
            foreach (var (node, analyst) in new[] { ("GOV_ANALYSIS", "gov.analyst1"), ("PROJECTS_ANALYSIS", "prj.analyst1"), ("FIN_ANALYSIS", "fin.analyst1") })
            {
                var task = await Single(host, subject, node);
                await host.AsAsync("ruh.manager", c => c.AssignAsync(task.Id, new AssignRequest(analyst), CancellationToken.None));
                await host.AsAsync(analyst, c => c.ActAsync(task.Id, new ActRequest("SEND_TO_AUDIT", null), CancellationToken.None));
            }

            await ClaimAndActAsync(host, subject, "GOV_AUDIT", "gov.auditor", "CONFIRM");
            await ClaimAndActAsync(host, subject, "PROJECTS_AUDIT", "prj.auditor", "CONFIRM");
            await ClaimAndActAsync(host, subject, "FIN_AUDIT", "audit.manager", "CONFIRM");
            await ClaimAndActAsync(host, subject, "GM_APPROVAL", "nhc.gm", "APPROVE");

            host.Subject.Last(subject).Status.ShouldBe(SubjectStatus.Approved);
            var instance = await host.InstanceAsync(subject);
            instance.Status.ShouldBe(InstanceStatus.Completed);
            var grading = JsonNode.Parse(instance.Facts)!["decisions"]!["grading"]!;
            // Construction: performance 3 (75M cumulative, 15M largest) is its worst axis; ICT: every axis 2.
            var grades = grading["items"]!.AsArray().ToDictionary(i => i!["sector"]!.GetValue<string>(), i => i!["grade"]!.GetValue<double>());
            grades.ShouldBe(new Dictionary<string, double> { ["CON"] = 3, ["ICT"] = 2 }, ignoreOrder: true);
            grading["sizeClass"]!.GetValue<string>().ShouldBe("MEDIUM");

            var number = JsonNode.Parse(instance.Facts)!["documents"]!["CERTIFICATE"]!["number"]!.GetValue<string>();
            number.ShouldStartWith("NHC");
            var version = await host.Host.AsAsync("admin", async sp =>
            {
                var documents = sp.GetRequiredService<DocumentsDbContext>();
                var document = await documents.Documents.SingleAsync(d => d.Number == number);
                return await documents.Versions.SingleAsync(v => v.DocumentId == document.Id);
            });
            version.Lines.ShouldContain("التشييد والبناء");

            // The individuals' demo service: an engineer with 12 years of experience. The membership check cannot reach
            // the Saudi Council of Engineers here, so the branch checks it by hand; 10 years or more go to the general
            // manager; the qualification card is issued with the expert grade.
            var person = await host.StartAsync("IND_QUALIFY", new JsonObject
            {
                ["IND_PROFESSION"] = "ENGINEER",
                ["IND_ENG_SPECIALTY"] = "CIVIL",
                ["IND_SCE_NUMBER"] = "123456",
                ["IND_DEGREE"] = "BACHELOR",
                ["IND_YEARS"] = 12,
                ["IND_REGION"] = "MAKKAH",
            }, level: "STANDARD", sla: 10);
            await ClaimAndActAsync(host, person, "INTAKE", "ruh.manager", "ACCEPT");
            await ClaimAndActAsync(host, person, "SCE_MANUAL", "ruh.manager", "VERIFIED");
            await ClaimAndActAsync(host, person, "EVALUATION", "prj.analyst1", "RECOMMEND");
            await ClaimAndActAsync(host, person, "GM_APPROVAL", "nhc.gm", "APPROVE");
            host.Subject.Last(person).Status.ShouldBe(SubjectStatus.Approved);
            var visited = (await host.InstanceAsync(person)).VisitedNodes;
            visited.ShouldContain("SCE_CHECK");
            visited.ShouldNotContain("PAY", "the standard track pays nothing");
            var card = JsonNode.Parse((await host.InstanceAsync(person)).Facts)!["documents"]!["ISSUE"]!["number"]!.GetValue<string>();
            card.ShouldStartWith("QLF");
            var cardData = await host.Host.AsAsync("admin", async sp =>
            {
                var documents = sp.GetRequiredService<DocumentsDbContext>();
                var document = await documents.Documents.SingleAsync(d => d.Number == card);
                return JsonNode.Parse((await documents.Versions.SingleAsync(v => v.DocumentId == document.Id)).Data)!;
            });
            cardData["profession"]!.GetValue<string>().ShouldBe("مهندس");
            cardData["specialty"]!.GetValue<string>().ShouldBe("هندسة مدنية");
            cardData["grade"]!.GetValue<string>().ShouldBe("خبير");
            cardData["region"]!.GetValue<string>().ShouldBe("منطقة مكة المكرمة");

            // The public API (ADR-0042): the regions endpoint as the partner demo client calls it.
            var regions = await host.Host.AsAsync("admin", async sp =>
            {
                var db = sp.GetRequiredService<Integration.Persistence.IntegrationDbContext>();
                var endpoint = await db.ApiEndpoints.SingleAsync(e => e.Code == "REGIONS");
                var client = await db.ApiClients.SingleAsync(c => c.Code == "PARTNER_DEMO");
                client.Endpoints.ShouldBe(["REGIONS", "REQUEST_STATUS", "CERTIFICATE_CHECK", "LOOKUPS", "CLASSES"]);
                var caller = new DynamicCube.Platform.PublicApi.ApiCaller(client.Code, client.Companies, null);
                var runner = sp.GetRequiredService<Integration.Runtime.PublicApiRunner>();
                var all = await runner.RunAsync(endpoint, new Dictionary<string, string>(), new Dictionary<string, string?>(), caller, false, CancellationToken.None);
                var makkah = await runner.RunAsync(endpoint, new Dictionary<string, string>(), new Dictionary<string, string?> { ["name"] = "mak" }, caller, false, CancellationToken.None);
                return (all.Answer, makkah.Answer);
            });
            regions.Item1.Status.ShouldBe(200);
            regions.Item1.Body!["items"]!.AsArray().Count.ShouldBe(13);
            regions.Item2.Body!["items"]!.AsArray().Select(r => r!["code"]!.GetValue<string>()).ShouldBe(["MAKKAH"]);

            // Several classes and the public regions list in one call.
            var lookups = await host.Host.AsAsync("admin", async sp =>
            {
                var db = sp.GetRequiredService<Integration.Persistence.IntegrationDbContext>();
                var endpoint = await db.ApiEndpoints.SingleAsync(e => e.Code == "LOOKUPS");
                var caller = new DynamicCube.Platform.PublicApi.ApiCaller("PARTNER_DEMO", ["MAIN"], null);
                return (await sp.GetRequiredService<Integration.Runtime.PublicApiRunner>().RunAsync(
                    endpoint, new Dictionary<string, string>(), new Dictionary<string, string?> { ["classes"] = "REGION,SVC_CATEGORY", ["lovs"] = "REGIONS" }, caller, false, CancellationToken.None)).Answer;
            });
            lookups.Status.ShouldBe(200);
            lookups.Body!["classes"]!.AsObject().Select(c => c.Key).ShouldBe(["REGION", "SVC_CATEGORY"]);
            lookups.Body!["lovs"]!["REGIONS"]!.AsArray().Count.ShouldBe(13);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    /// <summary>A medium contractor asking for construction and ICT (answers as the portal form stores them).</summary>
    private static JsonObject Request() => new()
    {
        ["CLS_DOMAINS"] = new JsonArray("CON", "ICT"),
        ["CLS_PRIMARY_DOMAIN"] = "CON",
        ["CLS_EMPLOYEES"] = 120,
        ["CLS_SAUDI_EMPLOYEES"] = 60,
        ["CLS_ENGINEERS"] = 30,
        ["CLS_TECHNICIANS"] = 40,
        ["CLS_DGA_CATEGORY"] = "GOLD",
        ["CLS_PROJECTS_VALUE"] = 75,
        ["CLS_LARGEST_PROJECT"] = 15,
        // Governance 80 of 100 points (grade 2): G1, G2 monthly, G3, G4, G7, G8, G9, G11.
        ["CLS_GOV_G1"] = "YES",
        ["CLS_GOV_G2"] = "MONTHLY",
        ["CLS_GOV_G3"] = "YES",
        ["CLS_GOV_G4"] = "YES",
        ["CLS_GOV_G5"] = "NO",
        ["CLS_GOV_G6"] = "NONE",
        ["CLS_GOV_G7"] = "YES",
        ["CLS_GOV_G8"] = "YES",
        ["CLS_GOV_G9"] = "YES",
        ["CLS_GOV_G10"] = "NO",
        ["CLS_GOV_G11"] = "YES",
        // BBB- is rank 10 of the scale: financial grade 2.
        ["CLS_CREDIT_RATING"] = "BBB-",
        ["CLS_WITHDRAWN"] = "NO",
    };

    private static async Task<WorkTask> Single(WorkflowHost host, Guid subject, string node) =>
        (await host.OpenTasksAsync(subject)).Single(t => t.NodeCode == node);

    private static async Task ClaimAndActAsync(WorkflowHost host, Guid subject, string node, string user, string action)
    {
        var task = await Single(host, subject, node);
        await host.AsAsync(user, c => c.ClaimAsync(task.Id, CancellationToken.None));
        await host.AsAsync(user, c => c.ActAsync(task.Id, new ActRequest(action, null), CancellationToken.None));
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory!.FullName, "DynamicCube.slnx")))
        {
            directory = directory.Parent;
        }

        return directory.FullName;
    }
}
