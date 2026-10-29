using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.Communication.Changes;
using DynamicCube.Modules.Decisions.Changes;
using DynamicCube.Modules.Documents.Changes;
using DynamicCube.Modules.Integration.Changes;
using DynamicCube.Modules.Parties.Changes;
using DynamicCube.Modules.Payments.Changes;
using DynamicCube.Modules.Services.Changes;
using DynamicCube.Modules.Services.Persistence;
using DynamicCube.Modules.Visits.Changes;
using DynamicCube.Modules.Visits.Persistence;
using DynamicCube.Modules.Workflow.Changes;
using DynamicCube.Modules.Workflow.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Decisions;
using DynamicCube.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Services.Tests;

public sealed class PackageTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task The_classification_packages_publish()
    {
        await using var host = new TestHost(fixture.RequireConnectionString());
        await Packages.ServicesAsync(host, Items("classification-new.json"));

        var version = await host.AsAsync("admin", sp => sp.GetRequiredService<ServicesDbContext>().Versions.AsNoTracking().SingleAsync(v => v.Code == "CLASSIFICATION_NEW/1"));
        version.Resolved.ShouldNotBeNull();
        version.Resolved.ShouldContain("CLS_ANNUAL_REVENUE");

        // Publishing the service gave it its workflow; the workflow package adds the staff and version 2 of it.
        var workflow = Items("classification-workflow.json");
        workflow.ShouldNotContain(i => i.EntityType.StartsWith("svc.", StringComparison.Ordinal));
        await host.PublishAsync(AppsControlAreas.Security, [.. workflow.Where(i => i.EntityType.StartsWith("apps.", StringComparison.Ordinal))]);
        await host.PublishAsync(WorkflowChangeTypes.Area, [.. workflow.Where(i => i.EntityType.StartsWith("wf.", StringComparison.Ordinal))]);

        // The certificate package: contact fields, the certificate, the grading rules, workflow version 2 and the messages.
        var certificate = Items("classification-certificate.json");
        await host.PublishAsync(PartyChangeTypes.Area, [.. certificate.Where(i => i.EntityType.StartsWith("party.", StringComparison.Ordinal))]);
        await host.PublishAsync(DocumentChangeTypes.Area, [.. certificate.Where(i => i.EntityType.StartsWith("doc.", StringComparison.Ordinal))]);
        await host.PublishAsync(DecisionChangeTypes.Area, [.. certificate.Where(i => i.EntityType.StartsWith("dec.", StringComparison.Ordinal))]);
        await host.PublishAsync(WorkflowChangeTypes.Area, [.. certificate.Where(i => i.EntityType.StartsWith("wf.", StringComparison.Ordinal))]);
        await host.PublishAsync(CommunicationChangeTypes.Area, [.. certificate.Where(i => i.EntityType.StartsWith("com.", StringComparison.Ordinal))]);
        var versions = await host.AsAsync("admin", sp => sp.GetRequiredService<WorkflowDbContext>().Versions.AsNoTracking()
            .Where(v => v.Workflow!.ServiceCode == "CLASSIFICATION_NEW").OrderBy(v => v.Number).Select(v => v.Code).ToListAsync());
        versions.ShouldBe(["CLASSIFICATION_NEW/1", "CLASSIFICATION_NEW/2", "CLASSIFICATION_NEW/3"]);

        // Renewal and extension act on the certificate; their workflows amend it.
        await Packages.ServicesAsync(host, Items("classification-renewal.json"));
        await host.PublishAsync(WorkflowChangeTypes.Area, [.. Items("classification-renewal-workflow.json")]);
        var followUps = await host.AsAsync("admin", sp => sp.GetRequiredService<ServicesDbContext>().Services.AsNoTracking()
            .Where(s => s.DocumentType == "CLS_CERTIFICATE").OrderBy(s => s.DisplayOrder).Select(s => s.Code).ToListAsync());
        followUps.ShouldBe(["CLASSIFICATION_RENEW", "CLASSIFICATION_EXTEND"]);
        var renewal = await host.AsAsync("admin", sp => sp.GetRequiredService<WorkflowDbContext>().Versions.AsNoTracking().SingleAsync(v => v.Code == "CLASSIFICATION_EXTEND/2"));
        renewal.PublishedAt.ShouldNotBeNull();

        // SADAD over the MoMRAH gateway, the bill messages, and version 4: paid levels pay before processing.
        var momrah = Items("momrah-mci.json");
        await host.PublishAsync(IntegrationChangeTypes.Area, [.. momrah.Where(i => i.EntityType.StartsWith("integ.", StringComparison.Ordinal))]);
        var sadad = Items("payments-sadad.json");
        await host.PublishAsync(IntegrationChangeTypes.Area, [.. sadad.Where(i => i.EntityType.StartsWith("integ.", StringComparison.Ordinal))]);
        await host.PublishAsync(PaymentChangeTypes.Area, [.. sadad.Where(i => i.EntityType.StartsWith("pay.", StringComparison.Ordinal))]);
        await host.PublishAsync(CommunicationChangeTypes.Area, [.. sadad.Where(i => i.EntityType.StartsWith("com.", StringComparison.Ordinal))]);
        await host.PublishAsync(WorkflowChangeTypes.Area, [.. Items("classification-payment.json")]);
        var paid = await host.AsAsync("admin", sp => sp.GetRequiredService<WorkflowDbContext>().Versions.AsNoTracking().SingleAsync(v => v.Code == "CLASSIFICATION_NEW/4"));
        paid.Definition.ShouldContain("\"PAYMENT\"");

        // Site visits: the portal's sample services get an inspector's visit and a safety inspection by the CIS.
        await Packages.PortalServicesAsync(host);
        var visits = Items("site-visits.json");
        await host.PublishAsync(IntegrationChangeTypes.Area, [.. visits.Where(i => i.EntityType.StartsWith("integ.", StringComparison.Ordinal))]);
        await host.PublishAsync(AppsControlAreas.Security, [.. visits.Where(i => i.EntityType.StartsWith("apps.", StringComparison.Ordinal))]);
        await host.PublishAsync(VisitChangeTypes.Area, [.. visits.Where(i => i.EntityType.StartsWith("vis.", StringComparison.Ordinal))]);
        await host.PublishAsync(WorkflowChangeTypes.Area, [.. visits.Where(i => i.EntityType.StartsWith("wf.", StringComparison.Ordinal))]);
        await host.PublishAsync(CommunicationChangeTypes.Area, [.. visits.Where(i => i.EntityType.StartsWith("com.", StringComparison.Ordinal))]);
        var inspected = await host.AsAsync("admin", sp => sp.GetRequiredService<WorkflowDbContext>().Versions.AsNoTracking()
            .Where(v => (v.Code == "SITE_VISIT/2" || v.Code == "EVENT_PERMIT/2") && v.PublishedAt != null).Select(v => v.Definition).ToListAsync());
        inspected.Count.ShouldBe(2);
        inspected.ShouldAllBe(d => d.Contains("\"VISIT\""));
        var types = await host.AsAsync("admin", sp => sp.GetRequiredService<VisitsDbContext>().Types.AsNoTracking().Where(t => t.Code.EndsWith("_INSPECTION")).OrderBy(t => t.Code).Select(t => t.Code).ToListAsync());
        types.ShouldBe(["SAFETY_INSPECTION", "SITE_INSPECTION"]);

        // A small establishment with good staffing, a gold DGA category and a 95M track record: grade 2 in both sectors.
        var graded = await GradeAsync(host, new JsonObject
        {
            ["sectors"] = new JsonArray("ICT", "CONSTRUCTION"),
            ["employees"] = 40,
            ["saudis"] = 30,
            ["engineers"] = 12,
            ["technicians"] = 15,
            ["dgaCategory"] = "GOLD",
            ["projectsValue"] = 95,
            ["largestProject"] = 30,
            ["board"] = true,
            ["erp"] = true,
            ["pms"] = true,
            ["org"] = false,
            ["iso"] = true,
        });
        graded["sizeClass"]!.GetValue<string>().ShouldBe("SMALL");
        graded["sizeClassNameAr"]!.GetValue<string>().ShouldBe("صغيرة");
        graded["eligible"]!.GetValue<bool>().ShouldBeTrue();
        graded["bestGrade"]!.GetValue<double>().ShouldBe(2);
        var sectors = graded["items"]!.AsArray();
        sectors.Select(i => i!["sectorNameAr"]!.GetValue<string>()).ShouldBe(["الاتصالات وتقنية المعلومات", "التشييد والبناء"]);
        sectors[0]!["technical"]!.GetValue<double>().ShouldBe(2, "ICT's technical grade is the DGA category");
        sectors[1]!["technical"]!.GetValue<double>().ShouldBe(1);
        sectors.ShouldAllBe(i => i!["grade"]!.GetValue<double>() == 2 && i["classified"]!.GetValue<bool>());

        // A micro establishment without engineers or technicians is not classified in any sector.
        var weak = await GradeAsync(host, new JsonObject { ["sectors"] = new JsonArray("CONSTRUCTION"), ["employees"] = 3, ["saudis"] = 0, ["engineers"] = 0, ["technicians"] = 0, ["projectsValue"] = 200, ["largestProject"] = 80 });
        weak["sizeClass"]!.GetValue<string>().ShouldBe("MICRO");
        weak["eligible"]!.GetValue<bool>().ShouldBeFalse();
        weak["items"]![0]!["grade"]!.GetValue<double>().ShouldBe(6);
    }

    [Fact]
    public async Task The_portal_home_setting_publishes_as_a_public_setting()
    {
        await using var host = new TestHost(fixture.RequireConnectionString());
        var setting = Items("portal-services.json").Single(i => i.EntityType == AppsControlChangeTypes.Setting);
        await host.PublishAsync(AppsControlAreas.Settings, setting);

        var row = await host.AsAsync("admin", sp => sp.GetRequiredService<AppsControl.Persistence.AppsControlDbContext>().Settings.AsNoTracking().SingleAsync(s => s.Code == "PORTAL_HOME"));
        row.IsPublic.ShouldBeTrue();
        JsonNode.Parse(row.Value)!["statistics"]!.AsArray().Select(t => t!["metric"]!.GetValue<string>()).ShouldBe(["SERVICES", "REQUESTS", "IN_PROGRESS", "COMPLETED"]);
    }

    private static Task<JsonObject> GradeAsync(TestHost host, JsonObject input) =>
        host.AsAsync("analyst", async sp => (await sp.GetRequiredService<IDecisionService>().EvaluateAsync(new DecisionRequest("CLS_GRADING", input, "TEST", Guid.CreateVersion7(), null), CancellationToken.None)).Output);

    private static List<Change> Items(string file) => Packages.Items(file);
}
