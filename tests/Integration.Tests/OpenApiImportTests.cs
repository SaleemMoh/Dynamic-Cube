using DynamicCube.Modules.Integration.Domain;
using DynamicCube.Modules.Integration.Runtime;
using Shouldly;

namespace DynamicCube.Modules.Integration.Tests;

/// <summary>Reading OpenAPI and Swagger descriptions into a provider, a connection and operations to review (ADR-0041).</summary>
public sealed class OpenApiImportTests
{
    internal const string CommerceYaml = """
        openapi: 3.0.1
        info: { title: Ministry of Commerce, version: '2.1', description: Commercial registrations }
        servers: [ { url: 'https://api.commerce.example/v2/' } ]
        components:
          securitySchemes:
            basic: { type: http, scheme: basic }
            gateway: { type: oauth2, flows: { clientCredentials: { tokenUrl: /oauth/v1/token, scopes: {} } } }
          schemas:
            Owner: { type: object, properties: { name: { type: string }, nationalId: { type: string } } }
            Registration:
              type: object
              properties:
                crNumber: { type: string, example: '1010000001' }
                status: { type: string, enum: [ACTIVE, SUSPENDED] }
                capital: { type: number }
                owners: { type: array, items: { $ref: '#/components/schemas/Owner' } }
                activities: { type: array, items: { type: string } }
        paths:
          /registrations/{cr.number}:
            parameters:
              - { name: cr.number, in: path, required: true, schema: { type: string }, example: '1010000001' }
            get:
              operationId: getRegistrationByNumber
              summary: Registration by CR number
              tags: [Registrations]
              parameters:
                - { name: lang, in: query, schema: { type: string, default: ar } }
                - { name: Authorization, in: header, schema: { type: string } }
                - { name: X-Channel, in: header, required: true, schema: { type: string } }
              responses:
                '200': { description: ok, content: { application/json: { schema: { $ref: '#/components/schemas/Registration' } } } }
                '404': { description: Not found }
          /owners/verify:
            post:
              operationId: verifyOwner
              requestBody:
                content:
                  application/json:
                    schema:
                      type: object
                      required: [person]
                      properties:
                        person:
                          type: object
                          required: [idNumber]
                          properties: { idNumber: { type: string }, birthDate: { type: string, format: date } }
                        tags: { type: array, items: { type: string } }
                        note: { type: string }
              responses:
                '200': { description: ok, content: { application/json: { schema: { type: object, properties: { verified: { type: boolean } } } } } }
        """;

    [Fact]
    public void An_openapi_3_yaml_description_becomes_operations_with_inputs_outputs_and_the_connection_settings()
    {
        var read = OpenApiImport.Read(CommerceYaml);

        read.Title.ShouldBe("Ministry of Commerce");
        read.Version.ShouldBe("2.1");
        read.SuggestedProviderCode.ShouldBe("MINISTRY_OF_COMMERCE");
        read.Servers.ShouldBe(["https://api.commerce.example/v2"]);
        read.Authentication.Select(a => a.AuthType).ShouldBe([AuthType.OAuth2ClientCredentials, AuthType.Basic], "the supported schemes, the preferred first");
        read.Authentication[0].TokenUrl.ShouldBe("/oauth/v1/token");

        var get = read.Operations.Single(o => o.Method == "GET");
        get.Code.ShouldBe("GET_REGISTRATION_BY_NUMBER");
        get.Name.ShouldBe("Registration by CR number");
        get.Tag.ShouldBe("Registrations");
        get.PathTemplate.ShouldBe("/registrations/{cr_number}", "a placeholder takes the parameter's usable name");
        get.Parameters.Select(p => (p.Name, p.Location, p.IsRequired)).ShouldBe(
            [("cr_number", ParameterLocation.Path, true), ("lang", ParameterLocation.Query, false), ("X-Channel", ParameterLocation.Header, true)],
            "the authorization header is the connection's");
        get.Parameters[0].SampleValue.ShouldBe("1010000001");
        get.Parameters[1].SampleValue.ShouldBe("ar");
        get.Fields.Select(f => f.Path).ShouldBe(["crNumber", "status", "capital", "owners[*].name", "owners[*].nationalId", "activities[*]"]);
        get.Fields.Select(f => f.Name).ShouldBe(["crNumber", "status", "capital", "ownersName", "ownersNationalId", "activities"]);
        get.Fields.Single(f => f.Path == "capital").Type.ShouldBe("number");

        var post = read.Operations.Single(o => o.Method == "POST");
        post.Code.ShouldBe("VERIFY_OWNER");
        post.Parameters.Select(p => (p.Name, p.Target, p.IsRequired, p.IsMasked)).ShouldBe(
        [
            ("idNumber", "person.idNumber", true, true),
            ("birthDate", "person.birthDate", false, true),
            ("note", "note", false, false),
        ]);
        post.Parameters.ShouldAllBe(p => p.Location == ParameterLocation.Body);
        post.Warnings.ShouldContain(w => w.Contains("'tags' is a list", StringComparison.Ordinal));
        post.Fields.Single().Path.ShouldBe("verified");
    }

    [Fact]
    public void A_swagger_2_json_description_is_read_with_its_host_body_and_api_key()
    {
        const string swagger = """
            {
              "swagger": "2.0",
              "info": { "title": "nicPersonAPI", "version": "1" },
              "host": "nic.example", "basePath": "/api", "schemes": ["https"],
              "securityDefinitions": { "key": { "type": "apiKey", "in": "header", "name": "X-Api-Key" } },
              "paths": {
                "/person": {
                  "post": {
                    "parameters": [{ "in": "body", "name": "body", "schema": { "type": "object", "required": ["id"], "properties": { "id": { "type": "string" } } } }],
                    "responses": { "200": { "description": "ok", "schema": { "type": "object", "properties": { "fullName": { "type": "string" } } } } }
                  }
                }
              }
            }
            """;
        var read = OpenApiImport.Read(swagger);

        read.SuggestedProviderCode.ShouldBe("NIC_PERSON_API");
        read.Servers.ShouldBe(["https://nic.example/api"]);
        read.Authentication.Single().ShouldSatisfyAllConditions(a => a.AuthType.ShouldBe(AuthType.ApiKey), a => a.ApiKeyHeader.ShouldBe("X-Api-Key"));
        var operation = read.Operations.Single();
        operation.Code.ShouldBe("POST_PERSON", "no operation id: the method and the path");
        operation.Parameters.Single().ShouldSatisfyAllConditions(p => p.Name.ShouldBe("id"), p => p.Target.ShouldBe("id"), p => p.IsRequired.ShouldBeTrue());
        operation.Fields.Single().Name.ShouldBe("fullName");
    }

    [Fact]
    public void The_mock_gateways_description_reads_with_its_envelope_fields()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "DynamicCube.slnx")))
        {
            root = Path.GetDirectoryName(root) ?? throw new InvalidOperationException("Repository root not found.");
        }

        var read = OpenApiImport.Read(File.ReadAllText(Path.Combine(root, "src/Tools/MockGateway/samples/openapi.yaml")));

        read.Servers.ShouldBe(["http://localhost:5090"]);
        read.Authentication.Single().AuthType.ShouldBe(AuthType.OAuth2ClientCredentials);
        read.Operations.Select(o => o.Code).ShouldBe(["CRS_BY_ID_NUMBER", "CR_INFORMATION", "PERSON_INFORMATION"]);
        read.Operations[0].Fields.Select(f => f.Path).ShouldContain("data.result.Licenses[*].Commercial_NO");
        read.Operations[1].Fields.Select(f => f.Path).ShouldContain("data.result.activities[*].name");
    }

    [Theory]
    [InlineData("just some text")]
    [InlineData("{ \"openapi\": \"3.0.0\", \"info\": { \"title\": \"x\", \"version\": \"1\" }, \"paths\": {} }")]
    public void Text_that_is_not_a_description_with_operations_is_refused(string text) =>
        Should.Throw<OpenApiImportException>(() => OpenApiImport.Read(text));
}
