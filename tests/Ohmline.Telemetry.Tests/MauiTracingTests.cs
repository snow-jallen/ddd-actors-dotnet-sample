using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Ohmline.Telemetry.Tests;

/// <summary>
/// What the MAUI head relies on (section 19.8): MAUI never starts hosted services, so the
/// OpenTelemetry SDK's own hosted service never builds the tracer provider. Resolving the
/// provider from the container builds it, and from then on its listener samples and exports.
/// The test's source has a name of its own, outside <c>Ohmline.*</c>, so that no node in
/// another test listens to it.
/// </summary>
public sealed class MauiTracingTests
{
    [Fact]
    public void Without_hosted_services_resolving_the_tracer_provider_starts_tracing()
    {
        using var source = new ActivitySource($"Maui.Head.Probe.{Guid.NewGuid():N}");
        var exported = new List<Activity>();
        var services = new ServiceCollection();
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddSource(source.Name).AddInMemoryExporter(exported));
        using var container = services.BuildServiceProvider();

        var before = source.StartActivity("before the provider is resolved");
        var provider = container.GetRequiredService<TracerProvider>();
        using (source.StartActivity("after the provider is resolved"))
        {
        }

        provider.ForceFlush();
        before.ShouldBeNull(); // Nothing listens until the provider exists.
        exported.ShouldHaveSingleItem().DisplayName.ShouldBe("after the provider is resolved");
    }
}
