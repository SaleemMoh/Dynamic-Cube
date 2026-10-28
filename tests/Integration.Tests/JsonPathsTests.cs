using System.Text.Json.Nodes;
using DynamicCube.Modules.Integration.Runtime;
using DynamicCube.Platform.Integration;
using Shouldly;

namespace DynamicCube.Modules.Integration.Tests;

public sealed class JsonPathsTests
{
    private static readonly JsonNode Mci = JsonNode.Parse("""
        {
          "statusDetails": [{ "code": 200, "message": "Ok" }],
          "data": {
            "responseCode": "1",
            "result": { "Licenses": [
              { "Commercial_NO": "3350043331", "LicenseType": { "Id": "1" } },
              { "Commercial_NO": "1010012006", "LicenseType": { "Id": "0" } }
            ] }
          }
        }
        """)!;

    [Theory]
    [InlineData("statusDetails.code", "200")]
    [InlineData("data.responseCode", "1")]
    [InlineData("data.result.Licenses[1].Commercial_NO", "1010012006")]
    [InlineData("data.result.licenses.commercial_no", "3350043331")]
    [InlineData("data.missing.value", null)]
    public void Reads_values_through_arrays_and_ignoring_case(string path, string? expected) =>
        JsonPaths.ReadText(Mci, path).ShouldBe(expected);

    [Fact]
    public void Wildcard_reads_every_element() =>
        JsonPaths.Read(Mci, "data.result.Licenses[*].LicenseType.Id")!.ToJsonString().ShouldBe("""["1","0"]""");

    [Fact]
    public void Status_object_and_status_array_read_the_same()
    {
        var nic = JsonNode.Parse("""{ "statusDetails": { "code": "200" } }""");

        JsonPaths.ReadText(nic, "statusDetails.code").ShouldBe("200");
    }

    [Theory]
    [InlineData("data.result", true)]
    [InlineData("Licenses[*].Commercial_NO", true)]
    [InlineData("a[0][1].b", true)]
    [InlineData("data..result", false)]
    [InlineData("data.result;drop", false)]
    [InlineData("", false)]
    public void Validates_paths(string path, bool valid) => JsonPaths.IsValid(path).ShouldBe(valid);

    [Fact]
    public void Writes_nested_body_values()
    {
        var body = new JsonObject();

        JsonPaths.Write(body, "request.person.idNumber", "1000000001");

        body.ToJsonString().ShouldBe("""{"request":{"person":{"idNumber":"1000000001"}}}""");
    }
}
