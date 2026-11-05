using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

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

    extension<TBuilder>(TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        /// <summary>
        /// Health checks, service discovery, and a standard resilience handler on every
        /// HttpClient. Telemetry is added in Chapter 17.
        /// </summary>
        public TBuilder AddServiceDefaults()
        {
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
}
