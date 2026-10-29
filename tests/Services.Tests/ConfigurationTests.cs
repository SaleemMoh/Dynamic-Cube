using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.Services.Changes;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Persistence;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Services.Tests;

public sealed class ConfigurationTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private TestHost _host = null!;

    public ValueTask InitializeAsync()
    {
        _host = new TestHost(fixture.RequireConnectionString());
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    private Task<ServiceVersion> VersionAsync(string code) =>
        _host.AsAsync("admin", sp => sp.GetRequiredService<ServicesDbContext>().Versions.AsNoTracking().SingleAsync(v => v.Code == code));

    [Fact]
    public async Task Publishing_a_version_snapshots_its_questions()
    {
        var sample = new ServiceSamples();
        await _host.PublishAsync(ServiceChangeTypes.Area, sample.All());

        // Later edits of the bank do not reach version 1; version 2 sees them.
        var relabel = new QuestionPayload { NameAr = "عدد العاملين", NameEn = "Staff count", Type = QuestionType.Number, Validation = new QuestionValidation { Min = 1 } };
        await _host.PublishAsync(ServiceChangeTypes.Area, new Change(ServiceChangeTypes.Question, sample.Employees, ChangeOperation.Update, relabel), sample.Version(2));

        var v1 = JsonSerializer.Deserialize<ResolvedVersion>((await VersionAsync($"{sample.Service}/1")).Resolved!, ChangeJson.Options)!;
        var v2 = JsonSerializer.Deserialize<ResolvedVersion>((await VersionAsync($"{sample.Service}/2")).Resolved!, ChangeJson.Options)!;

        v1.Questions.Count.ShouldBe(6);
        v1.Questions[sample.Employees].LabelEn.ShouldBe("Employees");
        v2.Questions[sample.Employees].LabelEn.ShouldBe("Staff count");
        v1.Number.ShouldBe(1);
        v1.Definition.Fees.Single().Bracket!.Rows.Count.ShouldBe(3);
        (await VersionAsync($"{sample.Service}/1")).PublishedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Published_versions_cannot_change_but_can_be_retired()
    {
        var sample = new ServiceSamples();
        await _host.PublishAsync(ServiceChangeTypes.Area, sample.All());
        var changed = sample.Definition();
        changed.Levels.RemoveAt(1);
        changed.Steps[0].TitleEn = "Level";

        var errors = await Should.ThrowAsync<ChangeValidationException>(() => _host.PublishAsync(ServiceChangeTypes.Area,
            new Change(ServiceChangeTypes.Version, $"{sample.Service}/1", ChangeOperation.Update, new ServiceVersionPayload { NameAr = "x", NameEn = "x", Definition = changed })));
        var skipped = await Should.ThrowAsync<ChangeValidationException>(() => _host.PublishAsync(ServiceChangeTypes.Area, sample.Version(3)));
        await _host.PublishAsync(ServiceChangeTypes.Area,
            new Change(ServiceChangeTypes.Version, $"{sample.Service}/1", ChangeOperation.Update, new ServiceVersionPayload { NameAr = "الإصدار 1", NameEn = "Version 1", IsRetired = true, Definition = sample.Definition() }));

        errors.Errors.Keys.ShouldBe(["definition"]);
        skipped.Errors["code"].Single().ShouldContain("is 2");
        (await VersionAsync($"{sample.Service}/1")).IsRetired.ShouldBeTrue();
    }

    [Fact]
    public async Task Forms_carry_their_layout_and_it_is_checked()
    {
        var sample = new ServiceSamples();
        await _host.PublishAsync(ServiceChangeTypes.Area, sample.All());
        var laidOut = sample.Definition();
        var details = laidOut.Steps.Single(s => s.Code == "DETAILS");
        details.Display = SectionDisplay.Tabs;
        details.Sections[0].Columns = 2;
        details.Sections[0].Questions[0].Width = FieldWidth.Full;
        await _host.PublishAsync(ServiceChangeTypes.Area, sample.Version(2, laidOut));
        var stored = JsonDocument.Parse((await VersionAsync($"{sample.Service}/2")).Definition).RootElement;
        var step = stored.GetProperty("steps").EnumerateArray().Single(s => s.GetProperty("code").GetString() == "DETAILS");
        step.GetProperty("display").GetString().ShouldBe("TABS");
        step.GetProperty("sections")[0].GetProperty("columns").GetInt32().ShouldBe(2);
        step.GetProperty("sections")[0].GetProperty("questions")[0].GetProperty("width").GetString().ShouldBe("FULL");

        var wrong = sample.Definition();
        wrong.Steps.Single(s => s.Code == "DETAILS").Sections[0].Columns = 4;
        wrong.Steps.Single(s => s.Code == "FEES").Display = SectionDisplay.Accordion;
        var errors = await Should.ThrowAsync<ChangeValidationException>(() => _host.PublishAsync(ServiceChangeTypes.Area, sample.Version(3, wrong)));
        errors.Errors.Keys.ShouldContain(k => k.EndsWith(".columns", StringComparison.Ordinal));
        errors.Errors.Keys.ShouldContain(k => k.EndsWith(".display", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_version_published_before_newer_fields_existed_can_still_be_retired()
    {
        var sample = new ServiceSamples();
        await _host.PublishAsync(ServiceChangeTypes.Area, sample.All());

        // As stored by an earlier release: no key for the fields added since (left out here when empty).
        await _host.AsAsync("admin", async sp =>
        {
            var db = sp.GetRequiredService<ServicesDbContext>();
            var version = await db.Versions.SingleAsync(v => v.Code == $"{sample.Service}/1");
            version.Definition = Strip(JsonNode.Parse(version.Definition))!.ToJsonString();
            await db.SaveChangesAsync();
            return 0;
        });

        await _host.PublishAsync(ServiceChangeTypes.Area,
            new Change(ServiceChangeTypes.Version, $"{sample.Service}/1", ChangeOperation.Update, new ServiceVersionPayload { NameAr = "الإصدار 1", NameEn = "Version 1", IsRetired = true, Definition = sample.Definition() }));
        (await VersionAsync($"{sample.Service}/1")).IsRetired.ShouldBeTrue();
    }

    /// <summary>Drops null and empty values, as a definition serialized before those fields existed would lack them.</summary>
    private static JsonNode? Strip(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj.ToList())
                {
                    var kept = Strip(value);
                    obj.Remove(key);
                    if (kept is not null && kept is not JsonArray { Count: 0 } && kept is not JsonObject { Count: 0 })
                    {
                        obj[key] = kept;
                    }
                }

                return obj;
            case JsonArray array:
                return new JsonArray([.. array.Select(i => Strip(i)?.DeepClone())]);
            default:
                return node?.DeepClone();
        }
    }

    [Fact]
    public async Task Services_belong_to_existing_categories_and_cards_are_checked()
    {
        var sample = new ServiceSamples();
        var category = TestHost.Unique("CAT");
        ServicePayload Payload(string? categoryCode) => new() { NameAr = "خدمة", NameEn = "Service", PartyKinds = [Platform.Parties.PartyKind.Establishment], CategoryCode = categoryCode };

        // Categories are values of class SVC_CATEGORY (Class/Domain), published in their own change set; one in use
        // cannot be deleted.
        await _host.PublishAsync(AppsControlAreas.Lookups, new Change(ServiceChangeTypes.Category, category, ChangeOperation.Create, new { nameAr = "التصاريح", nameEn = "Permits", icon = "event" }));
        await _host.PublishAsync(ServiceChangeTypes.Area, [.. sample.Questions(), new Change(ServiceChangeTypes.Service, sample.Service, ChangeOperation.Create, Payload(category))]);
        await Should.ThrowAsync<ChangeSetException>(() => _host.PublishAsync(AppsControlAreas.Lookups, new Change(ServiceChangeTypes.Category, category, ChangeOperation.Delete, null)));

        var bad = sample.Definition();
        bad.Card = new ServiceCardDefinition
        {
            Steps = [new CardText { TextAr = "التقديم" }],
            RequiredDocuments = [new RequiredDocument { NameAr = "السجل", NameEn = "CR" }],
            Channels = [ServiceChannel.Portal, ServiceChannel.Portal],
            FaqUrl = "javascript:alert(1)",
            Phone = "call us",
            Email = "nobody",
        };
        var card = await Should.ThrowAsync<ChangeValidationException>(() => _host.PublishAsync(ServiceChangeTypes.Area, sample.Version(1, bad)));
        card.Errors.Keys.Order().ShouldBe(["definition.card.channels", "definition.card.email", "definition.card.faqUrl", "definition.card.phone", "definition.card.steps[0].textEn"]);

        bad.Card = new ServiceCardDefinition { Steps = [new CardText { TextAr = "التقديم", TextEn = "Apply" }], Channels = [ServiceChannel.Portal], FaqUrl = "/help", Phone = "+966 11 000-0000", Email = "help@example.gov.sa" };
        await _host.PublishAsync(ServiceChangeTypes.Area, sample.Version(1, bad));
    }

    [Fact]
    public async Task Definitions_are_checked_before_publishing()
    {
        var sample = new ServiceSamples();
        await _host.PublishAsync(ServiceChangeTypes.Area, [.. sample.Questions(), sample.ServiceChange()]);
        var bad = sample.Definition();
        bad.Steps[1].Sections[0].Questions.Add(new QuestionUse { Code = "NO_SUCH_QUESTION" });
        bad.Steps[2].Sections[0].VisibleWhen = ServiceSamples.Rule("""{"unknown_operation": [1]}""");
        bad.Steps[4].TextEn = null;
        bad.Fees[0].Levels.Add("GOLDEN");
        bad.Fees[0].Bracket!.Rows[1].From = 5_000_000;
        bad.Eligibility.Add(new RuleDefinition { Code = "NOT_BANNED", Rule = ServiceSamples.Rule("true"), MessageAr = "x", MessageEn = "x" });

        var errors = await Should.ThrowAsync<ChangeValidationException>(() => _host.PublishAsync(ServiceChangeTypes.Area, sample.Version(1, bad)));

        errors.Errors.Keys.ShouldBe(
            [
                "definition.steps[1].sections[0].questions[2].code",
                "definition.steps[2].sections[0].visibleWhen",
                "definition.steps[4].textEn",
                "definition.fees[0].levels",
                "definition.fees[0].bracket.rows[1].from",
                "definition.eligibility",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Questions_used_by_versions_cannot_be_deleted()
    {
        var sample = new ServiceSamples();
        await _host.PublishAsync(ServiceChangeTypes.Area, sample.All());

        var error = await Should.ThrowAsync<ChangeSetException>(() => _host.PublishAsync(ServiceChangeTypes.Area,
            new Change(ServiceChangeTypes.Question, sample.Revenue, ChangeOperation.Delete, null)));

        error.Message.ShouldContain("used by service versions");
    }
}
