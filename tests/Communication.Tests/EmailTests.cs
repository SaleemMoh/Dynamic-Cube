using System.Net.Http.Json;
using System.Text.Json;
using DynamicCube.Modules.Communication.Domain;
using DynamicCube.Modules.Communication.Runtime;
using DynamicCube.Platform.Security;
using Microsoft.Extensions.Options;
using Shouldly;

namespace DynamicCube.Modules.Communication.Tests;

public sealed class EmailTests
{
    [Fact]
    public async Task Emails_reach_a_real_smtp_server_when_one_is_available()
    {
        var mailpit = Environment.GetEnvironmentVariable("DYNAMICCUBE_TEST_MAILPIT");
        Assert.SkipWhen(string.IsNullOrEmpty(mailpit), "Set DYNAMICCUBE_TEST_MAILPIT (e.g. http://localhost:8025, SMTP on port 1025 of the same host) to send through a real server.");
        var ct = TestContext.Current.CancellationToken;
        var api = new Uri(mailpit!);
        var options = Options.Create(new CommunicationOptions { Email = new EmailOptions { Host = api.Host, Port = 1025, Security = "None", From = "no-reply@dynamic-cube.local", FromName = "Dynamic Cube" } });
        var channel = new EmailChannel(options, new NoSecrets());
        var subject = "اعتماد الطلب " + Guid.NewGuid().ToString("N")[..8];

        await channel.SendAsync(new OutboundMessage { Address = "applicant@example.com", RecipientName = "مؤسسة المثال", Title = subject, Body = "<p dir=\"rtl\">تم اعتماد طلبكم</p>" }, ct);

        using var http = new HttpClient { BaseAddress = api };
        var found = await http.GetFromJsonAsync<JsonElement>($"/api/v1/search?query={Uri.EscapeDataString("subject:\"" + subject + "\"")}", ct);
        found.GetProperty("messages_count").GetInt32().ShouldBe(1);
        found.GetProperty("messages")[0].GetProperty("To")[0].GetProperty("Address").GetString().ShouldBe("applicant@example.com");

        var unreachable = new EmailChannel(Options.Create(new CommunicationOptions { Email = new EmailOptions { Host = api.Host, Port = 1, Security = "None" } }), new NoSecrets());
        await Should.ThrowAsync<MessageDeliveryException>(() => unreachable.SendAsync(new OutboundMessage { Address = "a@example.com", Body = "x" }, ct));
    }

    private sealed class NoSecrets : ISecretStore
    {
        public string? Find(string name, string key) => null;
    }
}
