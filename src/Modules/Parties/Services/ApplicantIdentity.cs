using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace DynamicCube.Modules.Parties.Services;

/// <summary>The signed-in applicant as the identity provider describes them.</summary>
public interface IApplicantIdentity
{
    string? NationalId { get; }

    string? Name { get; }
}

internal sealed class ClaimsApplicantIdentity(IHttpContextAccessor accessor, IOptions<PartiesOptions> options) : IApplicantIdentity
{
    public string? NationalId => accessor.HttpContext?.User.FindFirst(options.Value.NationalIdClaim)?.Value;

    public string? Name => accessor.HttpContext?.User.FindFirst(options.Value.NameClaim)?.Value;
}
