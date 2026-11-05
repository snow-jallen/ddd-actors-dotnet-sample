using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Tracing;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Ohmline.ServiceDefaults;

namespace Ohmline.Telemetry.Tests;

/// <summary>
/// What .NET 11's <c>AddTracing</c> and <see cref="ActivitySourceFactory"/> do beside the
/// OpenTelemetry SDK, on SDK 11.0.100-rc.3 and OpenTelemetry 1.20.1 (section 18.6, ADR 0013).
/// The extension methods that configure rules, such as <c>DisableTracing</c>, are in
/// <c>Microsoft.Extensions.Diagnostics.Tracing</c>; <c>AddTracing</c> itself is in
/// <c>Microsoft.Extensions.DependencyInjection</c>.
/// </summary>
public sealed class AddTracingTests
{
    [Fact]
    public void Given_a_source_from_the_factory_then_the_SDK_exports_its_spans_by_name()
    {
        var exported = new List<Activity>();
        using var host = Services(exported);
        var source = Factory(host).Create(new ActivitySourceOptions("Ohmline.Spike.Exported"));

        using (source.StartActivity("probe"))
        {
        }

        // A subclass of its own: ActivitySource is no longer sealed in .NET 11.
        source.GetType().ShouldNotBe(typeof(ActivitySource));
        exported.ShouldHaveSingleItem().Source.ShouldBeSameAs(source);
    }

    [Fact]
    public void Given_the_same_name_then_the_factory_gives_the_same_source_scoped_to_itself()
    {
        using var host = Services([]);
        var factory = Factory(host);

        var source = factory.Create(new ActivitySourceOptions("Ohmline.Spike.Cached"));

        factory.Create(new ActivitySourceOptions("Ohmline.Spike.Cached")).ShouldBeSameAs(source);
        source.Scope.ShouldBeSameAs(factory);
    }

    [Fact]
    public void Given_a_rule_that_disables_a_source_then_the_SDK_still_exports_its_spans()
    {
        var exported = new List<Activity>();
        using var host = Services(
            exported, tracing => tracing.DisableTracing("Ohmline.Spike.Disabled"));
        var source = Factory(host).Create(new ActivitySourceOptions("Ohmline.Spike.Disabled"));

        using (source.StartActivity("probe"))
        {
        }

        exported.ShouldHaveSingleItem(); // The rules govern the factory's listeners, not the SDK's.
    }

    [Fact]
    public void Given_its_container_is_disposed_then_the_factorys_source_records_nothing()
    {
        var host = Services([]);
        var source = Factory(host).Create(new ActivitySourceOptions("Ohmline.Spike.Disposed"));

        host.Dispose();
        using var listener = new ActivityListener
        {
            ShouldListenTo = candidate => candidate.Name == "Ohmline.Spike.Disposed",
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        source.StartActivity("probe").ShouldBeNull();
    }

    [Fact]
    public void Given_a_host_without_AddTracing_then_there_is_no_factory_to_ask()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.ConfigureOpenTelemetry();
        using var host = builder.Build();

        host.Services.GetService<ActivitySourceFactory>().ShouldBeNull();
    }

    private static ServiceProvider Services(
        ICollection<Activity> exported, Action<ITracingBuilder>? rules = null)
    {
        var services = new ServiceCollection();
        services.AddTracing(tracing => rules?.Invoke(tracing));
        services.AddOpenTelemetry().WithTracing(tracing => tracing
            .AddSource(ServiceDefaultsExtensions.OhmlineTelemetry)
            .AddInMemoryExporter(exported));
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TracerProvider>(); // The SDK listens once it is built.
        return provider;
    }

    private static ActivitySourceFactory Factory(IServiceProvider services) =>
        services.GetRequiredService<ActivitySourceFactory>();
}
