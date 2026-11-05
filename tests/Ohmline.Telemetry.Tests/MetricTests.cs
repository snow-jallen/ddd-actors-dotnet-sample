using System.Diagnostics.Metrics;
using Akka.Actor;
using Akka.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Time.Testing;
using Ohmline.ActorShell.Diagnostics;
using Ohmline.ActorShell.Telemetry;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Actors.Diagnostics;
using Ohmline.Charging.Actors.Serialization;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Actors.Budgets;
using Ohmline.SiteEnergy.Actors.Diagnostics;
using Ohmline.SiteEnergy.Actors.Serialization;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.Testing;

namespace Ohmline.Telemetry.Tests;

/// <summary>
/// Metrics as a test reads them (section 18.3). A <see cref="MetricCollector{T}"/> listens to
/// one instrument, of the meters one <see cref="IMeterFactory"/> made: the test's container is
/// a host of its own, so tests that run in parallel, each with a meter named
/// <c>Ohmline.Charging</c>, never see each other's measurements.
/// </summary>
public sealed class MetricTests(ITestOutputHelper output)
    : Akka.TestKit.Xunit.TestKit(
        ActorTestConfig.With(ChargingSerializer, SiteEnergySerializer),
        nameof(MetricTests),
        output)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(3);
    private static readonly DateTimeOffset Morning = new(2026, 11, 4, 14, 40, 0, TimeSpan.Zero);
    private static readonly ChargePointId Charger = new("OHM-SLC-0042");
    private static readonly SiteId Depot = new("slc-depot");

    private readonly ServiceProvider _host = Host();
    private readonly FakeTimeProvider _clock = new(Morning);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IMeterFactory Meters => _host.GetRequiredService<IMeterFactory>();

    [Fact]
    public async Task Given_a_refused_command_then_the_collector_has_it_with_its_reason()
    {
        using var refused = new MetricCollector<long>(
            Meters, ChargingDiagnostics.MeterName, ChargingMetrics.CommandsRejected);
        var chargePoint = Sys.ActorOf(ChargePointActor.Props(
            Charger, _clock, _host.GetRequiredService<ChargingMetrics>()));

        await chargePoint.Ask<CommandRejected>(
            new ReserveConnector(CommandId.New(_clock), Charger, 1, new("04A2B3C4D5E6F7")),
            Patience,
            Ct);

        var measurement = refused.LastMeasurement.ShouldNotBeNull();
        measurement.Value.ShouldBe(1);
        measurement.MatchesTags(
                Tag(ChargingMetrics.AggregateTag, ChargePointActor.TypeName),
                Tag(ChargingMetrics.CommandTag, nameof(ReserveConnector)),
                Tag(ChargingMetrics.ReasonTag, "NotRegistered"))
            .ShouldBeTrue();
    }

    [Fact]
    public void Given_two_hosts_in_one_process_then_each_collector_sees_only_its_own()
    {
        using var other = Host();
        using var here = new MetricCollector<long>(
            Meters, ChargingDiagnostics.MeterName, ChargingMetrics.SessionsStarted);
        using var there = new MetricCollector<long>(
            other.GetRequiredService<IMeterFactory>(),
            ChargingDiagnostics.MeterName,
            ChargingMetrics.SessionsStarted);

        _host.GetRequiredService<ChargingMetrics>().SessionStarted();
        _host.GetRequiredService<ChargingMetrics>().SessionStarted();

        here.GetMeasurementSnapshot().EvaluateAsCounter().ShouldBe(2);
        there.GetMeasurementSnapshot().ShouldBeEmpty();
    }

    [Fact]
    public void Given_the_energy_histogram_then_its_boundaries_are_part_of_the_instrument()
    {
        using var delivered = new MetricCollector<double>(
            Meters, ChargingDiagnostics.MeterName, ChargingMetrics.EnergyDelivered);

        _host.GetRequiredService<ChargingMetrics>().SessionEnded(new Energy(37.5m));

        delivered.LastMeasurement.ShouldNotBeNull().Value.ShouldBe(37.5);
        var histogram = delivered.Instrument.ShouldBeOfType<Histogram<double>>();
        histogram.Unit.ShouldBe("kWh");
        histogram.Advice.ShouldNotBeNull().HistogramBucketBoundaries
            .ShouldBe([1, 2, 5, 10, 20, 30, 40, 60, 80, 120]);
    }

    [Fact]
    public async Task Given_an_observable_gauge_then_it_has_a_value_only_once_it_is_observed()
    {
        using var utilization = new MetricCollector<double>(
            Meters, SiteEnergyDiagnostics.MeterName, SiteEnergyMetrics.AllocationUtilization);
        var budget = Sys.ActorOf(SitePowerBudgetActor.Props(
            Depot, _clock, _host.GetRequiredService<SiteEnergyMetrics>()));
        await budget.Ask<BudgetCommandAccepted>(Configure(), Patience, Ct);
        await budget.Ask<PowerAllocated>(Request(), Patience, Ct);

        utilization.GetMeasurementSnapshot().ShouldBeEmpty();
        utilization.RecordObservableInstruments();

        // 50 kW of the depot's 190 (a 200 kW connection less its 10 kW margin).
        utilization.LastMeasurement.ShouldNotBeNull().Value.ShouldBe(50 / 190.0, 1e-9);
    }

    [Fact]
    public async Task Given_a_measurement_that_comes_later_then_the_test_waits_for_it()
    {
        var shell = new ActorShellMetrics(Meters);
        using var deadLetters = new MetricCollector<long>(
            Meters, ActorShellDiagnostics.MeterName, ActorShellMetrics.DeadLetters);
        var listener = Sys.ActorOf(Props.Create(() => new DeadLetterMetricsListener(shell)));
        await listener.Ask<ActorIdentity>(new Identify(null), Patience, Ct);

        var gone = Sys.ActorOf(Props.Empty);
        await WatchAsync(gone);
        Sys.Stop(gone);
        await ExpectTerminatedAsync(gone, cancellationToken: Ct);

        gone.Tell(new GetChargePointStatus(Charger));

        await deadLetters.WaitForMeasurementsAsync(1, Patience);
        deadLetters.LastMeasurement.ShouldNotBeNull()
            .ContainsTags(Tag(ActorShellMetrics.MessageTypeTag, nameof(GetChargePointStatus)))
            .ShouldBeTrue();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _host.Dispose();
        }

        base.Dispose(disposing);
    }

    private static KeyValuePair<string, object?> Tag(string key, object? value) =>
        new(key, value);

    /// <summary>A host's container as far as metrics go: its factory and instruments.</summary>
    private static ServiceProvider Host() =>
        new ServiceCollection()
            .AddMetrics()
            .AddSingleton<ChargingMetrics>()
            .AddSingleton<SiteEnergyMetrics>()
            .BuildServiceProvider();

    private ConfigureSite Configure() => new(
        CommandId.New(_clock),
        Depot,
        new Power(200m),
        new Power(10m),
        "America/Denver",
        [
            new ConnectorLimits(
                new ConnectorId(Charger, 1), new Power(5m), new Power(50m), new Public()),
        ]);

    private RequestAllocation Request() => new(
        CommandId.New(_clock),
        Depot,
        new ConnectorId(Charger, 1),
        new SessionId(Guid.CreateVersion7(Morning)),
        new Power(50m));

    private static SerializerDetails ChargingSerializer(ExtendedActorSystem system) =>
        SerializerDetails.Create(
            Charging.Actors.Serialization.ChargingSerializer.Name,
            new ChargingSerializer(system),
            [.. Charging.Actors.Serialization.ChargingSerializer.BoundTypes]);

    private static SerializerDetails SiteEnergySerializer(ExtendedActorSystem system) =>
        SerializerDetails.Create(
            SiteEnergy.Actors.Serialization.SiteEnergySerializer.Name,
            new SiteEnergySerializer(system),
            [.. SiteEnergy.Actors.Serialization.SiteEnergySerializer.BoundTypes]);
}
