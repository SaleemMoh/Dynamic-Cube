using DynamicCube.Modules.AppsControl.Changes;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Modules.Services.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Services.Tests;

/// <summary>The portal offers the services of no app and of apps that tick the portal channel (ADR-0036).</summary>
public sealed class PortalChannelTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Services_of_an_app_show_on_the_portal_only_when_the_app_ticks_it()
    {
        await using var host = new TestHost(fixture.RequireConnectionString());
        var app = TestHost.Unique("APP");
        await host.PublishAsync(AppsControlAreas.System, App(app, ChangeOperation.Create, "INTERNAL"));
        Task<bool> OfferedAsync(Service service) => host.AsAsync("applicant", sp => sp.GetRequiredService<PortalChannel>().OffersAsync(service, CancellationToken.None));

        (await OfferedAsync(new Service { Code = "S1", BusinessApp = app })).ShouldBeFalse("the app is on the internal system only");
        (await OfferedAsync(new Service { Code = "S2" })).ShouldBeTrue("a service of no app is offered");
        (await OfferedAsync(new Service { Code = "S3", BusinessApp = TestHost.Unique("OTHER"), SharedApps = [app] })).ShouldBeFalse();

        await host.PublishAsync(AppsControlAreas.System, App(app, ChangeOperation.Update, "INTERNAL", "EXTERNAL"));
        (await OfferedAsync(new Service { Code = "S1", BusinessApp = app })).ShouldBeTrue();
        (await OfferedAsync(new Service { Code = "S3", BusinessApp = TestHost.Unique("OTHER"), SharedApps = [app] })).ShouldBeTrue("a shared app on the portal is enough");
    }

    private static Change App(string code, ChangeOperation operation, params string[] channels) =>
        new(AppsControlChangeTypes.BusinessApp, code, operation, new { nameAr = "تطبيق", nameEn = "App", kind = "DYNAMIC_CUBE", channels });
}
