using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Ohmline.Charging.Actors.Diagnostics;

namespace Ohmline.Charging.Actors.Tests.Diagnostics;

public sealed class ChargingDiagnosticsTests
{
    [Fact]
    public void Charging_spans_and_instruments_are_recorded_under_one_name()
    {
        ChargingDiagnostics.ActivitySource.Name.ShouldBe("Ohmline.Charging");
        ChargingDiagnostics.MeterName.ShouldBe("Ohmline.Charging");
    }

    [Fact]
    public void Given_the_hosts_meter_factory_then_the_meter_is_made_and_owned_by_it()
    {
        using var services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var factory = services.GetRequiredService<IMeterFactory>();

        var meter = ChargingDiagnostics.CreateMeter(factory);

        meter.Name.ShouldBe(ChargingDiagnostics.MeterName);
        meter.Scope.ShouldBeSameAs(factory);
        ChargingDiagnostics.CreateMeter(factory).ShouldBeSameAs(meter);
    }
}
