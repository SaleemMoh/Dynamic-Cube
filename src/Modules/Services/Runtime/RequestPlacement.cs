using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Platform.Security;

namespace DynamicCube.Modules.Services.Runtime;

/// <summary>
/// Where a request belongs (ADR-0036 step 6): the service's company (else the installation's first), and the branch of it
/// the answer to the service's branch question names — by branch code, region or city — else the company's default branch.
/// </summary>
internal static class RequestPlacement
{
    public static async Task<(string? Company, string? Branch)> PlaceAsync(IOrganizationDirectory? organization, Service service, JsonObject data, CancellationToken cancellationToken)
    {
        if (organization is null)
        {
            return (null, null);
        }

        var companies = await organization.CompaniesAsync(cancellationToken);
        var company = service.CompanyCode is { } wanted && companies.Any(c => c.Code == wanted) ? wanted : companies.Count > 0 ? companies[0].Code : null;
        if (company is null)
        {
            return (null, null);
        }

        var branches = (await organization.BranchesAsync(cancellationToken)).Where(b => b.CompanyCode == company).ToList();
        var answer = service.BranchQuestion is { } question ? Text(data[question]) : null;
        var branch = (answer is null
                ? null
                : branches.FirstOrDefault(b => Same(b.Code, answer)) ?? branches.FirstOrDefault(b => Same(b.RegionCode, answer)) ?? branches.FirstOrDefault(b => Same(b.City, answer)))
            ?? branches.FirstOrDefault(b => b.IsDefault)
            ?? branches.FirstOrDefault();
        return (company, branch?.Code);
    }

    /// <summary>An answer as text: a string or number, or the first of several choices.</summary>
    private static string? Text(JsonNode? node) => node switch
    {
        JsonArray array => array.Count > 0 ? Text(array[0]) : null,
        JsonValue value when value.GetValueKind() == JsonValueKind.String => value.GetValue<string>().Trim() is { Length: > 0 } s ? s : null,
        JsonValue value when value.GetValueKind() == JsonValueKind.Number => value.ToJsonString(),
        _ => null,
    };

    private static bool Same(string? a, string b) => a is not null && string.Equals(a.Trim(), b, StringComparison.OrdinalIgnoreCase);
}
