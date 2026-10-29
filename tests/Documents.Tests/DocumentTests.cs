using System.Text.Json.Nodes;
using DynamicCube.Modules.Documents.Changes;
using DynamicCube.Modules.Documents.Domain;
using DynamicCube.Modules.Documents.Persistence;
using DynamicCube.Modules.Documents.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Documents;
using DynamicCube.Platform.Files;
using DynamicCube.Platform.Outbox;
using DynamicCube.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Documents.Tests;

public sealed class DocumentTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Templates_must_parse_and_types_need_a_known_template()
    {
        await using var host = new DocumentHost(fixture);
        var ex = await Should.ThrowAsync<ChangeValidationException>(() => host.Host.PublishAsync(
            DocumentChangeTypes.Area,
            new Change(DocumentChangeTypes.Template, DocumentHost.Unique("TPL"), ChangeOperation.Create, new { nameAr = "قالب", nameEn = "Template", html = "{% for x in %}" })));
        ex.Errors.ShouldContainKey("html");

        var bad = await Should.ThrowAsync<ChangeValidationException>(() => host.Host.PublishAsync(
            DocumentChangeTypes.Area,
            new Change(DocumentChangeTypes.DocumentType, DocumentHost.Unique("CERT"), ChangeOperation.Create, new
            {
                nameAr = "شهادة",
                nameEn = "Certificate",
                numberPrefix = "cc-1",
                templateCode = "MISSING",
                alertDaysBefore = new[] { 30 },
            })));
        bad.Errors.Keys.ShouldBe(["numberPrefix", "alertDaysBefore", "templateCode"], ignoreOrder: true);
    }

    [Fact]
    public async Task Issuing_numbers_the_document_and_a_second_issue_for_the_holder_amends_it()
    {
        await using var host = new DocumentHost(fixture);
        var type = await host.TypeAsync(onePerHolder: true, validityMonths: 24, amendResets: false);
        var holder = Guid.CreateVersion7();

        var first = await host.IssueAsync(type, holder, "Riyadh", ("CONSTRUCTION", 2), ("ICT", 3));
        first.Amended.ShouldBeFalse();
        first.Version.ShouldBe(1);
        first.Number.ShouldMatch(@"^CC[A-Z0-9]+-2026-000001$");
        first.ExpiresAt.ShouldBe(host.Clock.Now.AddMonths(24));

        // A render job is waiting for the worker.
        (await host.Host.AsAsync("admin", sp => sp.GetRequiredService<DocumentsDbContext>().OutboxMessages.AnyAsync(m => m.Type == RenderDocumentMessage.MessageType))).ShouldBeTrue();

        host.Clock.Now = host.Clock.Now.AddMonths(3);
        var second = await host.IssueAsync(type, holder, "Jeddah", ("CONSTRUCTION", 1));
        second.Amended.ShouldBeTrue();
        second.Id.ShouldBe(first.Id);
        second.Number.ShouldBe(first.Number);
        second.Version.ShouldBe(2);
        second.ExpiresAt.ShouldBe(first.ExpiresAt, "amendments keep the validity unless the type resets it");

        var detail = await host.RegistryAsync("admin", r => r.GetAsync(first.Id, null, CancellationToken.None));
        detail.Document.CurrentVersion.ShouldBe(2);
        detail.Versions.Select(v => v.Version).ShouldBe([2, 1]);
        detail.Versions[0].Data["city"]!.GetValue<string>().ShouldBe("Jeddah");
        detail.Events.Select(e => e.Type).ShouldBe(["ISSUED", "AMENDED"]);

        // Another holder gets their own number; numbers restart every year (the clock is now in 2027).
        var other = await host.IssueAsync(type, Guid.CreateVersion7(), "Dammam", ("OM", 4));
        other.Number.ShouldEndWith("-2027-000001");
        (await host.IssueAsync(type, Guid.CreateVersion7(), "Abha", ("OM", 5))).Number.ShouldEndWith("-2027-000002");
    }

    [Fact]
    public async Task The_pdf_is_printed_from_the_template_with_the_holder_lines_and_qr_code_and_kept()
    {
        await using var host = new DocumentHost(fixture);
        var type = await host.TypeAsync();
        var issued = await host.IssueAsync(type, Guid.CreateVersion7(), "Riyadh", ("CONSTRUCTION", 2));

        var file = await host.RegistryAsync("admin", r => r.PdfAsync(issued.Id, null, null, CancellationToken.None));
        using (var reader = new StreamReader(file.Content))
        {
            (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).ShouldStartWith("%PDF");
        }

        file.FileName.ShouldBe($"{issued.Number}-v1.pdf");
        var html = host.Pdf.Printed.Single();
        html.ShouldContain("شهادة التصنيف");
        html.ShouldContain("<td>CONSTRUCTION</td><td>2</td>");
        html.ShouldContain("data:image/png;base64,");
        html.ShouldContain("/verify/");
        html.ShouldContain("@font-face");

        // Printed once: the second download reads the stored file, and the worker's job finds nothing to do.
        await host.RegistryAsync("admin", r => r.PdfAsync(issued.Id, 1, null, CancellationToken.None));
        await host.Host.AsAsync("worker", async sp =>
        {
            var version = await sp.GetRequiredService<DocumentsDbContext>().Versions.SingleAsync(v => v.DocumentId == issued.Id);
            await sp.GetRequiredService<RenderDocumentHandler>().HandleAsync(new RenderDocumentMessage(version.Id), CancellationToken.None);
            return version;
        });
        host.Pdf.Printed.Count.ShouldBe(1);

        // A stored file that went missing (another host's disk, an older backup) is printed again.
        await host.Host.AsAsync("admin", async sp =>
        {
            var key = await sp.GetRequiredService<DocumentsDbContext>().Versions.Where(v => v.DocumentId == issued.Id).Select(v => v.FileKey!).SingleAsync();
            await sp.GetRequiredService<IFileStorage>().DeleteAsync(key, CancellationToken.None);
        });
        (await host.RegistryAsync("admin", r => r.PdfAsync(issued.Id, 1, null, CancellationToken.None))).FileName.ShouldBe($"{issued.Number}-v1.pdf");
        host.Pdf.Printed.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Status_actions_need_a_reason_block_amendments_and_show_on_the_public_page()
    {
        await using var host = new DocumentHost(fixture);
        var type = await host.TypeAsync();
        var holder = Guid.CreateVersion7();
        var issued = await host.IssueAsync(type, holder, "Riyadh", ("CONSTRUCTION", 2));
        var code = await host.Host.AsAsync("admin", sp => sp.GetRequiredService<DocumentsDbContext>().Documents.Where(d => d.Id == issued.Id).Select(d => d.VerificationCode).SingleAsync());

        var verify = await host.RegistryAsync("anyone", r => r.VerifyAsync(code, CancellationToken.None));
        verify.Status.ShouldBe(DocumentStatus.Active);
        verify.Fields.Select(f => f.NameEn).ShouldBe(["City"], "only public fields are shown");
        verify.LineFields.Select(f => f.Code).ShouldBe(["sector", "grade"]);
        verify.Lines[0]!.AsObject().ContainsKey("points").ShouldBeFalse();

        await Should.ThrowAsync<DocumentValidationException>(() => host.RegistryAsync("officer", r => Done(r.SuspendAsync(issued.Id, " ", CancellationToken.None))));
        await host.RegistryAsync("officer", r => Done(r.SuspendAsync(issued.Id, "Inspection pending", CancellationToken.None)));
        (await host.RegistryAsync("anyone", r => r.VerifyAsync(code, CancellationToken.None))).Status.ShouldBe(DocumentStatus.Suspended);
        await Should.ThrowAsync<DocumentIssueException>(() => host.IssueAsync(type, holder, "Riyadh", ("CONSTRUCTION", 1)));

        await host.RegistryAsync("officer", r => Done(r.ReinstateAsync(issued.Id, "Inspection done", CancellationToken.None)));
        await host.RegistryAsync("officer", r => Done(r.CancelAsync(issued.Id, "Forged statements", CancellationToken.None)));
        await Should.ThrowAsync<DocumentConflictException>(() => host.RegistryAsync("officer", r => Done(r.SuspendAsync(issued.Id, "Again", CancellationToken.None))));

        // A cancelled document is not amended: the holder gets a new one.
        var renewed = await host.IssueAsync(type, holder, "Riyadh", ("CONSTRUCTION", 3));
        renewed.Amended.ShouldBeFalse();
        renewed.Number.ShouldNotBe(issued.Number);

        var events = (await host.RegistryAsync("admin", r => r.GetAsync(issued.Id, null, CancellationToken.None))).Events;
        events.Select(e => e.Type).ShouldBe(["ISSUED", "SUSPENDED", "REINSTATED", "CANCELLED"]);
        events[1].Comment.ShouldBe("Inspection pending");
        events[1].Actor.ShouldBe("officer");
    }

    [Fact]
    public async Task Holders_see_only_their_documents_and_expired_documents_expire()
    {
        await using var host = new DocumentHost(fixture);
        var type = await host.TypeAsync(validityMonths: 12);
        var holder = Guid.CreateVersion7();
        var issued = await host.IssueAsync(type, holder, "Riyadh", ("ICT", 1));

        (await host.RegistryAsync("applicant", r => r.GetAsync(issued.Id, holder, CancellationToken.None))).Versions[0].IssuedBy.ShouldBeEmpty();
        await Should.ThrowAsync<DocumentNotFoundException>(() => host.RegistryAsync("applicant", r => r.GetAsync(issued.Id, Guid.CreateVersion7(), CancellationToken.None)));
        (await host.RegistryAsync("applicant", r => r.SearchAsync(null, null, null, holder, 0, 50, CancellationToken.None))).Total.ShouldBe(1);

        host.Clock.Now = host.Clock.Now.AddMonths(12).AddMinutes(1);
        await host.Host.AsAsync("worker", sp => sp.GetRequiredService<DocumentDates>().RunAsync(CancellationToken.None));
        var expired = await host.RegistryAsync("admin", r => r.GetAsync(issued.Id, null, CancellationToken.None));
        expired.Document.Status.ShouldBe(DocumentStatus.Expired);
        expired.Events[^1].Type.ShouldBe("EXPIRED");

        // An expired one-per-holder document is not amended either.
        (await host.IssueAsync(type, holder, "Riyadh", ("ICT", 1))).Amended.ShouldBeFalse();
    }

    [Fact]
    public async Task Follow_up_requests_amend_their_document_renewing_or_extending_it()
    {
        await using var host = new DocumentHost(fixture);
        // A short validity: the expiry job below runs for the whole database, so the clock must not pass other tests' dates.
        var type = await host.TypeAsync(validityMonths: 3);
        var holder = Guid.CreateVersion7();
        var issued = await host.IssueAsync(type, holder, "Riyadh", ("ICT", 2), ("BUILDING", 3));
        var expires = issued.ExpiresAt!.Value;

        Task<IssuedDocumentInfo> AmendAsync(Guid document, ValidityChange? validity, JsonObject? data = null, Guid? holderId = null) =>
            host.Host.AsAsync("issuer", sp => sp.GetRequiredService<IDocumentIssuer>().IssueAsync(
                new DocumentIssue(type, holderId ?? holder, data ?? [], null, "SERVICE_REQUEST", Guid.CreateVersion7(), "REN-2026-000001", "Renewed", document, validity),
                CancellationToken.None));
        Task<DocumentFacts?> FactsAsync() => host.Host.AsAsync("reader", sp => sp.GetRequiredService<IDocumentDirectory>().GetAsync(issued.Id, CancellationToken.None));

        // Renewing a month before expiry adds a full period to the current expiry; data and lines not given are kept.
        host.Clock.Now = expires.AddMonths(-1);
        var renewed = await AmendAsync(issued.Id, new ValidityChange(ValidityMode.Renew), new JsonObject { ["city"] = "Jeddah" });
        renewed.Amended.ShouldBeTrue();
        renewed.Number.ShouldBe(issued.Number);
        renewed.ExpiresAt.ShouldBe(expires.AddMonths(3));
        var facts = (await FactsAsync())!;
        facts.Version.ShouldBe(2);
        facts.Data["city"]!.GetValue<string>().ShouldBe("Jeddah");
        facts.Data["internalNote"]!.GetValue<string>().ShouldBe("secret");
        facts.Lines.Count.ShouldBe(2);
        facts.ToJson()["expiresOn"]!.GetValue<string>().ShouldBe(DateOnly.FromDateTime(expires.AddMonths(3).UtcDateTime).ToString("O", System.Globalization.CultureInfo.InvariantCulture));

        // Once expired it cannot simply be amended, but an extension makes it active again.
        host.Clock.Now = renewed.ExpiresAt!.Value.AddDays(10);
        await host.Host.AsAsync("worker", sp => sp.GetRequiredService<DocumentDates>().RunAsync(CancellationToken.None));
        (await FactsAsync())!.Status.ShouldBe("EXPIRED");
        await Should.ThrowAsync<DocumentIssueException>(() => AmendAsync(issued.Id, new ValidityChange(ValidityMode.Keep)));
        await Should.ThrowAsync<DocumentIssueException>(() => AmendAsync(issued.Id, new ValidityChange(ValidityMode.Extend)));
        var extended = await AmendAsync(issued.Id, new ValidityChange(ValidityMode.Extend, 3));
        extended.ExpiresAt.ShouldBe(renewed.ExpiresAt.Value.AddMonths(3));
        (await FactsAsync())!.Status.ShouldBe("ACTIVE");

        // Only the holder's document of that type.
        await Should.ThrowAsync<DocumentIssueException>(() => AmendAsync(issued.Id, null, holderId: Guid.CreateVersion7()));
        var listed = await host.Host.AsAsync("reader", sp => sp.GetRequiredService<IDocumentDirectory>().OfHolderAsync(holder, type, CancellationToken.None));
        listed.Single().Number.ShouldBe(issued.Number);
    }

    [Fact]
    public async Task Holders_are_reminded_once_on_each_reminder_day_and_told_of_changes()
    {
        await using var host = new DocumentHost(fixture);
        var type = await host.TypeAsync(validityMonths: 12, alerts: [90, 30, 7]);
        var holder = Guid.CreateVersion7();
        var issued = await host.IssueAsync(type, holder, "Riyadh", ("ICT", 1));
        var expires = issued.ExpiresAt!.Value;

        async Task<List<string>> RunAsync(DateTimeOffset at)
        {
            host.Clock.Now = at;
            await host.Host.AsAsync("worker", sp => sp.GetRequiredService<DocumentDates>().RunAsync(CancellationToken.None));
            return [.. (await host.Host.RaisedAsync(issued.Id)).Select(e => e.Code)];
        }

        (await RunAsync(expires.AddDays(-120))).ShouldBe(["DOCUMENT_ISSUED"]);
        (await RunAsync(expires.AddDays(-89))).ShouldBe(["DOCUMENT_ISSUED", "DOCUMENT_EXPIRING"]);
        (await RunAsync(expires.AddDays(-88))).Count.ShouldBe(2, "each reminder is sent once");

        // A run that comes late sends the nearest reminder only.
        var late = await RunAsync(expires.AddDays(-5));
        late.Count(c => c == "DOCUMENT_EXPIRING").ShouldBe(2);
        var reminder = (await host.Host.RaisedAsync(issued.Id))[^1];
        reminder.PartyId.ShouldBe(holder);
        reminder.SubjectNumber.ShouldBe(issued.Number);
        reminder.Facts["document"]!["alertDays"]!.GetValue<int>().ShouldBe(7);
        reminder.Facts["document"]!["daysLeft"]!.GetValue<int>().ShouldBe(5);
        reminder.Facts["document"]!["verifyUrl"]!.GetValue<string>().ShouldStartWith("http://localhost:4201/verify/");

        await host.Host.AsAsync("officer", sp => sp.GetRequiredService<DocumentRegistry>().SuspendAsync(issued.Id, "Inspection", CancellationToken.None));
        (await RunAsync(expires.AddDays(1)))[^1].ShouldBe("DOCUMENT_EXPIRED");
        var events = await host.Host.RaisedAsync(issued.Id);
        events.Select(e => e.Code).ShouldContain("DOCUMENT_SUSPENDED");
        events.Single(e => e.Code == "DOCUMENT_SUSPENDED").Facts["document"]!["reason"]!.GetValue<string>().ShouldBe("Inspection");
    }

    [Fact]
    public async Task Gotenberg_prints_arabic_pdfs_when_it_is_available()
    {
        var url = Environment.GetEnvironmentVariable("DYNAMICCUBE_TEST_GOTENBERG");
        Assert.SkipWhen(string.IsNullOrEmpty(url), "Set DYNAMICCUBE_TEST_GOTENBERG (e.g. http://localhost:3000) to print through a real Gotenberg.");
        await using var host = new DocumentHost(fixture, realPdf: true);
        var (content, type) = await host.Host.AsAsync("admin", sp =>
        {
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DocumentsOptions>>().Value.GotenbergUrl = new Uri(url!);
            return sp.GetRequiredService<DocumentRegistry>().PreviewAsync(
                new TemplatePreviewRequest(DocumentHost.TemplateHtml, "A4", false, new JsonObject { ["city"] = "الرياض" }, [new JsonObject { ["sector"] = "التشييد", ["grade"] = 1 }], "pdf"),
                CancellationToken.None);
        });
        type.ShouldBe("application/pdf");
        System.Text.Encoding.ASCII.GetString(content, 0, 5).ShouldBe("%PDF-");
        content.Length.ShouldBeGreaterThan(5000);
    }

    private static async Task<bool> Done(Task task)
    {
        await task;
        return true;
    }
}
