using System.Text.Json.Nodes;
using DynamicCube.Modules.Decisions.Changes;
using DynamicCube.Modules.Documents.Changes;
using DynamicCube.Modules.Documents.Persistence;
using DynamicCube.Modules.Workflow.Changes;
using DynamicCube.Modules.Workflow.Domain;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Workflow;
using DynamicCube.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using static DynamicCube.Modules.Workflow.Tests.WorkflowHost;

namespace DynamicCube.Modules.Workflow.Tests;

public sealed class DecisionDocumentTests(DatabaseFixture fixture)
{
    private static object Decide(string code, string ruleSet, string next) => new
    {
        code,
        type = "DECISION",
        nameAr = "حساب الدرجة",
        nameEn = "Grade",
        ruleSet,
        input = new { score = new { var = "data.SCORE" }, sectors = new { var = "data.SECTORS" } },
        result = "grading",
        next,
    };

    private const string Rules = """
        {
          "variables": [{ "code": "grade", "rule": {"if": [{">=": [{"var": "input.score"}, 80]}, 1, {">=": [{"var": "input.score"}, 50]}, 3, 0]} }],
          "items": { "source": {"var": "input.sectors"}, "as": "sector", "output": { "sector": {"var": "sector"}, "grade": {"var": "vars.grade"} } },
          "output": { "grade": {"var": "vars.grade"}, "eligible": {">": [{"var": "vars.grade"}, 0]} }
        }
        """;

    private static object RuleSetVersion(bool retired) => new { nameAr = "1", nameEn = "1", isRetired = retired, configuration = JsonNode.Parse(Rules) };

    private static object Issue(string code, string documentType, string next) => new
    {
        code,
        type = "DOCUMENT",
        nameAr = "إصدار الشهادة",
        nameEn = "Issue certificate",
        documentType,
        data = new { grade = new { var = "decisions.grading.grade" } },
        lines = new { source = new { var = "decisions.grading.items" }, @as = "line", fields = new { sector = new { var = "line.sector" }, grade = new { var = "line.grade" } } },
        reason = "Approved",
        next,
    };

    private static async Task<(string RuleSet, string DocumentType)> ConfigureAsync(WorkflowHost host)
    {
        var ruleSet = Unique("GRADE");
        await host.Host.PublishAsync(
            DecisionChangeTypes.Area,
            new Change(DecisionChangeTypes.RuleSet, ruleSet, ChangeOperation.Create, new { nameAr = "الدرجة", nameEn = "Grade", engine = "RULES" }),
            new Change(DecisionChangeTypes.Version, ruleSet + "/1", ChangeOperation.Create, RuleSetVersion(false)));

        var template = Unique("TPL");
        var type = Unique("CERT");
        await host.Host.PublishAsync(
            DocumentChangeTypes.Area,
            new Change(DocumentChangeTypes.Template, template, ChangeOperation.Create, new { nameAr = "قالب", nameEn = "Template", html = "<p>{{ document.number }} {{ data.grade }}</p>" }),
            new Change(DocumentChangeTypes.DocumentType, type, ChangeOperation.Create, new
            {
                nameAr = "شهادة",
                nameEn = "Certificate",
                numberPrefix = "WD" + type[^4..],
                validityMonths = 12,
                onePerHolder = true,
                templateCode = template,
                fields = new[] { new { code = "grade", nameAr = "الدرجة", nameEn = "Grade", isPublic = true } },
                lineFields = new[] { new { code = "sector", nameAr = "القطاع", nameEn = "Sector", isPublic = true }, new { code = "grade", nameAr = "الدرجة", nameEn = "Grade", isPublic = true } },
            }));
        return (ruleSet, type);
    }

    [Fact]
    public async Task Decisions_route_the_request_and_approved_ones_get_a_document()
    {
        await using var host = new WorkflowHost(fixture);
        var (ruleSet, type) = await ConfigureAsync(host);
        var workflow = await host.WorkflowAsync(
        [
            Start("GRADE"),
            Decide("GRADE", ruleSet, "ROUTE"),
            Gateway("ROUTE", [new { when = JsonNode.Parse("""{"var": "decisions.grading.eligible"}"""), to = "CERTIFY" }], "REJECTED"),
            Issue("CERTIFY", type, "APPROVED"),
            End("APPROVED", "APPROVED"),
            End("REJECTED", "REJECTED"),
        ]);

        var started = await host.StartAsync(workflow, new JsonObject { ["SCORE"] = 90, ["SECTORS"] = new JsonArray("CONSTRUCTION", "ICT") });

        var instance = await host.InstanceAsync(started);
        instance.Status.ShouldBe(InstanceStatus.Completed);
        instance.Outcome.ShouldBe("APPROVED");
        var facts = JsonNode.Parse(instance.Facts)!;
        facts["decisions"]!["grading"]!["grade"]!.GetValue<double>().ShouldBe(1);
        facts["decisions"]!["grading"]!["version"]!.GetValue<int>().ShouldBe(1);
        var number = facts["documents"]!["CERTIFY"]!["number"]!.GetValue<string>();
        number.ShouldStartWith("WD" + type[^4..] + "-");

        var events = await host.Host.AsAsync("admin", sp => sp.GetRequiredService<Persistence.WorkflowDbContext>().Events.Where(e => e.InstanceId == instance.Id).OrderBy(e => e.Id).Select(e => e.Type).ToListAsync());
        events.ShouldContain(WorkflowEventTypes.Decided);
        events.ShouldContain(WorkflowEventTypes.DocumentIssued);
        events.IndexOf(WorkflowEventTypes.Decided).ShouldBeLessThan(events.IndexOf(WorkflowEventTypes.DocumentIssued));

        var document = await host.Host.AsAsync("admin", sp => sp.GetRequiredService<DocumentsDbContext>().Documents.SingleAsync(d => d.Number == number));
        document.HolderPartyId.ShouldBe(started); // the test subject is for a party with its own id
        var version = await host.Host.AsAsync("admin", sp => sp.GetRequiredService<DocumentsDbContext>().Versions.SingleAsync(v => v.DocumentId == document.Id));
        version.Lines.ShouldContain("ICT");

        var rejected = await host.StartAsync(workflow, new JsonObject { ["SCORE"] = 10, ["SECTORS"] = new JsonArray() });
        var other = await host.InstanceAsync(rejected);
        other.Outcome.ShouldBe("REJECTED");
        JsonNode.Parse(other.Facts)!["documents"].ShouldBeNull();
    }

    [Fact]
    public async Task Decision_and_document_steps_are_checked_when_published()
    {
        await using var host = new WorkflowHost(fixture);
        var code = await host.ServiceAsync();
        var ex = await Should.ThrowAsync<ChangeValidationException>(() => host.Host.PublishAsync(
            WorkflowChangeTypes.Area,
            new Change(WorkflowChangeTypes.Version, code + "/2", ChangeOperation.Create, new
            {
                nameAr = "1",
                nameEn = "1",
                definition = new
                {
                    nodes = new object[]
                    {
                        Start("GRADE"),
                        new { code = "GRADE", type = "DECISION", nameAr = "ق", nameEn = "D", result = "bad name", next = "CERTIFY" },
                        new { code = "CERTIFY", type = "DOCUMENT", nameAr = "و", nameEn = "Doc", data = new { x = new { no_such_op = 1 } }, next = "END" },
                        End("END", "APPROVED"),
                    },
                },
            })));
        ex.Errors.Keys.ShouldContain(k => k.Contains("ruleSet"));
        ex.Errors.Keys.ShouldContain(k => k.Contains("result"));
        ex.Errors.Keys.ShouldContain(k => k.Contains("documentType"));
        ex.Errors.Keys.ShouldContain(k => k.Contains("data.x"));
    }

    [Fact]
    public async Task A_workflow_cannot_be_published_with_a_rule_set_that_has_no_published_version()
    {
        await using var host = new WorkflowHost(fixture);
        var ruleSet = Unique("EMPTY");
        await host.Host.PublishAsync(DecisionChangeTypes.Area, new Change(DecisionChangeTypes.RuleSet, ruleSet, ChangeOperation.Create, new { nameAr = "فارغة", nameEn = "Empty", engine = "RULES" }));

        var ex = await Should.ThrowAsync<ChangeValidationException>(() => host.WorkflowAsync(
        [
            Start("GRADE"),
            Decide("GRADE", ruleSet, "END"),
            End("END", "APPROVED"),
        ]));

        // The problem and the steps to fix it, under the step's rule set.
        var messages = ex.Errors.Single(e => e.Key.EndsWith(".ruleSet", StringComparison.Ordinal)).Value;
        messages[0].ShouldContain($"'{ruleSet}'");
        messages[0].ShouldContain("'GRADE'");
        messages.Skip(1).Select(m => m[..2]).ShouldBe(["1.", "2.", "3."]);
    }

    [Fact]
    public async Task The_last_version_of_a_rule_set_in_use_cannot_be_retired_nor_the_rule_set_deactivated()
    {
        await using var host = new WorkflowHost(fixture);
        var (ruleSet, _) = await ConfigureAsync(host);
        var workflow = await host.WorkflowAsync([Start("GRADE"), Decide("GRADE", ruleSet, "END"), End("END", "APPROVED")]);

        var retire = await Should.ThrowAsync<ChangeValidationException>(() =>
            host.Host.PublishAsync(DecisionChangeTypes.Area, new Change(DecisionChangeTypes.Version, ruleSet + "/1", ChangeOperation.Update, RuleSetVersion(true))));
        var deactivate = await Should.ThrowAsync<ChangeValidationException>(() =>
            host.Host.PublishAsync(DecisionChangeTypes.Area, new Change(DecisionChangeTypes.RuleSet, ruleSet, ChangeOperation.Update, new { nameAr = "الدرجة", nameEn = "Grade", engine = "RULES", isActive = false })));

        retire.Errors["isRetired"][0].ShouldContain(workflow + "/");
        retire.Errors["isRetired"].Skip(1).Select(m => m[..2]).ShouldBe(["1.", "2.", "3.", "4."]);
        deactivate.Errors["isActive"][0].ShouldContain(workflow + "/");

        // With a newer version in use, the old one may be retired.
        await host.Host.PublishAsync(
            DecisionChangeTypes.Area,
            new Change(DecisionChangeTypes.Version, ruleSet + "/2", ChangeOperation.Create, RuleSetVersion(false)),
            new Change(DecisionChangeTypes.Version, ruleSet + "/1", ChangeOperation.Update, RuleSetVersion(true)));
    }

    [Fact]
    public async Task A_rule_set_that_can_no_longer_be_evaluated_stops_the_start_with_a_configuration_error()
    {
        await using var host = new WorkflowHost(fixture);
        var (ruleSet, _) = await ConfigureAsync(host);
        var workflow = await host.WorkflowAsync([Start("GRADE"), Decide("GRADE", ruleSet, "END"), End("END", "APPROVED")]);
        await host.Host.AsAsync("admin", async sp =>
        {
            var decisions = sp.GetRequiredService<Decisions.Persistence.DecisionsDbContext>();
            await decisions.Versions.Where(v => v.RuleSet!.Code == ruleSet).ExecuteUpdateAsync(u => u.SetProperty(v => v.IsRetired, true));
            return true;
        });

        var ex = await Should.ThrowAsync<WorkflowConfigurationException>(() => host.StartAsync(workflow, new JsonObject { ["SCORE"] = 90 }));

        ex.Message.ShouldContain(ruleSet);
        ex.Message.ShouldContain($"Workflow {workflow}/");
    }
}
