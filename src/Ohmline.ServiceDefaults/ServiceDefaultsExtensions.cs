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
/// The defaults every Ohmline service shares. Two health endpoints (section 18.5):
/// <c>/health/live</c> runs only the checks tagged <see cref="LiveTag"/> and says whether the
/// process should be restarted; <c>/health/ready</c> runs those tagged <see cref="ReadyTag"/>,
/// such as "an Up member of the cluster" and "the journal's database answers", and says whether
/// it should be sent work.
/// </summary>
public static class ServiceDefaultsExtensions
{
    /// <summary>The tag of the checks that <c>/health/live</c> runs: never a dependency.</summary>
    public const string LiveTag = "live";

    /// <summary>The tag of the checks that <c>/health/ready</c> runs.</summary>
    public const string ReadyTag = "ready";

    public const string LivePath = "/health/live";

    public const string ReadyPath = "/health/ready";

    /// <summary>
    /// Every source and meter Ohmline names (section 17.1): each context's, the actor shell's,
    /// and any added later. A source that is not listened to records nothing at all, so a name
    /// missing here is the most common reason a span never shows up.
    /// </summary>
    public const string OhmlineTelemetry = "Ohmline.*";

    /// <summary>
    /// Microsoft.FeatureManagement's source: a span per evaluation of a flag whose telemetry is
    /// on, with the flag and its outcome (section 21.8). A canary's traces then say which
    /// flags were on when they were decided.
    /// </summary>
    public const string FeatureManagementTelemetry = "Microsoft.FeatureManagement";

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
                    .AddSource(OhmlineTelemetry, FeatureManagementTelemetry)
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
        /// <c>/health/live</c> and <c>/health/ready</c>, in every environment, not only in
        /// Development as the Aspire template maps them: orchestrators probe them in production
        /// (Chapter 21). Each runs only the checks with its tag, so a check without a tag runs
        /// in neither, and each says no more than its status.
        /// </summary>
        public WebApplication MapDefaultEndpoints()
        {
            app.MapHealthChecks(LivePath, new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains(LiveTag),
            });
            app.MapHealthChecks(ReadyPath, new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains(ReadyTag),
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
        path.StartsWithSegments("/health");
}
