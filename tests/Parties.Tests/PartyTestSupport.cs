using System.Net;
using DynamicCube.Modules.Integration.Changes;
using DynamicCube.Modules.Parties.Changes;
using DynamicCube.Modules.Parties.Services;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicCube.Modules.Parties.Tests;

/// <summary>Stands in for the national ID and name claims of the signed-in applicant.</summary>
public sealed class TestApplicant : IApplicantIdentity
{
    public string? NationalId { get; set; }

    public string? Name { get; set; }
}

internal static class PartyTestSupport
{
    private static int _sequence = Random.Shared.Next(1000, 9000) * 10000;

    /// <summary>A unique, valid citizen ID number.</summary>
    public static string NationalId() => "1" + Interlocked.Increment(ref _sequence).ToString("D9", System.Globalization.CultureInfo.InvariantCulture);

    public static string CrNumber() => "4" + Interlocked.Increment(ref _sequence).ToString("D9", System.Globalization.CultureInfo.InvariantCulture);

    public static TestHost Host(DatabaseFixture fixture, TestApplicant applicant) =>
        new(fixture.RequireConnectionString(), services =>
        {
            services.AddSingleton(applicant);
            services.AddScoped<IApplicantIdentity>(sp => sp.GetRequiredService<TestApplicant>());
        });

    /// <summary>
    /// Publishes an MCI-style operation on a path unique to the test and a context source reading it; returns the path
    /// prefix the fake provider should answer (every other context source of the shared database gets 404).
    /// </summary>
    public static async Task<string> PublishMciContextSourceAsync(TestHost host)
    {
        var prefix = "/v1/" + TestHost.Unique("T").ToLowerInvariant();
        var operation = await MomrahSamples.PublishAsync(host, retryCount: 0, operation: (c, e) => MomrahSamples.CrByIdNumber(c, e, path: prefix + "/{idNumber}"));
        await host.PublishAsync(
            PartyChangeTypes.Area,
            new Change(PartyChangeTypes.ContextSource, TestHost.Unique("MCI_CRS"), ChangeOperation.Create, new ContextSourcePayload
            {
                NameAr = "السجلات التجارية",
                NameEn = "Commercial registrations",
                OperationCode = operation,
                InputParameter = "idNumber",
                ItemsPath = "Licenses",
                IdentifierPath = "Commercial_NO",
                NameArPath = "Commercial_Name",
                RelationCodePath = "RelationTypeInfo.Id",
                RelationProvider = "MCI",
                StatusPath = "Status",
                ActiveStatuses = ["Active"],
                RefreshMinutes = 60,
            }));
        return prefix;
    }

    /// <summary>Answers the token endpoint and the test's own paths; 404 for everything else.</summary>
    public static void Answer(TestHost host, string prefix, Func<RecordedRequest, (HttpStatusCode, string)> api) =>
        host.Provider.Respond = r =>
            r.Url.AbsolutePath == "/oauth/v1/token" ? (HttpStatusCode.OK, MomrahSamples.Token("t"))
            : r.Url.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal) ? api(r)
            : (HttpStatusCode.NotFound, string.Empty);

    public static string Licenses(params (string Cr, string Name, string Relation, string Status)[] licenses) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            statusDetails = new[] { new { code = 200, message = "Ok" } },
            data = new
            {
                responseCode = "1",
                result = new
                {
                    Licenses = licenses.Select(l => new Dictionary<string, object>
                    {
                        ["Commercial_NO"] = l.Cr,
                        ["Commercial_Name"] = l.Name,
                        ["RelationTypeInfo"] = new { Id = l.Relation, Name = "x" },
                        ["Status"] = l.Status,
                    }),
                },
            },
        });

    public static void Header(IServiceProvider services, string? actingParty)
    {
        var context = new DefaultHttpContext();
        if (actingParty is not null)
        {
            context.Request.Headers[Platform.Parties.IActingPartyAccessor.Header] = actingParty;
        }

        services.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
    }

    public static string IntegrationArea => IntegrationChangeTypes.Area;
}
