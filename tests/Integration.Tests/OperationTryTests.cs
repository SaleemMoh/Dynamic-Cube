using System.Net;
using DynamicCube.Modules.Integration.Changes;
using DynamicCube.Modules.Integration.Domain;
using DynamicCube.Modules.Integration.Persistence;
using DynamicCube.Modules.Integration.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Integration;
using DynamicCube.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Integration.Tests;

/// <summary>The operation designer's "Try it": an operation that is not saved yet runs on its published connection (ADR-0041).</summary>
public sealed class OperationTryTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private TestHost _host = null!;
    private string _connection = null!;
    private string _envelope = null!;

    public ValueTask InitializeAsync()
    {
        _host = new TestHost(fixture.RequireConnectionString());
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    private async Task PublishAsync() =>
        await MomrahSamples.PublishAsync(_host, operation: (connection, envelope) =>
        {
            _connection = connection;
            _envelope = envelope;
            return MomrahSamples.CrByIdNumber(connection, envelope);
        });

    private Task<(IntegrationResult Result, IntegrationTrace Trace)> TryAsync(OperationPayload payload, Dictionary<string, string?> inputs) =>
        _host.AsAsync("admin", sp => sp.GetRequiredService<IntegrationGateway>().TryAsync(payload, inputs, CancellationToken.None));

    [Fact]
    public async Task An_unsaved_operation_runs_on_its_published_connection_and_envelope()
    {
        await PublishAsync();
        _host.Provider.Respond = r => r.Url.ToString() == "https://gateway.test/oauth/v1/token" ? (HttpStatusCode.OK, MomrahSamples.Token("t")) : (HttpStatusCode.OK, MomrahSamples.CrInformation);

        // Changed in the designer, not saved: another path and a new output.
        var draft = MomrahSamples.CrByIdNumber(_connection, _envelope, path: "/v2/cr/{idNumber}");
        draft.Code = TestHost.Unique("NEW_OP");
        draft.Outputs.Add(new OperationOutput { Name = "names", Path = "Licenses[*].Commercial_Name" });
        draft.CacheSeconds = 3600;
        var (result, trace) = await TryAsync(draft, new() { ["idNumber"] = "1003721519" });

        result.Outcome.ShouldBe(IntegrationOutcome.Success, "the published envelope read the response");
        result.Outputs["names"]!.AsArray().Count.ShouldBe(2);
        trace.Url.ShouldBe("https://gateway.test/v2/cr/***", "masked as the definition says");
        _host.Provider.Requests[^1].Url.AbsolutePath.ShouldBe("/v2/cr/1003721519");

        // Not cached even though the draft caches: a second try calls the provider again.
        await TryAsync(draft, new() { ["idNumber"] = "1003721519" });
        _host.Provider.CountTo("/v2/cr/").ShouldBe(2);
        var logged = await _host.AsAsync("admin", sp => sp.GetRequiredService<IntegrationDbContext>().CallLogs.CountAsync(l => l.OperationCode == draft.Code));
        logged.ShouldBe(2, "tries are written to the call log like other calls");
        (await _host.AsAsync("admin", sp => sp.GetRequiredService<IntegrationDbContext>().Operations.AnyAsync(o => o.Code == draft.Code))).ShouldBeFalse("nothing is saved");
    }

    [Fact]
    public async Task An_unsaved_operation_with_problems_reports_them_as_saving_would()
    {
        await PublishAsync();
        var draft = MomrahSamples.CrByIdNumber("NO_SUCH_CONNECTION", _envelope, path: "/v1/cr/{crNumber}");
        draft.Outputs.Add(new OperationOutput { Name = "bad name", Path = "a..b" });

        var errors = (await Should.ThrowAsync<ChangeValidationException>(() => TryAsync(draft, new() { ["idNumber"] = "1" }))).Errors.Keys;
        errors.ShouldContain("connectionCode");
        errors.ShouldContain("pathTemplate", "the placeholder has no path parameter");
        errors.ShouldContain("parameters[0].name", "idNumber is not in the path");
        errors.ShouldContain("outputs[2].name");
        errors.ShouldContain("outputs[2].path");
        _host.Provider.Requests.ShouldBeEmpty();
    }
}
