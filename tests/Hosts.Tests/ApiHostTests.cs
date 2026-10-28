extern alias ExternalApi;
extern alias InternalApi;

using System.Net;
using System.Net.Http.Json;
using DynamicCube.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace DynamicCube.Hosts.Tests;

public sealed class InternalApiTests : ApiHostTests<InternalApi::Program>
{
    public InternalApiTests()
        : base("INTERNAL")
    {
    }
}

public sealed class ExternalApiTests : ApiHostTests<ExternalApi::Program>
{
    public ExternalApiTests()
        : base("EXTERNAL")
    {
    }
}

public abstract class ApiHostTests<TProgram> : IDisposable
    where TProgram : class
{
    private readonly WebApplicationFactory<TProgram> _factory;
    private readonly string _appCode;

    protected ApiHostTests(string appCode)
    {
        _appCode = appCode;
        _factory = new WebApplicationFactory<TProgram>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DynamicCube", "Host=127.0.0.1;Port=1;Database=none;Username=none;Timeout=1");
        });
    }

    [Fact]
    public async Task Liveness_probe_does_not_need_the_database()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Readiness_probe_reports_unavailable_database()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Info_endpoint_identifies_the_host()
    {
        using var client = _factory.CreateClient();

        var info = await client.GetFromJsonAsync<HostInfo>(new Uri("/api/info", UriKind.Relative), TestContext.Current.CancellationToken);

        info!.App.ShouldBe(_appCode);
    }

    [Fact]
    public async Task Current_user_endpoint_requires_a_token()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/me", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }
}
