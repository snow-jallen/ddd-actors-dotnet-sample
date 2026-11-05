using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.TestKit;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.Journeys;
using Ohmline.Charging.Actors.Tests.Serialization;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Journeys;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Journeys.TheJourney;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.Journeys;

/// <summary>
/// The journey among fake contexts (section 15.4): Driver Accounts, Site Energy, the payment
/// side and the charge point are test probes whose autopilots answer as those contexts would.
/// The journey cannot tell; the test sees every request it made, in order.
/// </summary>
public sealed class FakeContextTests(ITestOutputHelper output)
    : PersistenceShellSpec(
        ActorTestConfig.With(
            Serializers.Charging, Serializers.DriverAccounts, Serializers.SiteEnergy),
        nameof(FakeContextTests),
        output)
{
    private readonly FakeTimeProvider _clock = new(Morning);

    [Fact]
    public async Task Given_contexts_that_say_yes_then_a_journey_runs_to_a_settled_payment()
    {
        var contexts = Contexts(PlaceHoldThen(new HoldPlaced(HoldId, Hold)));
        var shard = CreateTestProbe();
        var journey = await Journey(contexts).StartUnderAsync(shard);

        journey.Tell(Start(), TestActor);
        await ExpectMsgAsync(new CommandAccepted(1), cancellationToken: Ct);
        await StepAsync(journey, JourneyView.AwaitingEnergyFlow);
        journey.Tell(SessionBegan(), TestActor);
        await ExpectMsgAsync(new CommandAccepted(5), cancellationToken: Ct);
        journey.Tell(SessionOver(new Energy(30m)), TestActor);
        await ExpectMsgAsync(new CommandAccepted(6), cancellationToken: Ct);

        (await shard.ExpectMsgAsync<Passivate>(cancellationToken: Ct)).ShouldNotBeNull();
        (await JournalProbe.EventsAsync<JourneyEvent>(Sys, PersistenceId, Ct))
            .Select(@event => @event.GetType().Name)
            .ShouldBe(
            [
                "JourneyStarted",
                "TokenSecured",
                "HoldSecured",
                "PowerSecured",
                "EnergyFlowStarted",
                "SettlementDue",
                "JourneyCompleted",
            ]);
        // 30 kWh at the sample tariff's highest energy rate, USD 0.62, and a USD 1.00 fee.
        (await contexts.Payments.ExpectMsgAsync<PortCall.PlaceHold>(cancellationToken: Ct))
            .Amount.ShouldBe(Hold);
        (await contexts.Payments.ExpectMsgAsync<PortCall.Capture>(cancellationToken: Ct))
            .ShouldBe(new PortCall.Capture(
                IdempotencyKey.Of(TheJourney.Journey, "capture"),
                HoldId,
                new Money(19.60m, Currency.Usd)));
        (await contexts.Chargers.ExpectMsgAsync<PortCall.RemoteStart>(cancellationToken: Ct))
            .Limit.ShouldBe(new Power(22m));
    }

    [Fact]
    public async Task Given_a_card_that_is_declined_then_the_token_is_unlocked_and_it_gives_up()
    {
        var contexts = Contexts(PlaceHoldThen(new HoldDeclined("insufficient-funds", "No.")));
        var shard = CreateTestProbe();
        var journey = await Journey(contexts).StartUnderAsync(shard);

        journey.Tell(Start(), TestActor);
        await ExpectMsgAsync(new CommandAccepted(1), cancellationToken: Ct);

        await contexts.Tokens.ExpectMsgAsync<LockForSession>(cancellationToken: Ct);
        (await contexts.Tokens.ExpectMsgAsync<UnlockFromSession>(cancellationToken: Ct))
            .SessionId.ShouldBe(JourneySession);
        (await shard.ExpectMsgAsync<Passivate>(cancellationToken: Ct)).ShouldNotBeNull();
        await contexts.Budgets.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), Ct);
    }

    [Fact]
    public async Task Given_a_capture_that_fails_then_the_attempt_is_stored_and_retried_later()
    {
        var failed = new CaptureFailed(HoldId, "provider-unavailable", "No answer.");
        var contexts = Contexts(call => call switch
        {
            PortCall.PlaceHold => new HoldPlaced(HoldId, Hold),
            _ => failed,
        });
        var journey = await Journey(contexts).StartAsync();
        journey.Tell(Start(), TestActor);
        await ExpectMsgAsync(new CommandAccepted(1), cancellationToken: Ct);
        await StepAsync(journey, JourneyView.AwaitingEnergyFlow);
        journey.Tell(SessionBegan(), TestActor);
        await ExpectMsgAsync(new CommandAccepted(5), cancellationToken: Ct);

        journey.Tell(SessionOver(new Energy(30m)), TestActor);
        await ExpectMsgAsync(new CommandAccepted(6), cancellationToken: Ct);

        await AwaitAssertAsync(
            async () => (await JournalProbe.EventsAsync<JourneyEvent>(Sys, PersistenceId, Ct))
                .Last().ShouldBeOfType<CaptureAttemptFailed>().Attempt.ShouldBe(1),
            cancellationToken: Ct);
        await StepAsync(journey, JourneyView.Settling);
        await contexts.Payments.ExpectMsgAsync<PortCall.PlaceHold>(cancellationToken: Ct);
        await contexts.Payments.ExpectMsgAsync<PortCall.Capture>(cancellationToken: Ct);
        // The next attempt waits for its timer, 30 seconds away; section 15.7 moves time on.
        await contexts.Payments.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), Ct);
    }

    private static string PersistenceId => ChargingJourneyActor.PersistenceIdOf(TheJourney.Journey);

    private PersistenceShellSpec.Scenario Journey(FakeContexts contexts) =>
        Shell(contexts.Journey(TheJourney.Journey, _clock), PersistenceId);

    private FakeContexts Contexts(Func<PortCall, PaymentPortResult> payments) =>
        FakeContexts.SayingYes(this, payments);

    private static Func<PortCall, PaymentPortResult> PlaceHoldThen(PaymentPortResult hold) =>
        FakeContexts.PlaceHoldThen(hold);

    private async Task StepAsync(IActorRef journey, string step) =>
        await AwaitAssertAsync(
            async () => (await journey.Ask<JourneyView>(
                    new GetJourney(TheJourney.Journey), PortCall.Timeout, Ct))
                .Step.ShouldBe(step),
            cancellationToken: Ct);
}
