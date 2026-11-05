using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Ohmline.ServiceDefaults;

/// <summary>
/// The defaults every Ohmline service shares. Two health endpoints: <c>/alive</c> says the
/// process answers (only checks tagged <see cref="LiveTag"/>), and <c>/health</c> runs every
/// check, such as "this node is an Up member of the cluster", and says whether it is ready.
/// </summary>
public static class ServiceDefaultsExtensions
{
    /// <summary>The tag of the checks that <c>/alive</c> runs: never a dependency.</summary>
    public const string LiveTag = "live";

    /// <summary>
    /// Every source and meter Ohmline names (section 17.1): each context's, the actor shell's,
    /// and any added later. A source that is not listened to records nothing at all, so a name
    /// missing here is the most common reason a span never shows up.
    /// </summary>
    public const string OhmlineTelemetry = "Ohmline.*";

    /// <summary>Where the exporter sends to; Aspire sets it for every project it runs.</summary>
    public const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    extension<TBuilder>(TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        /// <summary>
        /// Telemetry, health checks, service discovery, and a standard resilience handler on
        /// every HttpClient.
        /// </summary>
        public TBuilder AddServiceDefaults()
        {
            builder.ConfigureOpenTelemetry();
            builder.Services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy(), [LiveTag]);
            builder.Services.AddServiceDiscovery();
            builder.Services.ConfigureHttpClientDefaults(http =>
            {
                http.AddStandardResilienceHandler();
                http.AddServiceDiscovery();
            });
            return builder;
        }

        /// <summary>
        /// Traces, metrics and logs through the OpenTelemetry SDK: Ohmline's own sources and
        /// meters, ASP.NET Core, HttpClient and the runtime, under a resource that says which
        /// service, which version and which environment. Exported over OTLP when an endpoint is
        /// configured, and otherwise collected and dropped, so a test or a laptop without a
        /// collector runs the same code.
        /// </summary>
        public TBuilder ConfigureOpenTelemetry()
        {
            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
            });

            var telemetry = builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource
                    .AddService(
                        serviceName: ServiceName(builder),
                        serviceVersion: ServiceVersion(),
                        // Aspire and Kubernetes say which instance in OTEL_RESOURCE_ATTRIBUTES.
                        autoGenerateServiceInstanceId: false)
                    .AddAttributes(
                    [
                        new("deployment.environment.name", builder.Environment.EnvironmentName),
                    ]))
                .WithTracing(tracing => tracing
                    .AddSource(OhmlineTelemetry)
                    .AddAspNetCoreInstrumentation(aspNetCore =>
                        aspNetCore.Filter = context => !IsProbe(context.Request.Path))
                    .AddHttpClientInstrumentation())
                .WithMetrics(metrics => metrics
                    .AddMeter(OhmlineTelemetry)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation());

            if (!string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointKey]))
            {
                // Traces, metrics and logs, to OTEL_EXPORTER_OTLP_ENDPOINT, in one call.
                telemetry.UseOtlpExporter();
            }

            return builder;
        }
    }

    extension(WebApplication app)
    {
        /// <summary>
        /// <c>/health</c> and <c>/alive</c>, in every environment: orchestrators probe them
        /// (Chapter 21), and they say no more than healthy or not.
        /// </summary>
        public WebApplication MapDefaultEndpoints()
        {
            app.MapHealthChecks("/health");
            app.MapHealthChecks("/alive", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains(LiveTag),
            });
            return app;
        }
    }

    /// <summary>
    /// The name Aspire gave the project (<c>node</c>, <c>gateway</c>, <c>billing</c>), or, run
    /// on its own, the project's.
    /// </summary>
    private static string ServiceName(IHostApplicationBuilder builder) =>
        builder.Configuration["OTEL_SERVICE_NAME"] is { Length: > 0 } name
            ? name
            : builder.Environment.ApplicationName;

    /// <summary>What lets a canary be told apart from the stable version in any backend.</summary>
    private static string? ServiceVersion() =>
        Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

    /// <summary>Probes come every few seconds and say nothing about a charge: no trace.</summary>
    private static bool IsProbe(PathString path) =>
        path.StartsWithSegments("/health") || path.StartsWithSegments("/alive");
}
