using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Ohmline.Billing.Diagnostics;

namespace Ohmline.Billing.Tests.Diagnostics;

public sealed class BillingDiagnosticsTests
{
    [Fact]
    public void Billing_spans_and_instruments_are_recorded_under_one_name()
    {
        BillingDiagnostics.ActivitySource.Name.ShouldBe("Ohmline.Billing");
        BillingDiagnostics.MeterName.ShouldBe("Ohmline.Billing");
    }

    [Fact]
    public void Given_the_hosts_meter_factory_then_the_meter_is_made_and_owned_by_it()
    {
        using var services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var factory = services.GetRequiredService<IMeterFactory>();

        var meter = BillingDiagnostics.CreateMeter(factory);

        meter.Name.ShouldBe(BillingDiagnostics.MeterName);
        meter.Scope.ShouldBeSameAs(factory);
        BillingDiagnostics.CreateMeter(factory).ShouldBeSameAs(meter);
    }
}
