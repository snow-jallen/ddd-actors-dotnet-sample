using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Ohmline.ServiceDefaults;

namespace Ohmline.Node.Tests;

/// <summary>
/// What every host gets from <c>AddServiceDefaults</c> (section 17.2): the SDK listens to every
/// source and meter whose name starts with <c>Ohmline.</c>, and to nothing it was not told of.
/// </summary>
public sealed class ServiceDefaultsTelemetryTests
{
    [Fact]
    public void Given_the_defaults_then_every_Ohmline_source_is_listened_to_and_no_other()
    {
        using var host = Host(new());
        _ = host.Services.GetRequiredService<TracerProvider>();

        using var ohmline = new ActivitySource($"Ohmline.Probe{Guid.NewGuid():N}");
        using var elsewhere = new ActivitySource($"Elsewhere.Probe{Guid.NewGuid():N}");

        ohmline.HasListeners().ShouldBeTrue();
        elsewhere.HasListeners().ShouldBeFalse();
    }

    /// <summary>
    /// Metrics need a reader: without an exporter the SDK enables no instrument at all, so
    /// recording costs nothing. With one (here an OTLP endpoint that no one listens on; nothing
    /// is sent before the first export interval), Ohmline's instruments are collected.
    /// </summary>
    [Theory]
    [InlineData(null, false)]
    [InlineData("http://127.0.0.1:9", true)]
    public void Given_an_OTLP_endpoint_or_none_then_Ohmline_meters_are_collected_or_not(
        string? endpoint, bool collected)
    {
        using var host = Host(new() { [ServiceDefaultsExtensions.OtlpEndpointKey] = endpoint });
        _ = host.Services.GetRequiredService<MeterProvider>();

        using var ohmline = new Meter($"Ohmline.Probe{Guid.NewGuid():N}");
        using var elsewhere = new Meter($"Elsewhere.Probe{Guid.NewGuid():N}");

        ohmline.CreateCounter<long>("probe").Enabled.ShouldBe(collected);
        elsewhere.CreateCounter<long>("probe").Enabled.ShouldBeFalse();
    }

    [Fact]
    public void Given_a_service_name_from_Aspire_then_the_resource_says_which_service_and_where()
    {
        using var host = Host(new() { ["OTEL_SERVICE_NAME"] = "node" }, "Staging");

        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        var attributes = resource.Attributes.ToDictionary();
        attributes["service.name"].ShouldBe("node");
        attributes["deployment.environment.name"].ShouldBe("Staging");
        attributes.ShouldContainKey("service.version");
    }

    private static IHost Host(Dictionary<string, string?> settings, string environment = "Test")
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { EnvironmentName = environment });
        builder.Configuration.AddInMemoryCollection(settings);
        builder.AddServiceDefaults();
        return builder.Build();
    }
}
