using DynamicCube.Modules.Integration.Changes;
using DynamicCube.Modules.Integration.Domain;
using DynamicCube.Modules.Integration.Tests.Infrastructure;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Integration;

namespace DynamicCube.Modules.Integration.Tests;

/// <summary>MoMRAH gateway configuration and sample responses as given in the MCI and NIC integration guides.</summary>
internal static class MomrahSamples
{
    public const string CrInformation = """
        {
          "statusDetails": [{ "code": 200, "message": "Ok" }],
          "data": {
            "responseCode": "1",
            "responseMessage": "success",
            "result": { "Licenses": [
              { "Commercial_NO": "3350043331", "Commercial_Name": "مؤسسة مشعل للنظارات", "LicenseType": { "Id": "1", "Name": "Branch" },
                "RelationTypeInfo": { "Id": "1", "Name": "مالك المؤسسة" }, "Source_Name": " حائل", "Status": "Active" },
              { "Commercial_NO": "1010012006", "Commercial_Name": "مؤسسة سلطان زيد الحسين للمقاولات", "LicenseType": { "Id": "0", "Name": "Main" },
                "RelationTypeInfo": { "Id": "1", "Name": "مالك المؤسسة" }, "Source_Name": "الرياض", "Status": "Active" }
            ] }
          }
        }
        """;

    public const string RecordNotFound = """
        { "statusDetails": [{ "code": 200, "message": "Ok" }], "data": { "responseCode": "001-000001", "responseMessage": "Record Not Found" } }
        """;

    public const string WrongIdType = """
        { "statusDetails": [{ "code": 200, "message": "Ok" }], "data": { "responseCode": "001-000007", "responseMessage": "Wrong ID Type" } }
        """;

    public const string TokenExpired = """{ "statusDetails": [{ "code": 401, "message": "Access Token Expired" }] }""";

    public static string Token(string value) => $$"""{ "access_token": "{{value}}", "token_type": "Bearer", "expires_in": 17999 }""";

    public static EnvelopeProfilePayload Envelope() => new()
    {
        NameAr = "بوابة الوزارة",
        NameEn = "MoMRAH gateway",
        StatusCodePath = "statusDetails.code",
        StatusMessagePath = "statusDetails.message",
        SuccessStatusCodes = ["200"],
        BusinessCodePath = "data.responseCode",
        BusinessMessagePath = "data.responseMessage",
        SuccessBusinessCodes = ["1"],
        ResultPath = "data.result",
        ResponseCodes =
        [
            new() { Code = "001-000001", Outcome = IntegrationOutcome.NotFound, MessageAr = "لا يوجد سجل مطابق.", MessageEn = "No matching record." },
        ],
    };

    public static OperationPayload CrByIdNumber(string connection, string envelope, int cacheSeconds = 0) => new()
    {
        NameAr = "السجلات التجارية برقم الهوية",
        NameEn = "CRs by ID number",
        ConnectionCode = connection,
        EnvelopeProfileCode = envelope,
        Method = "GET",
        PathTemplate = "/v1/MCI-DIP/cr-information-id-number/{idNumber}",
        Parameters = [new() { Name = "idNumber", Location = ParameterLocation.Path, IsMasked = true }],
        Outputs =
        [
            new() { Name = "crNumbers", Path = "Licenses[*].Commercial_NO" },
            new() { Name = "firstName", Path = "Licenses.Commercial_Name" },
        ],
        ResponseCodes =
        [
            new() { Code = "001-000007", Outcome = IntegrationOutcome.BusinessError, MessageAr = "نوع الهوية غير صحيح.", MessageEn = "The ID type is not valid." },
        ],
        MaskFields = ["Commercial_Name"],
        CacheSeconds = cacheSeconds,
    };

    /// <summary>Publishes a provider, a gateway connection, the envelope and one operation; returns the operation code.</summary>
    public static async Task<string> PublishAsync(TestHost host, int retryCount = 2, int breakerFailures = 5, int cacheSeconds = 0, Func<string, string, OperationPayload>? operation = null)
    {
        var provider = TestHost.Unique("MCI");
        var connection = TestHost.Unique("MOMRAH");
        var envelope = TestHost.Unique("ENV");
        var code = TestHost.Unique("MCI_CR_BY_ID");
        await host.PublishAsync(
            IntegrationChangeTypes.Area,
            new Change(IntegrationChangeTypes.Provider, provider, ChangeOperation.Create, new ProviderPayload { NameAr = "وزارة التجارة", NameEn = "Ministry of Commerce" }),
            new Change(IntegrationChangeTypes.Connection, connection, ChangeOperation.Create, new ConnectionPayload
            {
                NameAr = "بوابة الوزارة",
                NameEn = "MoMRAH gateway",
                ProviderCode = provider,
                BaseUrl = "https://gateway.test",
                AuthType = AuthType.OAuth2ClientCredentials,
                SecretName = TestHost.SecretName,
                TokenUrl = "/oauth/v1/token",
                RetryCount = retryCount,
                CircuitBreakerFailures = breakerFailures,
                TimeoutSeconds = 5,
            }),
            new Change(IntegrationChangeTypes.EnvelopeProfile, envelope, ChangeOperation.Create, Envelope()),
            new Change(IntegrationChangeTypes.Operation, code, ChangeOperation.Create, (operation ?? ((c, e) => CrByIdNumber(c, e, cacheSeconds)))(connection, envelope)));
        return code;
    }
}
