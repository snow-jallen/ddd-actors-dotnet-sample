using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.ServiceDefaults;

namespace Ohmline.Node.Tests;

/// <summary>
/// The node's feature flags (section 21.8): Microsoft.FeatureManagement evaluates them in the
/// host, from configuration in its own schema, and the entities read the outcome.
/// </summary>
public sealed class FeatureFlagTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_the_flag_on_in_configuration_then_the_entities_see_it_on()
    {
        var flags = FlagsFrom(enabled: true);

        await flags.RefreshAsync(Ct);

        flags.Current.IsEnabled(ChargingFeatures.DynamicIdleFee).ShouldBeTrue();
    }

    [Fact]
    public async Task Given_the_flag_off_in_configuration_then_the_entities_see_it_off()
    {
        var flags = FlagsFrom(enabled: false);

        await flags.RefreshAsync(Ct);

        flags.Current.IsEnabled(ChargingFeatures.DynamicIdleFee).ShouldBeFalse();
    }

    [Fact]
    public async Task Given_a_flag_with_telemetry_then_each_evaluation_is_traced()
    {
        List<Activity> traced = [];
        using var listener = new ActivityListener
        {
            ShouldListenTo = source =>
                source.Name == ServiceDefaultsExtensions.FeatureManagementTelemetry,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = traced.Add,
        };
        ActivitySource.AddActivityListener(listener);
        var flags = FlagsFrom(enabled: true);

        await flags.RefreshAsync(Ct);

        traced.ShouldNotBeEmpty();
    }

    private static FeatureManagementFlags FlagsFrom(bool enabled)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["feature_management:feature_flags:0:id"] = ChargingFeatures.DynamicIdleFee,
                ["feature_management:feature_flags:0:enabled"] = enabled ? "true" : "false",
                ["feature_management:feature_flags:0:telemetry:enabled"] = "true",
            })
            .Build();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging();
        services.AddFeatureManagement();
        var features = services.BuildServiceProvider().GetRequiredService<IFeatureManager>();
        return new FeatureManagementFlags(features, TimeProvider.System);
    }
}
