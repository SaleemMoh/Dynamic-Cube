using System.Net;
using System.Xml.Linq;
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

/// <summary>
/// SOAP operations (ADR-0044): the envelope built from the inputs, WS-Security UsernameToken, the XML response read as
/// JSON for outputs and code maps, faults as outcomes, and the call log masked.
/// </summary>
public sealed class SoapTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private const string Ns = "http://yakeen.test/person";
    private static readonly XNamespace Env11 = SoapXml.Soap11;
    private static readonly XNamespace Env12 = SoapXml.Soap12;
    private static readonly XNamespace Wsse = SoapXml.Wsse;
    private static readonly XNamespace Wsu = SoapXml.Wsu;
    private TestHost _host = null!;

    public ValueTask InitializeAsync()
    {
        _host = new TestHost(fixture.RequireConnectionString());
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    private const string PersonResponse = """
        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
          <soap:Body>
            <p:GetPersonInfoResponse xmlns:p="http://yakeen.test/person">
              <p:return>
                <p:firstName>محمد</p:firstName>
                <p:familyName>العتيبي</p:familyName>
                <p:idExpiryDate>1450-05-01</p:idExpiryDate>
                <p:middleName xsi:nil="true"/>
                <p:nationality code="SA">سعودي</p:nationality>
                <p:addresses><p:address><p:city>الرياض</p:city></p:address></p:addresses>
              </p:return>
            </p:GetPersonInfoResponse>
          </soap:Body>
        </soap:Envelope>
        """;

    private async Task<string> PublishAsync(AuthType auth, bool digest, Action<OperationPayload>? change = null, int retryCount = 2)
    {
        var provider = TestHost.Unique("ELM");
        var connection = TestHost.Unique("YAKEEN");
        var code = TestHost.Unique("YAKEEN_PERSON");
        var operation = new OperationPayload
        {
            NameAr = "بيانات المواطن",
            NameEn = "Citizen information",
            ConnectionCode = connection,
            Protocol = OperationProtocol.Soap,
            Soap = new SoapSettings { Version = "1.1", Action = "urn:GetPersonInfo", OperationName = "GetPersonInfo", Namespace = Ns },
            PathTemplate = "/soap/yakeen",
            Parameters =
            [
                new() { Name = "idNumber", Location = ParameterLocation.Body, Target = "request.nin", IsMasked = true },
                new() { Name = "birthDate", Location = ParameterLocation.Body, Target = "request.dateOfBirth" },
                new() { Name = "chargeCode", Location = ParameterLocation.Body, IsRequired = false },
            ],
            Outputs =
            [
                new() { Name = "firstName", Path = "return.firstName" },
                new() { Name = "nationality", Path = "return.nationality.$text" },
                new() { Name = "nationalityCode", Path = "return.nationality.code" },
                new() { Name = "cities", Path = "return.addresses.address[*].city" },
                new() { Name = "middleName", Path = "return.middleName" },
            ],
            MaskFields = ["familyName"],
            IsIdempotent = true,
        };
        change?.Invoke(operation);
        await _host.PublishAsync(
            IntegrationChangeTypes.Area,
            new Change(IntegrationChangeTypes.Provider, provider, ChangeOperation.Create, new ProviderPayload { NameAr = "علم", NameEn = "Elm" }),
            new Change(IntegrationChangeTypes.Connection, connection, ChangeOperation.Create, new ConnectionPayload
            {
                NameAr = "يقين",
                NameEn = "Yakeen",
                ProviderCode = provider,
                BaseUrl = "https://yakeen.test",
                AuthType = auth,
                SecretName = auth == AuthType.None ? null : TestHost.SecretName,
                PasswordDigest = digest,
                RetryCount = retryCount,
                TimeoutSeconds = 5,
            }),
            new Change(IntegrationChangeTypes.Operation, code, ChangeOperation.Create, operation));
        return code;
    }

    private Task<IntegrationResult> InvokeAsync(string operation, Dictionary<string, string?>? inputs = null) =>
        _host.AsAsync("admin", sp => sp.GetRequiredService<IIntegrationGateway>().InvokeAsync(
            operation,
            inputs ?? new Dictionary<string, string?> { ["idNumber"] = "1000000001", ["birthDate"] = "1410-01-15" },
            CancellationToken.None));

    [Fact]
    public async Task Builds_the_envelope_with_a_username_token_and_reads_the_xml_response_as_json()
    {
        var operation = await PublishAsync(AuthType.WsSecurity, digest: false);
        _host.Provider.Respond = _ => (HttpStatusCode.OK, PersonResponse);

        var result = await InvokeAsync(operation);

        result.Outcome.ShouldBe(IntegrationOutcome.Success);
        result.Outputs["firstName"]!.GetValue<string>().ShouldBe("محمد");
        result.Outputs["nationality"]!.GetValue<string>().ShouldBe("سعودي");
        result.Outputs["nationalityCode"]!.GetValue<string>().ShouldBe("SA");
        result.Outputs["cities"]!.AsArray().Select(c => c!.GetValue<string>()).ShouldBe(["الرياض"], "one repeated element still reads as a list");
        result.Outputs["middleName"].ShouldBeNull();

        var call = _host.Provider.Requests.Single();
        call.Method.ShouldBe(HttpMethod.Post);
        call.Url.ToString().ShouldBe("https://yakeen.test/soap/yakeen");
        call.Header("SOAPAction").ShouldBe("\"urn:GetPersonInfo\"");
        call.Header("Content-Type").ShouldStartWith("text/xml");
        call.Authorization.ShouldBeNull();

        var envelope = XDocument.Parse(call.Body!).Root!;
        envelope.Name.ShouldBe(Env11 + "Envelope");
        var body = envelope.Element(Env11 + "Body")!.Elements().Single();
        body.Name.ShouldBe(XName.Get("GetPersonInfo", Ns));
        var request = body.Element(XName.Get("request", Ns))!;
        request.Elements().Select(e => e.Name.LocalName).ShouldBe(["nin", "dateOfBirth"], "the inputs' order, nested under one parent");
        request.Element(XName.Get("nin", Ns))!.Value.ShouldBe("1000000001");
        body.Element(XName.Get("chargeCode", Ns)).ShouldBeNull("an input not given is left out");

        var token = envelope.Element(Env11 + "Header")!.Element(Wsse + "Security")!.Element(Wsse + "UsernameToken")!;
        token.Element(Wsse + "Username")!.Value.ShouldBe("soap-user");
        token.Element(Wsse + "Password")!.Value.ShouldBe("soap-pass");
        token.Element(Wsse + "Password")!.Attribute("Type")!.Value.ShouldBe(SoapXml.PasswordTextType);
        token.Element(Wsse + "Nonce").ShouldNotBeNull();
        token.Element(Wsu + "Created").ShouldNotBeNull();

        var log = await _host.AsAsync("admin", sp => sp.GetRequiredService<IntegrationDbContext>().CallLogs.SingleAsync(l => l.Id == result.CallId));
        log.Method.ShouldBe("POST");
        log.RequestBody.ShouldNotBeNull();
        log.RequestBody.ShouldNotContain("1000000001");
        log.RequestBody.ShouldNotContain("soap-pass", Case.Sensitive, "the credentials never reach the log");
        log.RequestBody.ShouldNotContain("UsernameToken");
        log.ResponseBody.ShouldNotBeNull();
        log.ResponseBody.ShouldNotContain("العتيبي");
        log.ResponseBody.ShouldContain("محمد");
    }

    [Fact]
    public async Task Soap_12_sends_the_action_in_the_content_type_and_a_password_digest()
    {
        var operation = await PublishAsync(AuthType.WsSecurity, digest: true, p => p.Soap!.Version = "1.2");
        _host.Provider.Respond = _ => (HttpStatusCode.OK, PersonResponse.Replace(SoapXml.Soap11, SoapXml.Soap12, StringComparison.Ordinal));

        (await InvokeAsync(operation)).IsSuccess.ShouldBeTrue();

        var call = _host.Provider.Requests.Single();
        call.Header("Content-Type").ShouldBe("application/soap+xml; charset=utf-8; action=\"urn:GetPersonInfo\"");
        call.Header("SOAPAction").ShouldBeNull();
        var token = XDocument.Parse(call.Body!).Root!.Element(Env12 + "Header")!.Element(Wsse + "Security")!.Element(Wsse + "UsernameToken")!;
        var password = token.Element(Wsse + "Password")!;
        password.Attribute("Type")!.Value.ShouldBe(SoapXml.PasswordDigestType);
        var nonce = Convert.FromBase64String(token.Element(Wsse + "Nonce")!.Value);
        password.Value.ShouldBe(SoapXml.Digest(nonce, token.Element(Wsu + "Created")!.Value, "soap-pass"));
        password.Value.ShouldNotBe("soap-pass");
    }

    [Fact]
    public async Task Faults_become_outcomes_with_the_code_map_and_server_faults_are_not_repeated()
    {
        var operation = await PublishAsync(AuthType.Basic, digest: false, p => p.ResponseCodes =
        [
            new() { Code = "PersonNotFound", Outcome = IntegrationOutcome.NotFound, MessageAr = "لا يوجد سجل لهذا الرقم.", MessageEn = "No record for this number." },
        ]);

        _host.Provider.Respond = _ => (HttpStatusCode.InternalServerError, """
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><soap:Fault>
              <faultcode>soap:Client</faultcode><faultstring>The date of birth does not match.</faultstring>
            </soap:Fault></soap:Body></soap:Envelope>
            """);
        var mismatch = await InvokeAsync(operation);
        mismatch.Outcome.ShouldBe(IntegrationOutcome.BusinessError);
        mismatch.Code.ShouldBe("Client");
        mismatch.MessageEn.ShouldBe("The date of birth does not match.");
        _host.Provider.Requests.Count.ShouldBe(1, "a fault is an answer, not a reason to call again");
        _host.Provider.Requests[0].Authorization.ShouldBe("Basic " + Convert.ToBase64String("soap-user:soap-pass"u8.ToArray()));

        _host.Provider.Respond = _ => (HttpStatusCode.InternalServerError, """
            <env:Envelope xmlns:env="http://www.w3.org/2003/05/soap-envelope" xmlns:y="http://yakeen.test/faults"><env:Body><env:Fault>
              <env:Code><env:Value>env:Sender</env:Value><env:Subcode><env:Value>y:PersonNotFound</env:Value></env:Subcode></env:Code>
              <env:Reason><env:Text xml:lang="en">No person</env:Text></env:Reason>
            </env:Fault></env:Body></env:Envelope>
            """);
        var missing = await InvokeAsync(operation);
        missing.Outcome.ShouldBe(IntegrationOutcome.NotFound);
        missing.Code.ShouldBe("PersonNotFound");
        missing.MessageAr.ShouldBe("لا يوجد سجل لهذا الرقم.");

        _host.Provider.Reset();
        _host.Provider.Respond = _ => (HttpStatusCode.InternalServerError, """
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><soap:Fault>
              <faultcode>soap:Server</faultcode><faultstring>Database down</faultstring>
            </soap:Fault></soap:Body></soap:Envelope>
            """);
        var down = await InvokeAsync(operation);
        down.Outcome.ShouldBe(IntegrationOutcome.TechnicalError);
        down.MessageEn.ShouldBe("The external service is not available right now. Please try again later.");
        _host.Provider.Requests.Count.ShouldBe(1);

        _host.Provider.Respond = _ => (HttpStatusCode.OK, "<html>maintenance</html>");
        (await InvokeAsync(operation)).Outcome.ShouldBe(IntegrationOutcome.TechnicalError, "a page that is not a SOAP envelope");
    }

    [Fact]
    public async Task Checks_the_soap_call_and_keeps_ws_security_to_soap_operations()
    {
        var errors = (await Should.ThrowAsync<ChangeValidationException>(() => PublishAsync(AuthType.None, digest: false, p =>
        {
            p.Soap = new SoapSettings { Version = "2.0", OperationName = "Get Person", Namespace = "not a uri", Action = "a\"b" };
            p.Parameters.Add(new() { Name = "bad", Location = ParameterLocation.Body, Target = "request.$x" });
        }))).Errors.Keys.ToList();
        errors.ShouldContain(k => k.EndsWith("soap.version", StringComparison.Ordinal));
        errors.ShouldContain(k => k.EndsWith("soap.operationName", StringComparison.Ordinal));
        errors.ShouldContain(k => k.EndsWith("soap.namespace", StringComparison.Ordinal));
        errors.ShouldContain(k => k.EndsWith("soap.action", StringComparison.Ordinal));
        errors.ShouldContain(k => k.EndsWith("parameters[3].target", StringComparison.Ordinal));

        var rest = await PublishAsync(AuthType.WsSecurity, digest: false, p =>
        {
            p.Protocol = OperationProtocol.Rest;
            p.Soap = null;
            p.Method = "POST";
            p.Parameters = [new() { Name = "idNumber", Location = ParameterLocation.Body }];
        });
        var refused = await InvokeAsync(rest, new() { ["idNumber"] = "1" });
        refused.Outcome.ShouldBe(IntegrationOutcome.TechnicalError);
        refused.Code.ShouldBe("AUTH");
        _host.Provider.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void Reads_xml_as_json_with_attributes_text_nil_and_repeated_elements()
    {
        var read = SoapXml.Read("""
            <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"><s:Body>
              <r:Resp xmlns:r="urn:r"><r:item id="1">a</r:item><r:item id="2">b</r:item><r:empty/><r:none xsi:nil="true"/><r:n>7</r:n></r:Resp>
            </s:Body></s:Envelope>
            """)!;

        read.Fault.ShouldBeNull();
        read.Data!.ToJsonString().ShouldBe("""{"item":[{"id":"1","$text":"a"},{"id":"2","$text":"b"}],"empty":null,"none":null,"n":"7"}""");
        SoapXml.Read("{\"not\":\"xml\"}").ShouldBeNull();
        SoapXml.Read("<!DOCTYPE x [<!ENTITY e SYSTEM \"file:///etc/passwd\">]><x>&e;</x>").ShouldBeNull("no DTDs");
        SoapXml.Read("<x>not soap</x>").ShouldBeNull();
    }
}
