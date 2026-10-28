using DynamicCube.Modules.Parties.Domain;
using DynamicCube.Modules.Parties.Services;
using DynamicCube.Platform.Parties;
using DynamicCube.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Services.Tests;

/// <summary>Stands in for the national ID claim of the signed-in applicant.</summary>
public sealed class TestApplicant : IApplicantIdentity
{
    public string? NationalId { get; set; }

    public string? Name { get; set; }
}

/// <summary>An applicant who owns an establishment; calls run as that applicant acting for the establishment (or themselves).</summary>
internal sealed class Applicant : IAsyncDisposable
{
    private static int _sequence = Random.Shared.Next(1000, 9000) * 10000;

    private Applicant(TestHost host, TestApplicant identity)
    {
        Host = host;
        Identity = identity;
    }

    public TestHost Host { get; }

    public TestApplicant Identity { get; }

    public Guid IndividualId { get; private set; }

    public Guid EstablishmentId { get; private set; }

    public string CrNumber { get; } = "4" + Next();

    public static string NationalId() => "1" + Next();

    public static async Task<Applicant> CreateAsync(DatabaseFixture fixture, string relationship = "OWNER", List<string>? services = null)
    {
        var identity = new TestApplicant { NationalId = NationalId(), Name = "مقدم الطلب" };
        var host = new TestHost(fixture.RequireConnectionString(), s =>
        {
            s.AddSingleton(identity);
            s.AddScoped<IApplicantIdentity>(sp => sp.GetRequiredService<TestApplicant>());
        });
        var applicant = new Applicant(host, identity);
        applicant.IndividualId = await host.AsAsync("applicant", async sp => (await sp.GetRequiredService<ContextService>().CurrentIndividualAsync(CancellationToken.None)).Id);
        applicant.EstablishmentId = await host.AsAsync("admin", sp => sp.GetRequiredService<PartyAdminService>().RegisterAsync(
            new RegisterPartyRequest(PartyKind.Establishment, IdentifierTypes.CrNumber, applicant.CrNumber, "مؤسسة الاختبار", "Test establishment"), CancellationToken.None));
        await host.AsAsync("admin", sp => sp.GetRequiredService<PartyAdminService>().AddRelationshipAsync(
            applicant.EstablishmentId, new AddRelationshipRequest(identity.NationalId!, relationship, services, null, null), CancellationToken.None));
        return applicant;
    }

    /// <summary>Runs as the applicant acting for the establishment (default) or for themselves.</summary>
    public Task<T> AsAsync<T>(Func<IServiceProvider, Task<T>> action, bool self = false) =>
        Host.AsAsync("applicant", sp =>
        {
            var context = new DefaultHttpContext();
            if (!self)
            {
                context.Request.Headers[IActingPartyAccessor.Header] = EstablishmentId.ToString();
            }

            sp.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
            return action(sp);
        });

    public async ValueTask DisposeAsync() => await Host.DisposeAsync();

    private static string Next() => Interlocked.Increment(ref _sequence).ToString("D9", System.Globalization.CultureInfo.InvariantCulture);
}
