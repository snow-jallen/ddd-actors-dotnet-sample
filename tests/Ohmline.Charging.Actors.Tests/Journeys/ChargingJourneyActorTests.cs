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
/// are stubs. The steps themselves are the pure machine's tests.
/// </summary>
public sealed class ChargingJourneyActorTests(ITestOutputHelper output)
    : PersistenceShellSpec(
        ActorTestConfig.With(
            Serializers.Charging, Serializers.DriverAccounts, Serializers.SiteEnergy),
        nameof(ChargingJourneyActorTests),
        output)
{
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(300);

    private readonly FakeTimeProvider _clock = new(Morning);

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
