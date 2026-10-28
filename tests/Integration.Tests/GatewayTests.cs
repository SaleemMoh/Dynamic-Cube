using System.Net;
using DynamicCube.Modules.Integration.Changes;
using DynamicCube.Modules.Integration.Domain;
using DynamicCube.Modules.Integration.Persistence;
using DynamicCube.Modules.Integration.Runtime;
using DynamicCube.Modules.Integration.Tests.Infrastructure;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DynamicCube.Modules.Integration.Tests;

public sealed class GatewayTests(IntegrationFixture fixture) : IAsyncLifetime
{
    private TestHost _host = null!;

    public ValueTask InitializeAsync()
    {
        _host = new TestHost(fixture.RequireConnectionString());
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    private Task<IntegrationResult> InvokeAsync(string operation, string idNumber = "1003721519") =>
        _host.AsAsync("admin", sp => sp.GetRequiredService<IIntegrationGateway>().InvokeAsync(operation, new Dictionary<string, string?> { ["idNumber"] = idNumber }, CancellationToken.None));

    private void Answer(Func<RecordedRequest, (HttpStatusCode, string)> api) =>
        _host.Provider.Respond = r => r.Url.AbsolutePath == "/oauth/v1/token" ? (HttpStatusCode.OK, MomrahSamples.Token("token-1")) : api(r);

    [Fact]
    public async Task Reads_the_envelope_maps_outputs_and_reuses_the_token()
    {
        var operation = await MomrahSamples.PublishAsync(_host);
        Answer(_ => (HttpStatusCode.OK, MomrahSamples.CrInformation));

        var first = await InvokeAsync(operation);
        var second = await InvokeAsync(operation);

        first.Outcome.ShouldBe(IntegrationOutcome.Success);
        first.Code.ShouldBe("1");
        first.Outputs["crNumbers"]!.ToJsonString().ShouldBe("""["3350043331","1010012006"]""");
        first.Outputs["firstName"]!.GetValue<string>().ShouldBe("مؤسسة مشعل للنظارات");
        second.IsSuccess.ShouldBeTrue();
        _host.Provider.CountTo("/oauth/v1/token").ShouldBe(1);
        var call = _host.Provider.Requests[^1];
        call.Url.ToString().ShouldBe("https://gateway.test/v1/MCI-DIP/cr-information-id-number/1003721519");
        call.Authorization.ShouldBe("Bearer token-1");
        _host.Provider.Requests[0].Authorization.ShouldBe("Basic " + Convert.ToBase64String("consumer-key:consumer-secret"u8.ToArray()));
    }

    [Fact]
    public async Task Business_codes_map_to_outcomes_and_bilingual_messages()
    {
        var operation = await MomrahSamples.PublishAsync(_host);

        Answer(_ => (HttpStatusCode.OK, MomrahSamples.RecordNotFound));
        var notFound = await InvokeAsync(operation);
        Answer(_ => (HttpStatusCode.OK, MomrahSamples.WrongIdType));
        var wrongType = await InvokeAsync(operation);
        Answer(_ => (HttpStatusCode.OK, """{ "statusDetails": [{ "code": 200 }], "data": { "responseCode": "001-000004", "responseMessage": "At least one CR is Suspended" } }"""));
        var unmapped = await InvokeAsync(operation);

        notFound.Outcome.ShouldBe(IntegrationOutcome.NotFound);
        notFound.MessageAr.ShouldBe("لا يوجد سجل مطابق.");
        wrongType.Outcome.ShouldBe(IntegrationOutcome.BusinessError);
        wrongType.MessageEn.ShouldBe("The ID type is not valid.");
        unmapped.Outcome.ShouldBe(IntegrationOutcome.BusinessError);
        unmapped.Code.ShouldBe("001-000004");
        unmapped.MessageEn.ShouldBe("At least one CR is Suspended");
    }

    [Fact]
    public async Task Gets_a_new_token_once_when_the_provider_rejects_it()
    {
        var operation = await MomrahSamples.PublishAsync(_host);
        var rejected = false;
        Answer(_ =>
        {
            if (!rejected)
            {
                rejected = true;
                return (HttpStatusCode.Unauthorized, MomrahSamples.TokenExpired);
            }

            return (HttpStatusCode.OK, MomrahSamples.CrInformation);
        });

        var result = await InvokeAsync(operation);

        result.IsSuccess.ShouldBeTrue();
        _host.Provider.CountTo("/oauth/v1/token").ShouldBe(2);
    }

    [Fact]
    public async Task Retries_idempotent_calls_after_server_errors()
    {
        var operation = await MomrahSamples.PublishAsync(_host, retryCount: 2);
        var calls = 0;
        Answer(_ => ++calls < 3 ? (HttpStatusCode.ServiceUnavailable, "<html>busy</html>") : (HttpStatusCode.OK, MomrahSamples.CrInformation));

        var result = await InvokeAsync(operation);

        result.IsSuccess.ShouldBeTrue();
        calls.ShouldBe(3);
        var log = await _host.AsAsync("admin", sp => sp.GetRequiredService<IntegrationDbContext>().CallLogs.SingleAsync(l => l.Id == result.CallId));
        log.Attempts.ShouldBe(3);
    }

    [Fact]
    public async Task Does_not_repeat_non_idempotent_calls()
    {
        var operation = await MomrahSamples.PublishAsync(_host, retryCount: 2, operation: (c, e) =>
        {
            var payload = MomrahSamples.CrByIdNumber(c, e);
            payload.Method = "POST";
            payload.PathTemplate = "/v1/inspections";
            payload.Parameters = [new() { Name = "idNumber", Location = ParameterLocation.Body, Target = "request.idNumber" }];
            return payload;
        });
        var calls = 0;
        Answer(_ =>
        {
            calls++;
            return (HttpStatusCode.BadGateway, "bad gateway");
        });

        var result = await InvokeAsync(operation);

        result.Outcome.ShouldBe(IntegrationOutcome.TechnicalError);
        result.MessageEn.ShouldBe("The external service is not available right now. Please try again later.");
        calls.ShouldBe(1);
        _host.Provider.Requests[^1].Body.ShouldBe("""{"request":{"idNumber":"1003721519"}}""");
    }

    [Fact]
    public async Task Opens_the_circuit_after_repeated_technical_failures()
    {
        var operation = await MomrahSamples.PublishAsync(_host, retryCount: 0, breakerFailures: 2);
        Answer(_ => (HttpStatusCode.InternalServerError, "{}"));

        await InvokeAsync(operation);
        await InvokeAsync(operation);
        var before = _host.Provider.Requests.Count;
        var third = await InvokeAsync(operation);

        third.Outcome.ShouldBe(IntegrationOutcome.TechnicalError);
        third.Code.ShouldBe("CIRCUIT_OPEN");
        _host.Provider.Requests.Count.ShouldBe(before);
    }

    [Fact]
    public async Task Caches_results_when_the_operation_allows_it()
    {
        var operation = await MomrahSamples.PublishAsync(_host, cacheSeconds: 300);
        Answer(_ => (HttpStatusCode.OK, MomrahSamples.CrInformation));

        await InvokeAsync(operation);
        var second = await InvokeAsync(operation);
        await InvokeAsync(operation, idNumber: "1000000002");

        second.FromCache.ShouldBeTrue();
        _host.Provider.CountTo("/cr-information-id-number/").ShouldBe(2);
    }

    [Fact]
    public async Task Call_log_masks_personal_data()
    {
        var operation = await MomrahSamples.PublishAsync(_host);
        Answer(_ => (HttpStatusCode.OK, MomrahSamples.CrInformation));

        var result = await InvokeAsync(operation);

        var log = await _host.AsAsync("admin", sp => sp.GetRequiredService<IntegrationDbContext>().CallLogs.SingleAsync(l => l.Id == result.CallId));
        log.Url.ShouldBe("https://gateway.test/v1/MCI-DIP/cr-information-id-number/***");
        log.ResponseBody.ShouldNotBeNull();
        log.ResponseBody.ShouldNotContain("مؤسسة مشعل");
        log.ResponseBody.ShouldContain("3350043331");
        log.UserId.ShouldBe("sub-admin");
        log.Outcome.ShouldBe(IntegrationOutcome.Success);
    }

    [Fact]
    public async Task Rejects_missing_inputs_and_unknown_operations()
    {
        var operation = await MomrahSamples.PublishAsync(_host);

        await Should.ThrowAsync<IntegrationInputException>(() => _host.AsAsync("admin", sp =>
            sp.GetRequiredService<IIntegrationGateway>().InvokeAsync(operation, new Dictionary<string, string?>(), CancellationToken.None)));
        await Should.ThrowAsync<IntegrationOperationNotFoundException>(() => InvokeAsync("NO_SUCH_OPERATION"));
    }

    [Fact]
    public async Task Test_console_returns_the_raw_response()
    {
        var operation = await MomrahSamples.PublishAsync(_host);
        Answer(_ => (HttpStatusCode.OK, MomrahSamples.CrInformation));

        var (result, trace) = await _host.AsAsync("admin", sp => sp.GetRequiredService<IntegrationGateway>().TestAsync(operation, new Dictionary<string, string?> { ["idNumber"] = "1003721519" }, CancellationToken.None));

        result.IsSuccess.ShouldBeTrue();
        trace.HttpStatus.ShouldBe(200);
        trace.ResponseBody.ShouldBe(MomrahSamples.CrInformation);
        trace.Url.ShouldEndWith("/***");
    }

    [Fact]
    public async Task Validation_rejects_inconsistent_operations_and_credentials_in_headers()
    {
        var errors = await Should.ThrowAsync<ChangeValidationException>(() => _host.PublishAsync(
            IntegrationChangeTypes.Area,
            new Change(IntegrationChangeTypes.Connection, TestHost.Unique("BAD"), ChangeOperation.Create, new ConnectionPayload
            {
                NameAr = "س",
                NameEn = "x",
                ProviderCode = "NO_SUCH_PROVIDER",
                BaseUrl = "ftp://gateway",
                AuthType = AuthType.ApiKey,
                Headers = [new() { Name = "Authorization", Value = "Bearer secret" }],
            })));

        errors.Errors.Keys.ShouldBe(["providerCode", "baseUrl", "secretName", "headers[0].name"], ignoreOrder: true);

        var operationErrors = await Should.ThrowAsync<ChangeValidationException>(() => MomrahSamples.PublishAsync(_host, operation: (c, e) =>
        {
            var payload = MomrahSamples.CrByIdNumber(c, e);
            payload.PathTemplate = "/v1/crs/{crNumber}";
            payload.Outputs = [new() { Name = "x", Path = "a..b" }];
            return payload;
        }));

        operationErrors.Errors.Keys.ShouldBe(["parameters[0].name", "pathTemplate", "outputs[0].path"], ignoreOrder: true);
    }
}
