using System.Text.Json.Nodes;
using DynamicCube.Modules.Documents.Changes;
using DynamicCube.Modules.Documents.Persistence;
using DynamicCube.Modules.Workflow.Changes;
using DynamicCube.Modules.Workflow.Domain;
using DynamicCube.Modules.Workflow.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using static DynamicCube.Modules.Workflow.Tests.WorkflowHost;

namespace DynamicCube.Modules.Workflow.Tests;

/// <summary>One request with several items: each decided on its own at a task, and a document issued per approved item.</summary>
public sealed class TaskItemTests(DatabaseFixture fixture)
{
    private static async Task<string> DocumentTypeAsync(WorkflowHost host)
    {
        var template = Unique("TPL");
        var type = Unique("SECT");
        await host.Host.PublishAsync(
            DocumentChangeTypes.Area,
            new Change(DocumentChangeTypes.Template, template, ChangeOperation.Create, new { nameAr = "قالب", nameEn = "Template", html = "<p>{{ document.number }} {{ data.sector }}</p>" }),
            new Change(DocumentChangeTypes.DocumentType, type, ChangeOperation.Create, new
            {
                nameAr = "شهادة قطاع",
                nameEn = "Sector certificate",
                numberPrefix = "WS" + type[^4..],
                validityMonths = 12,
                onePerHolder = false,
                templateCode = template,
                fields = new[] { new { code = "sector", nameAr = "القطاع", nameEn = "Sector", isPublic = true } },
            }));
        return type;
    }

    [Fact]
    public async Task Each_item_is_decided_on_its_own_and_approved_ones_get_a_document()
    {
        await using var host = new WorkflowHost(fixture);
        var type = await DocumentTypeAsync(host);
        var reviewer = User("rev");
        var roles = await host.RolesAsync("REVIEWER");
        await host.UsersAsync((reviewer, roles));
        var workflow = await host.WorkflowAsync(
        [
            Start("REVIEW"),
            new
            {
                code = "REVIEW",
                type = "HUMAN_TASK",
                nameAr = "مراجعة القطاعات",
                nameEn = "Review sectors",
                assignment = Role(roles[0]),
                actions = new object[] { Act("DONE", "ROUTE"), Return() },
                items = new
                {
                    source = JsonNode.Parse("""{"var": "data.SECTORS"}"""),
                    @as = "sector",
                    key = JsonNode.Parse("""{"var": "sector.code"}"""),
                    label = JsonNode.Parse("""{"var": "sector.name"}"""),
                },
            },
            Gateway("ROUTE", [new { when = JsonNode.Parse("""{">": [{"var": "items.REVIEW.approvedCount"}, 0]}"""), to = "CERTIFY" }], "NO"),
            new
            {
                code = "CERTIFY",
                type = "DOCUMENT",
                nameAr = "شهادة لكل قطاع",
                nameEn = "A certificate per sector",
                documentType = type,
                forEach = JsonNode.Parse("""{"var": "items.REVIEW.approvedItems"}"""),
                forEachAs = "sector",
                data = new { sector = new { var = "sector.code" } },
                reason = "Approved",
                next = "OK",
            },
            End("OK", "APPROVED"),
            End("NO", "REJECTED"),
        ]);

        var sectors = new JsonArray(
            new JsonObject { ["code"] = "BUILD", ["name"] = "Buildings" },
            new JsonObject { ["code"] = "ROADS", ["name"] = "Roads" },
            new JsonObject { ["code"] = "WATER", ["name"] = "Water" });
        var subject = await host.StartAsync(workflow, new JsonObject { ["SECTORS"] = sectors });
        var task = (await host.OpenTasksAsync(subject)).ShouldHaveSingleItem();
        await host.AsAsync(reviewer, c => c.ClaimAsync(task.Id, CancellationToken.None));

        var detail = await host.AsAsync(reviewer, c => c.GetAsync(task.Id, CancellationToken.None));
        detail.Items.Select(i => $"{i.Key}:{i.Label}").ShouldBe(["BUILD:Buildings", "ROADS:Roads", "WATER:Water"]);

        // Saved while working: only some items decided.
        await host.AsAsync(reviewer, c => c.SaveInputsAsync(task.Id, new InputsRequest(null, [new ItemDecision("BUILD", "approved", null)]), CancellationToken.None));
        (await host.AsAsync(reviewer, c => c.GetAsync(task.Id, CancellationToken.None))).Items[0].Decision.ShouldBe("APPROVED");

        // Moving on needs every item decided and a reason for each rejection.
        (await Should.ThrowAsync<WorkflowValidationException>(() => host.AsAsync(reviewer, c => c.ActAsync(task.Id, new ActRequest("DONE", null, null, [new ItemDecision("BUILD", "APPROVED", null)]), CancellationToken.None)))).Field.ShouldBe("items.ROADS");
        (await Should.ThrowAsync<WorkflowValidationException>(() => host.AsAsync(reviewer, c => c.ActAsync(task.Id, new ActRequest("DONE", null, null,
            [new ItemDecision("BUILD", "APPROVED", null), new ItemDecision("ROADS", "REJECTED", " "), new ItemDecision("WATER", "APPROVED", null)]), CancellationToken.None)))).Field.ShouldBe("items.ROADS");

        await host.AsAsync(reviewer, c => c.ActAsync(task.Id, new ActRequest("DONE", null, null,
            [new ItemDecision("BUILD", "APPROVED", null), new ItemDecision("ROADS", "REJECTED", "No projects in roads"), new ItemDecision("WATER", "APPROVED", null)]), CancellationToken.None));

        var instance = await host.InstanceAsync(subject);
        instance.Outcome.ShouldBe("APPROVED");
        var facts = JsonNode.Parse(instance.Facts)!;
        facts["items"]!["REVIEW"]!["approved"]!.ToJsonString().ShouldBe("""["BUILD","WATER"]""");
        facts["items"]!["REVIEW"]!["rejectedCount"]!.GetValue<int>().ShouldBe(1);
        var documents = facts["documents"]!["CERTIFY"]!;
        documents["count"]!.GetValue<int>().ShouldBe(2);
        var numbers = documents["items"]!.AsArray().Select(d => d!["number"]!.GetValue<string>()).ToList();
        numbers.Distinct().Count().ShouldBe(2);
        var issued = await host.Host.AsAsync("admin", sp => sp.GetRequiredService<DocumentsDbContext>().Documents.CountAsync(d => numbers.Contains(d.Number)));
        issued.ShouldBe(2);

        // The applicant is told each decision, with the reason for the rejected one.
        var told = host.Subject.Items[subject];
        told.Select(i => $"{i.Key}:{i.Decision}").ShouldBe(["BUILD:APPROVED", "ROADS:REJECTED", "WATER:APPROVED"]);
        told.Single(i => i.Key == "ROADS").Reason.ShouldBe("No projects in roads");
    }

    [Fact]
    public async Task Items_and_documents_per_element_are_checked_when_published()
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
                        new { code = "START", type = "START", nameAr = "بداية", nameEn = "Start", next = "REVIEW", forEach = JsonNode.Parse("""{"var": "x"}""") },
                        new { code = "REVIEW", type = "HUMAN_TASK", nameAr = "مراجعة", nameEn = "Review", assignment = Role("R"), actions = new object[] { Act("OK", "DONE") }, items = new { @as = "bad name" } },
                        End("DONE", "APPROVED"),
                    },
                },
            })));

        ex.Errors.Keys.ShouldContain("definition.nodes[0].forEach");
        ex.Errors.Keys.ShouldContain("definition.nodes[1].items.source");
        ex.Errors.Keys.ShouldContain("definition.nodes[1].items.as");
    }
}
