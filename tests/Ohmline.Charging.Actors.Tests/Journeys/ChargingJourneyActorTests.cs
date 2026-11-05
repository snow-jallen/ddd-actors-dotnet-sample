using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.TestKit;
using Microsoft.Extensions.Time.Testing;
using Ohmline.ActorShell.Sharding;
using Ohmline.Charging.Actors.Journeys;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Journeys;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Journeys.TheJourney;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.Journeys;

/// <summary>
/// The journey's shell: what it stores, when it acts, and what it does again after a restart.
/// Driver Accounts and Site Energy are test probes in place of their shard regions; the ports
/// are stubs, or probes with autopilots. The steps themselves are the pure machine's tests.
/// </summary>
/// <remarks>
/// Time is virtual (section 15.7): the actor's timers run on Akka's <see cref="TestScheduler"/>
/// and its decisions read a <see cref="FakeTimeProvider"/>, and a test moves both together. A
/// timer elapses only when the test says so, never because the machine was slow. A test that
/// moves time first waits until the actor has started the timer: a question it must answer
/// after the message that started it is the barrier. Never with clustering: the scheduler
/// drives gossip and failure detection too.
/// </remarks>
public sealed class ChargingJourneyActorTests(ITestOutputHelper output)
    : PersistenceShellSpec(
        ActorTestConfig.With(
            ActorTestConfig.VirtualTime,
            Serializers.Charging,
            Serializers.DriverAccounts,
            Serializers.SiteEnergy),
        nameof(ChargingJourneyActorTests),
        output)
{
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(300);

    private readonly FakeTimeProvider _clock = new(Morning);

    private TestScheduler Scheduler => (TestScheduler)Sys.Scheduler;

    [Fact]
    public void The_persistence_id_is_the_durable_type_name_and_the_journey_id() =>
        ChargingJourneyActor.PersistenceIdOf(Journey).ShouldBe($"charging-journey-{Journey}");

    [Fact]
    public async Task Given_no_journey_when_charging_starts_then_it_is_stored_and_the_token_asked()
    {
        var (tokens, journey) = JourneyWith(new StubPaymentPort());
        var start = Start();

        await journey.Given()
            .When(start)
            .ThenAsync(new CommandAccepted(1), Started(by: start.CommandId));

        var lockFor = await tokens.ExpectMsgAsync<LockForSession>(cancellationToken: Ct);
        lockFor.TokenUid.ShouldBe(DriversCard);
        lockFor.SessionId.ShouldBe(JourneySession);
    }

    [Fact]
    public async Task Given_a_journal_that_fails_the_write_then_nobody_is_asked_anything()
    {
        var (tokens, journey) = JourneyWith(new StubPaymentPort());

        await journey.Given()
            .When(Start())
            .WithJournalWrite(write => write.Fail())
            .ThenStopsAsync();

        await tokens.ExpectNoMsgAsync(Quiet, Ct);
    }

    [Fact]
    public async Task Given_a_locked_token_and_a_placed_hold_then_the_site_is_asked_for_power()
    {
        var budgets = CreateTestProbe();
        var payments = new StubPaymentPort(hold: new HoldPlaced(HoldId, Hold));
        var (tokens, journey) = JourneyWith(payments, budgets);
        var actor = await journey.Given(Started(by: Command)).StartAsync();
        await tokens.ExpectMsgAsync<LockForSession>(cancellationToken: Ct); // Asked again.

        actor.Tell(new TokenLocked(DriversCard, JourneySession, Driver, Version: 2), tokens.Ref);

        var request = await budgets.ExpectMsgAsync<RequestAllocation>(cancellationToken: Ct);
        request.SiteId.ShouldBe(Depot);
        request.ConnectorId.ShouldBe(Connector);
        request.SessionId.ShouldBe(JourneySession);
        request.Requested.ShouldBe(new Power(22m));
        budgets.LastSender.ShouldBe(actor);
        payments.Asked.ShouldBe([$"hold {Hold} for {Driver}"]);
        (await JournalProbe.EventsAsync<JourneyEvent>(
                Sys, ChargingJourneyActor.PersistenceIdOf(Journey), Ct))
            .Select(@event => @event.GetType())
            .ShouldBe([typeof(JourneyStarted), typeof(TokenSecured), typeof(HoldSecured)]);
    }

    [Fact]
    public async Task Given_a_journey_waiting_for_its_hold_when_it_recovers_then_it_asks_again()
    {
        var payments = new StubPaymentPort();
        var (tokens, journey) = JourneyWith(payments);

        await journey.Given(Started(by: Command), TokenSecured()).StartAsync();

        await AwaitAssertAsync(
            () => payments.Asked.ShouldBe([$"hold {Hold} for {Driver}"]),
            cancellationToken: Ct);
        await tokens.ExpectNoMsgAsync(Quiet, Ct);
    }

    [Fact]
    public async Task Given_a_journey_when_asked_then_it_tells_its_step_and_session() =>
        await JourneyWith(new StubPaymentPort()).Journey
            .Given(Started(by: Command), TokenSecured())
            .When(new GetJourney(Journey))
            .ThenAsync(new JourneyView(Journey, 2, JourneyView.AwaitingHold, JourneySession));

    [Fact]
    public async Task Given_a_journey_waiting_for_energy_when_its_session_starts_then_it_answers()
    {
        var started = new SessionStarted(
                JourneySession,
                Depot,
                Connector,
                DriversCard,
                Serialization.ChargingSamples.Tariff,
                Hold,
                MeterStartWh: 1_200_000)
            with { CommandId = Command, OccurredAt = Morning };

        await JourneyWith(new StubPaymentPort()).Journey
            .Given(Started(by: Command), TokenSecured(), HoldSecured(), PowerSecured())
            .When(started)
            .ThenAsync(
                new CommandAccepted(5),
                Stamped(new EnergyFlowStarted(Journey, Serialization.ChargingSamples.Tariff)));
    }

    [Fact]
    public async Task Given_a_journey_that_is_abandoned_then_it_gives_back_and_passivates_itself()
    {
        var shard = CreateTestProbe();
        var payments = new StubPaymentPort(hold: new HoldDeclined("insufficient-funds", "No."));
        var (tokens, journey) = JourneyWith(payments);

        await journey.Given(Started(by: Command), TokenSecured()).StartUnderAsync(shard);

        var unlock = await tokens.ExpectMsgAsync<UnlockFromSession>(cancellationToken: Ct);
        unlock.SessionId.ShouldBe(JourneySession);
        (await shard.ExpectMsgAsync<Passivate>(cancellationToken: Ct))
            .StopMessage.ShouldBe(Stop.Instance);
    }

    [Fact]
    public async Task Given_a_charger_told_to_start_when_two_minutes_pass_then_all_is_given_back()
    {
        var contexts = FakeContexts.SayingYes(
            this, FakeContexts.PlaceHoldThen(new HoldPlaced(HoldId, Hold)));
        var journey = await AwaitingEnergyAsync(contexts);

        Advance(ChargingJourney.EnergyFlowTimeout);

        (await contexts.Budgets.FishForMessageAsync<ReleaseAllocation>(
                _ => true, cancellationToken: Ct))
            .SessionId.ShouldBe(JourneySession);
        (await contexts.Payments.FishForMessageAsync<PortCall.Void>(
                _ => true, cancellationToken: Ct))
            .Hold.ShouldBe(IdempotencyKey.Of(Journey, IdempotencyKey.Hold));
        (await contexts.Tokens.FishForMessageAsync<UnlockFromSession>(
                _ => true, cancellationToken: Ct))
            .SessionId.ShouldBe(JourneySession);
        (await ViewAsync(journey)).AbandonReason.ShouldBe(AbandonReasons.EnergyFlowTimedOut);
    }

    [Fact]
    public async Task Given_a_charger_told_to_start_when_less_than_two_minutes_pass_it_still_waits()
    {
        var contexts = FakeContexts.SayingYes(
            this, FakeContexts.PlaceHoldThen(new HoldPlaced(HoldId, Hold)));
        var journey = await AwaitingEnergyAsync(contexts);

        Advance(ChargingJourney.EnergyFlowTimeout - TimeSpan.FromSeconds(1));

        (await ViewAsync(journey)).Step.ShouldBe(JourneyView.AwaitingEnergyFlow);
    }

    [Fact]
    public async Task Given_a_journey_waiting_for_energy_when_it_restarts_then_its_timer_runs_on()
    {
        var contexts = FakeContexts.SayingYes(
            this, FakeContexts.PlaceHoldThen(new HoldPlaced(HoldId, Hold)));
        var journey = await AwaitingEnergyAsync(contexts);
        Advance(TimeSpan.FromMinutes(1));
        await WatchAsync(journey);
        Sys.Stop(journey);
        await ExpectTerminatedAsync(journey, cancellationToken: Ct);

        // A new incarnation recovers, and starts the energy-flow timer for the minute left.
        var restarted = Sys.ActorOf(contexts.Journey(Journey, _clock));
        (await ViewAsync(restarted)).Step.ShouldBe(JourneyView.AwaitingEnergyFlow);
        Advance(TimeSpan.FromSeconds(59));
        (await ViewAsync(restarted)).Step.ShouldBe(JourneyView.AwaitingEnergyFlow);
        Advance(TimeSpan.FromSeconds(1));

        await AwaitAssertAsync(
            async () => (await ViewAsync(restarted)).Step.ShouldBe(JourneyView.Abandoned),
            cancellationToken: Ct);
    }

    [Fact]
    public async Task Given_a_lock_without_an_answer_when_ten_seconds_pass_then_it_asks_again()
    {
        var (tokens, scenario) = JourneyWith(new StubPaymentPort()); // Driver Accounts is silent.
        var journey = await scenario.StartAsync();
        journey.Tell(Start(), TestActor);
        await ExpectMsgAsync(new CommandAccepted(1), cancellationToken: Ct);
        var first = await tokens.ExpectMsgAsync<LockForSession>(cancellationToken: Ct);
        await journey.Ask<ActorIdentity>(new Identify(1), PortCall.Timeout, Ct); // The barrier.

        Advance(ChargingJourney.ReplyTimeout);

        (await tokens.ExpectMsgAsync<LockForSession>(cancellationToken: Ct)).ShouldBe(first);
        first.CommandId.ShouldBe(IdempotencyKey.Of(Journey, IdempotencyKey.Lock).CommandId);
    }

    [Fact]
    public async Task Given_a_failed_capture_when_its_pause_is_over_then_it_is_captured_again()
    {
        var captures = 0;
        var contexts = FakeContexts.SayingYes(this, call => call switch
        {
            PortCall.Capture capture when Interlocked.Increment(ref captures) == 1 =>
                new CaptureFailed(capture.HoldId, "provider-unavailable", "No answer."),
            _ => FakeContexts.PlaceHoldThen(new HoldPlaced(HoldId, Hold))(call),
        });
        var journey = await AwaitingEnergyAsync(contexts);
        journey.Tell(SessionBegan(), TestActor);
        await ExpectMsgAsync(new CommandAccepted(5), cancellationToken: Ct);
        journey.Tell(SessionOver(new Energy(30m)), TestActor);
        await ExpectMsgAsync(new CommandAccepted(6), cancellationToken: Ct);
        await AwaitAssertAsync(
            async () => (await ViewAsync(journey)).Version.ShouldBe(7), // The failed attempt.
            cancellationToken: Ct);

        Advance(ChargingJourney.CaptureBackoff(1));

        await AwaitAssertAsync(
            async () => (await ViewAsync(journey)).Step.ShouldBe(JourneyView.Completed),
            cancellationToken: Ct);
        captures.ShouldBe(2);
    }

    /// <summary>
    /// A journey among fake contexts that say yes, started and waiting for energy: its
    /// energy-flow timer runs from the fixed morning.
    /// </summary>
    private async Task<IActorRef> AwaitingEnergyAsync(FakeContexts contexts)
    {
        var journey = await Shell(contexts.Journey(Journey, _clock), PersistenceIdOf())
            .Given()
            .StartAsync();
        journey.Tell(Start(), TestActor);
        await ExpectMsgAsync(new CommandAccepted(1), cancellationToken: Ct);
        await AwaitAssertAsync(
            async () => (await ViewAsync(journey)).Step.ShouldBe(JourneyView.AwaitingEnergyFlow),
            cancellationToken: Ct);
        return journey;
    }

    private Task<JourneyView> ViewAsync(IActorRef journey) =>
        journey.Ask<JourneyView>(new GetJourney(Journey), PortCall.Timeout, Ct);

    /// <summary>Moves the decisions' clock and the actor's timers on together.</summary>
    private void Advance(TimeSpan by)
    {
        _clock.Advance(by);
        Scheduler.Advance(by);
    }

    private static string PersistenceIdOf() => ChargingJourneyActor.PersistenceIdOf(Journey);

    private (TestProbe Tokens, PersistenceShellSpec.Scenario Journey) JourneyWith(
        StubPaymentPort payments, TestProbe? budgets = null)
    {
        var tokens = CreateTestProbe();
        var scenario = Shell(
            Props(tokens, budgets ?? CreateTestProbe(), payments),
            ChargingJourneyActor.PersistenceIdOf(Journey));
        return (tokens, scenario);
    }

    private Props Props(TestProbe tokens, TestProbe budgets, StubPaymentPort payments) =>
        ChargingJourneyActor.Props(
            Journey,
            _clock,
            new Region<IdTokenRegion>(tokens.Ref),
            new Region<SitePowerBudgetRegion>(budgets.Ref),
            payments,
            new StubChargerPort());
}
