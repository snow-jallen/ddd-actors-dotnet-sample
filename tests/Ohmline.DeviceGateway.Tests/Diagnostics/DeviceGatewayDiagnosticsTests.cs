using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Ohmline.DeviceGateway.Diagnostics;

namespace Ohmline.DeviceGateway.Tests.Diagnostics;

public sealed class DeviceGatewayDiagnosticsTests
{
    [Fact]
    public void Device_Gateway_spans_and_instruments_are_recorded_under_one_name()
    {
        DeviceGatewayDiagnostics.ActivitySource.Name.ShouldBe("Ohmline.DeviceGateway");
        DeviceGatewayDiagnostics.MeterName.ShouldBe("Ohmline.DeviceGateway");
    }

    [Fact]
    public void Given_the_hosts_meter_factory_then_the_meter_is_made_and_owned_by_it()
    {
        using var services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var factory = services.GetRequiredService<IMeterFactory>();

        var meter = DeviceGatewayDiagnostics.CreateMeter(factory);

        meter.Name.ShouldBe(DeviceGatewayDiagnostics.MeterName);
        meter.Scope.ShouldBeSameAs(factory);
        DeviceGatewayDiagnostics.CreateMeter(factory).ShouldBeSameAs(meter);
    }
}
