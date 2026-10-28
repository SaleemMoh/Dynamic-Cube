using System.Globalization;
using DynamicCube.Platform;
using DynamicCube.Platform.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace DynamicCube.Hosting;

/// <summary>Defaults shared by every Dynamic Cube process (APIs, worker, migrator).</summary>
public static class HostDefaults
{
    public const string ReadyTag = "ready";

    /// <summary>Serilog, OpenTelemetry, the platform kernel and readiness health checks.</summary>
    public static IHostApplicationBuilder AddDynamicCubeDefaults(this IHostApplicationBuilder builder, string appCode)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(appCode);

        builder.Services.AddSerilog((services, logger) => logger
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("App", appCode)
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService($"dynamic-cube-{appCode.ToLowerInvariant()}"))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
                if (HasOtlpEndpoint(builder.Configuration))
                {
                    tracing.AddOtlpExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
                if (HasOtlpEndpoint(builder.Configuration))
                {
                    metrics.AddOtlpExporter();
                }
            });

        builder.Services.AddPlatform(builder.Configuration);
        builder.Services.AddHealthChecks().AddDbContextCheck<PlatformDbContext>("database", tags: [ReadyTag]);

        return builder;
    }

    private static bool HasOtlpEndpoint(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
}
