using Akka.Actor;
using Akka.TestKit;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.Diagnostics;
using Ohmline.Charging.Actors.Journeys;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Journeys;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.Testing;
using Ohmline.Testing.Metrics;
using static Ohmline.Charging.Actors.Tests.Journeys.TheJourney;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.Journeys;

/// <summary>
/// The service level indicator of section 18.9, measured by the journey from its own events:
/// how long a driver waits from "start charging" to energy flowing, or to the journey giving
/// up. Time is virtual, as in <see cref="ChargingJourneyActorTests"/>.
/// </summary>
public sealed class TimeToEnergyFlowTests : PersistenceShellSpec
{
    private readonly FakeTimeProvider _clock = new(Morning);
    private readonly TestMeterFactory _meters = new();
    private readonly InstrumentRecorder _recorded;
    private readonly ChargingMetrics _metrics;

    public TimeToEnergyFlowTests(ITestOutputHelper output)
        : base(
            ActorTestConfig.With(
                ActorTestConfig.VirtualTime,
                Serializers.Charging,
                Serializers.DriverAccounts,
                Serializers.SiteEnergy),
            nameof(TimeToEnergyFlowTests),
            output)
    {
        _recorded = new InstrumentRecorder(_meters);
        _metrics = new ChargingMetrics(_meters);
    }

    private TestScheduler Scheduler => (TestScheduler)Sys.Scheduler;

    [Fact]
    public async Task Given_a_journey_that_recovered_when_its_session_starts_then_the_wait_counts()
    {
        var contexts = FakeContexts.SayingYes(
            this, FakeContexts.PlaceHoldThen(new HoldPlaced(HoldId, Hold)));
        var journey = await Shell(
                contexts.Journey(Journey, _clock, _metrics),
                ChargingJourneyActor.PersistenceIdOf(Journey))
            .Given(Started(by: Command), TokenSecured(), HoldSecured(), PowerSecured())
            .StartAsync();
        _clock.Advance(TimeSpan.FromSeconds(42));

        journey.Tell(SessionBegan(), TestActor);

        await ExpectMsgAsync(new CommandAccepted(5), cancellationToken: Ct);
        var waited = _recorded.Of(ChargingMetrics.TimeToEnergyFlow).ShouldHaveSingleItem();
        waited.Value.ShouldBe(42);
        waited.Tags[ChargingMetrics.OutcomeTag].ShouldBe(ChargingMetrics.EnergyFlowing);
    }

    [Fact]
    public async Task Given_a_charger_that_never_starts_then_the_wait_counts_with_its_reason()
    {
        var contexts = FakeContexts.SayingYes(
            this, FakeContexts.PlaceHoldThen(new HoldPlaced(HoldId, Hold)));
        var journey = await Shell(
                contexts.Journey(Journey, _clock, _metrics),
                ChargingJourneyActor.PersistenceIdOf(Journey))
            .Given()
            .StartAsync();
        journey.Tell(Start(), TestActor);
        await ExpectMsgAsync(new CommandAccepted(1), cancellationToken: Ct);
        await AwaitAssertAsync(
            async () => (await ViewAsync(journey)).Step.ShouldBe(JourneyView.AwaitingEnergyFlow),
            cancellationToken: Ct);

        _clock.Advance(ChargingJourney.EnergyFlowTimeout);
        Scheduler.Advance(ChargingJourney.EnergyFlowTimeout);

        await AwaitAssertAsync(
            () =>
            {
                var waited = _recorded.Of(ChargingMetrics.TimeToEnergyFlow).ShouldHaveSingleItem();
                waited.Value.ShouldBe(ChargingJourney.EnergyFlowTimeout.TotalSeconds);
                waited.Tags[ChargingMetrics.OutcomeTag]
                    .ShouldBe(AbandonReasons.EnergyFlowTimedOut);
            },
            cancellationToken: Ct);
    }

    [Fact]
    public async Task Given_a_journey_whose_energy_already_flowed_when_it_recovers_then_nothing()
    {
        var journey = await Shell(
                FakeContexts.SayingYes(
                        this, FakeContexts.PlaceHoldThen(new HoldPlaced(HoldId, Hold)))
                    .Journey(Journey, _clock, _metrics),
                ChargingJourneyActor.PersistenceIdOf(Journey))
            .Given(
                Started(by: Command),
                TokenSecured(),
                HoldSecured(),
                PowerSecured(),
                Stamped(new EnergyFlowStarted(Journey, Serialization.ChargingSamples.Tariff)))
            .StartAsync();

        (await ViewAsync(journey)).Version.ShouldBe(5);

        _recorded.Of(ChargingMetrics.TimeToEnergyFlow).ShouldBeEmpty();
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

    private Task<JourneyView> ViewAsync(IActorRef journey) =>
        journey.Ask<JourneyView>(new GetJourney(Journey), PortCall.Timeout, Ct);
}
