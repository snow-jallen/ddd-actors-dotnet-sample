using System.Diagnostics.Metrics;
using Akka.Actor;
using Akka.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Ohmline.ActorShell.Telemetry;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Testing;
using Ohmline.Testing.Metrics;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.Diagnostics;

/// <summary>
/// The shell times every decider actor's recovery (section 18.2), on the meter of the factory
/// it finds in the actor system's container, as Akka.Hosting gives it one in a node.
/// </summary>
public sealed class RecoveryDurationTests : PersistenceShellSpec
{
    private readonly FakeTimeProvider _clock = new(Morning);
    private readonly TestMeterFactory _meters;
    private readonly InstrumentRecorder _recorded;

    public RecoveryDurationTests(ITestOutputHelper output)
        : this(new TestMeterFactory(), output)
    {
    }

    private RecoveryDurationTests(TestMeterFactory meters, ITestOutputHelper output)
        : base(
            ActorTestConfig.With(Serializers.Charging).And(DependencyResolverSetup.Create(
                new ServiceCollection().AddSingleton<IMeterFactory>(meters)
                    .BuildServiceProvider())),
            nameof(RecoveryDurationTests),
            output)
    {
        _meters = meters;
        _recorded = new InstrumentRecorder(meters);
    }

    [Fact]
    public async Task Given_a_charge_point_with_a_history_then_its_recovery_is_timed_by_type()
    {
        var chargePoint = await Shell(
                ChargePointActor.Props(Charger, _clock),
                ChargePointActor.PersistenceIdOf(Charger))
            .Given(Registered(), Reserved(1, DriversCard))
            .StartAsync();

        await chargePoint.Ask<ChargePointStatus>(
            Status(), TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        var recovery = _recorded.Of(ActorShellMetrics.RecoveryDuration).ShouldHaveSingleItem();
        recovery.Value.ShouldBeGreaterThan(0);
        recovery.Tags.ShouldBe(
            [new(ActorShellMetrics.ActorTypeTag, nameof(ChargePointActor))]);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _recorded.Dispose();
            _meters.Dispose();
        }

        base.Dispose(disposing);
    }
}
