using System.Text.Json.Nodes;
using DynamicCube.Modules.Communication.Changes;
using DynamicCube.Modules.Communication.Domain;
using DynamicCube.Modules.Communication.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Communication.Tests;

public sealed class CommunicationTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Templates_and_rules_are_checked_when_published()
    {
        await using var host = new CommunicationHost(fixture);
        var empty = await Should.ThrowAsync<ChangeValidationException>(() => host.Host.PublishAsync(
            CommunicationChangeTypes.Area,
            new Change(CommunicationChangeTypes.Template, CommunicationHost.Unique("T"), ChangeOperation.Create, new { nameAr = "ق", nameEn = "T", smsAr = "{% if x %}", smsEn = "only English" })));
        empty.Errors.Keys.ShouldContain("smsAr");

        var template = await host.TemplateAsync(email: false);
        var rule = await Should.ThrowAsync<ChangeValidationException>(() => host.Host.PublishAsync(
            CommunicationChangeTypes.Area,
            new Change(CommunicationChangeTypes.Rule, CommunicationHost.Unique("R"), ChangeOperation.Create, new
            {
                nameAr = "ق",
                nameEn = "R",
                eventCode = "NOPE",
                templateCode = template,
                channels = new[] { "EMAIL", "FAX" },
                recipients = new object[] { new { kind = "ROLE" }, new { kind = "ALIEN" } },
                condition = new { no_such_op = 1 },
            })));
        rule.Errors.Keys.ShouldBe(["eventCode", "channels", "recipients[0].value", "recipients[1].kind", "condition"], ignoreOrder: true);
        rule.Errors["channels"].ShouldContain(m => m.Contains("FAX"));
        rule.Errors["channels"].ShouldContain(m => m.Contains("no EMAIL wording"));
    }

    [Fact]
    public async Task Events_become_in_app_notifications_and_emails_for_the_party_and_staff()
    {
        await using var host = new CommunicationHost(fixture);
        var (reviewer, colleague) = (TestHost.Unique("rev").ToLowerInvariant(), TestHost.Unique("col").ToLowerInvariant());
        var role = await host.RoleWithUsersAsync((reviewer, "reviewer@example.com"), (colleague, null));
        var template = await host.TemplateAsync();
        await host.RuleAsync(template, ["IN_APP", "EMAIL", "SMS"], [new { kind = "PARTY" }], condition: JsonNode.Parse("""{"<=": [{"var": "request.grade"}, 3]}"""));
        await host.RuleAsync(template, ["IN_APP", "EMAIL"], [new { kind = "ROLE", value = role }, new { kind = "USER", value = new { var = "request.reviewer" } }], language: "EN");

        var party = Guid.CreateVersion7();
        var raised = await host.RaiseAsync(party, grade: 2, reviewer: reviewer);
        var messages = (await host.MessagesAsync(raised.SubjectId)).Items;

        // The party: in-app (both languages), an Arabic email waiting for the worker, SMS skipped (not configured here).
        var forParty = messages.Where(m => m.Recipient == party.ToString()).ToList();
        forParty.Select(m => (m.Channel, m.Status)).ShouldBe([("EMAIL", MessageStatus.Pending), ("IN_APP", MessageStatus.Sent), ("SMS", MessageStatus.Skipped)], ignoreOrder: true);
        var email = forParty.Single(m => m.Channel == "EMAIL");
        email.Address.ShouldStartWith("p");
        email.Title.ShouldBe($"اعتماد الطلب {raised.SubjectNumber}");
        forParty.Single(m => m.Channel == "SMS").Error!.ShouldContain("not configured");

        // Staff: the role's members and the reviewer once each; the colleague without an email gets in-app only.
        var forStaff = messages.Where(m => m.RecipientKind == "USER").ToList();
        forStaff.Count(m => m.Recipient == reviewer).ShouldBe(2);
        forStaff.Single(m => m.Recipient == colleague && m.Channel == "EMAIL").Status.ShouldBe(MessageStatus.Skipped);
        forStaff.Single(m => m.Recipient == reviewer && m.Channel == "EMAIL").Title.ShouldBe($"Request {raised.SubjectNumber} approved");

        // Delivering the email: HTML encoded, wrapped right-to-left, sent once.
        await host.SendAsync(email.Id);
        await host.SendAsync(email.Id);
        var sent = host.Email.Sent.ShouldHaveSingleItem();
        sent.Body.ShouldContain("dir=\"rtl\"");
        sent.Body.ShouldContain("بالدرجة 2 & شكراً");
        sent.Body.ShouldContain("مؤسسة " + party.ToString()[..4]);
        (await host.MessagesAsync(raised.SubjectId)).Items.Single(m => m.Id == email.Id).Status.ShouldBe(MessageStatus.Sent);

        // The same event delivered again creates nothing.
        await host.DispatchAsync(raised);
        (await host.MessagesAsync(raised.SubjectId)).Total.ShouldBe(messages.Count);

        // A condition that does not hold sends nothing to the party.
        var other = await host.RaiseAsync(party, grade: 5, reviewer: reviewer);
        (await host.MessagesAsync(other.SubjectId)).Items.ShouldAllBe(m => m.RecipientKind == "USER");
    }

    [Fact]
    public async Task Failed_emails_are_recorded_retried_and_can_be_sent_again()
    {
        await using var host = new CommunicationHost(fixture);
        var template = await host.TemplateAsync(sms: false, inApp: false);
        await host.RuleAsync(template, ["EMAIL"], [new { kind = "PARTY" }]);
        var party = Guid.CreateVersion7();
        var raised = await host.RaiseAsync(party);
        var email = (await host.MessagesAsync(raised.SubjectId)).Items.Single();

        host.Email.Failing = true;
        await Should.ThrowAsync<MessageDeliveryException>(() => host.SendAsync(email.Id));
        var failed = (await host.MessagesAsync(raised.SubjectId)).Items.Single();
        failed.Status.ShouldBe(MessageStatus.Failed);
        failed.Attempts.ShouldBe(1);
        failed.Error.ShouldBe("Server down.");

        host.Email.Failing = false;
        var resent = await host.Host.AsAsync("admin", sp => sp.GetRequiredService<CommunicationService>().ResendAsync(email.Id, CancellationToken.None));
        resent.Message.Status.ShouldBe(MessageStatus.Pending);
        await Should.ThrowAsync<CommunicationConflictException>(() => host.Host.AsAsync("admin", sp => sp.GetRequiredService<CommunicationService>().ResendAsync(email.Id, CancellationToken.None)));
        await host.SendAsync(email.Id);
        var done = (await host.MessagesAsync(raised.SubjectId)).Items.Single();
        done.Status.ShouldBe(MessageStatus.Sent);
        done.Attempts.ShouldBe(2);

        // A party without an email address: recorded as skipped.
        host.Parties.WithoutContacts.Add(party);
        var second = await host.RaiseAsync(party);
        (await host.MessagesAsync(second.SubjectId)).Items.Single().Error.ShouldBe("The recipient has no email address.");
    }

    [Fact]
    public async Task People_read_their_own_notifications()
    {
        await using var host = new CommunicationHost(fixture);
        var template = await host.TemplateAsync(email: false, sms: false);
        await host.RuleAsync(template, ["IN_APP"], [new { kind = "PARTY" }]);
        var (mine, theirs) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var first = await host.RaiseAsync(mine);
        host.Clock.Now = host.Clock.Now.AddMinutes(5);
        await host.RaiseAsync(mine);
        await host.RaiseAsync(theirs);

        Task<NotificationPage> ListAsync(bool unread = false) =>
            host.Host.AsAsync("applicant", sp => sp.GetRequiredService<CommunicationService>().NotificationsAsync(new NotificationOwner(mine, null), unread, 20, CancellationToken.None));
        var page = await ListAsync();
        page.Unread.ShouldBe(2);
        page.Items.Count.ShouldBe(2);
        page.Items[^1].SubjectId.ShouldBe(first.SubjectId);
        page.Items[^1].Link.ShouldBe($"/requests/{first.SubjectId}");
        page.Items[^1].BodyEn.ShouldBe($"Request {first.SubjectNumber}, grade 3");

        await host.Host.AsAsync("applicant", sp => sp.GetRequiredService<CommunicationService>().ReadAsync(new NotificationOwner(mine, null), page.Items[0].Id, CancellationToken.None));
        (await ListAsync(unread: true)).Items.ShouldHaveSingleItem().Id.ShouldBe(page.Items[1].Id);

        // Another party's notification cannot be marked from here.
        var others = await host.Host.AsAsync("x", sp => sp.GetRequiredService<CommunicationService>().NotificationsAsync(new NotificationOwner(theirs, null), false, 20, CancellationToken.None));
        await host.Host.AsAsync("applicant", sp => sp.GetRequiredService<CommunicationService>().ReadAsync(new NotificationOwner(mine, null), others.Items[0].Id, CancellationToken.None));
        (await host.Host.AsAsync("x", sp => sp.GetRequiredService<CommunicationService>().UnreadAsync(new NotificationOwner(theirs, null), CancellationToken.None))).ShouldBe(1);

        await host.Host.AsAsync("applicant", sp => sp.GetRequiredService<CommunicationService>().ReadAsync(new NotificationOwner(mine, null), null, CancellationToken.None));
        (await ListAsync()).Unread.ShouldBe(0);
    }

    [Fact]
    public async Task Templates_are_previewed_on_an_event_sample()
    {
        await using var host = new CommunicationHost(fixture);
        var events = await host.Host.AsAsync("admin", sp => Task.FromResult(sp.GetRequiredService<CommunicationService>().Events()));
        events.Select(e => e.Code).ShouldContain("REQUEST_RETURNED");
        events.Select(e => e.Code).ShouldContain("DOCUMENT_EXPIRING");
        events.Select(e => e.Code).ShouldContain("TASK_SLA_BREACHED");
        events.Single(e => e.Code == "DOCUMENT_EXPIRING").Sample["party"]!["nameAr"]!.GetValue<string>().ShouldBe("مؤسسة المثال");

        var preview = await host.Host.AsAsync("admin", sp => Task.FromResult(sp.GetRequiredService<CommunicationService>().Preview(new TemplatePreviewRequest(
            "DOCUMENT_EXPIRING",
            new MessageTemplatePayload { SmsAr = "شهادتكم {{ document.number }} تنتهي خلال {{ document.daysLeft }} يوماً", SmsEn = "{{ document.number }} expires in {{ document.daysLeft }} days" }))));
        preview.ShouldHaveSingleItem().BodyAr.ShouldBe("شهادتكم CC-2026-000001 تنتهي خلال 90 يوماً");
    }
}
