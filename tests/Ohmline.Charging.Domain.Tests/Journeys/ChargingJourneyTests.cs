using Ohmline.Charging.Domain.Journeys;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Contracts;
using static Ohmline.Charging.Domain.Tests.Journeys.TheJourney;
using static Ohmline.Charging.Domain.Tests.Known;
using ChargingState = Ohmline.Charging.Domain.Journeys.Charging;

namespace Ohmline.Charging.Domain.Tests.Journeys;

/// <summary>
/// The journey's steps, given the steps before, when it hears one input: then these events,
/// this state and these effects. Every effect is data, so every test is a plain comparison.
/// </summary>
public sealed class ChargingJourneyTests
{
    private static readonly Money Twelve = new(12m, Currency.Usd);

    [Fact]
    public void Given_nothing_when_charging_starts_then_the_start_records_everything_it_needs()
    {
        var step = When(0, Start);

        step.Events.ShouldBe([History(1)[0]]);
        step.Events[0].ShouldBe(new JourneyStarted(
                Journey, Command, JourneySession, DriversCard, Connector, Depot, MaxPower, Hold)
            .At(Minute(0)));
        step.State.ShouldBe(new AwaitingTokenLock(Terms, Minute(0)));
        step.Effects.ShouldBe([new JourneyEffect.LockToken(DriversCard, JourneySession)]);
    }

    [Fact]
    public void A_journeys_session_is_named_after_it() =>
        ChargingJourney.SessionOf(Journey).Value.ShouldBe(Journey.Value);

    [Fact]
    public void Given_a_started_journey_when_the_token_is_locked_then_its_driver_is_asked_to_hold()
    {
        var step = When(1, Locked);

        step.Events.ShouldBe([new TokenSecured(Journey, Driver).At(Minute(1))]);
        step.State.ShouldBe(new AwaitingHold(Terms, Driver, Minute(1)));
        step.Effects.ShouldBe([new JourneyEffect.PlaceHold(Driver, Hold)]);
    }

    [Fact]
    public void Given_a_started_journey_when_the_lock_is_refused_then_nothing_is_given_back()
    {
        var refused = new TokenLockRefused(
            DriversCard, JourneySession, "token-is-blocked", "Token 04A2B3C4D5E6F7 is blocked.");

        var step = When(1, refused);

        step.Events.ShouldBe(
        [
            new JourneyAbandoned(
                    Journey, AbandonReasons.TokenRefused, "Token 04A2B3C4D5E6F7 is blocked.")
                .At(Minute(1)),
        ]);
        step.State.Value.ShouldBeOfType<Abandoned>().From.ShouldBe(After(1));
        step.Effects.ShouldBeEmpty();
    }

    [Fact]
    public void Given_a_locked_token_when_the_hold_is_placed_then_the_site_is_asked_for_power()
    {
        var step = When(2, Placed);

        step.Events.ShouldBe([new HoldSecured(Journey, HoldId).At(Minute(2))]);
        step.State.ShouldBe(new AwaitingAllocation(Terms, HoldId, Minute(2)));
        step.Effects.ShouldBe(
            [new JourneyEffect.RequestAllocation(Depot, Connector, JourneySession, MaxPower)]);
    }

    [Fact]
    public void Given_a_locked_token_when_the_hold_is_declined_then_the_token_is_unlocked()
    {
        var step = When(2, new HoldDeclined("insufficient-funds", "The card was declined."));

        step.Events.ShouldBe(
        [
            new JourneyAbandoned(Journey, AbandonReasons.HoldDeclined, "The card was declined.")
                .At(Minute(2)),
        ]);
        step.Effects.ShouldBe([new JourneyEffect.UnlockToken(DriversCard, JourneySession)]);
    }

    [Fact]
    public void Given_a_hold_when_power_is_allocated_then_the_charger_starts_with_that_limit()
    {
        var allocated =
            new PowerAllocated(Depot, Connector, JourneySession, new Power(22m), Version: 7);

        var step = When(3, allocated);

        step.Events.ShouldBe([new PowerSecured(Journey, new Power(22m)).At(Minute(3))]);
        step.State.ShouldBe(new AwaitingEnergyFlow(Terms, HoldId, new Power(22m), Minute(3)));
        step.Effects.ShouldBe(
        [
            new JourneyEffect.StartCharger(Connector, DriversCard, JourneySession, new Power(22m)),
            new JourneyEffect.StartTimer(JourneyTimer.EnergyFlow, TimeSpan.FromMinutes(2)),
        ]);
    }

    [Fact]
    public void Given_a_hold_when_the_request_is_queued_then_the_charger_starts_with_no_power()
    {
        var step = When(3, new QueuedForPower(Depot, Connector, JourneySession, Version: 7));

        step.Events.ShouldBe([new PowerSecured(Journey, Power.Zero).At(Minute(3))]);
        step.Effects[0].ShouldBe(
            new JourneyEffect.StartCharger(Connector, DriversCard, JourneySession, Power.Zero));
    }

    [Fact]
    public void Given_a_hold_when_the_site_refuses_then_the_hold_is_voided_and_the_token_unlocked()
    {
        var refused = new BudgetCommandRejected(
            "connector-has-open-request", "Connector OHM-SLC-0042/1 already has a request.");

        var step = When(3, refused);

        step.Events.ShouldBe(
        [
            new JourneyAbandoned(
                    Journey,
                    AbandonReasons.AllocationRefused,
                    "Connector OHM-SLC-0042/1 already has a request.")
                .At(Minute(3)),
        ]);
        step.Effects.ShouldBe(
        [
            new JourneyEffect.VoidHold(HoldId),
            new JourneyEffect.UnlockToken(DriversCard, JourneySession),
        ]);
    }

    [Fact]
    public void Given_a_charger_told_to_start_when_it_accepts_then_the_journey_waits_for_the_car()
    {
        var step = When(4, new ChargerAccepted(Connector));

        step.Events.ShouldBeEmpty();
        step.State.ShouldBe(After(4));
        step.Effects.ShouldBeEmpty();
    }

    [Fact]
    public void Given_a_charger_told_to_start_when_it_rejects_then_everything_is_given_back()
    {
        var step = When(4, new ChargerRejected(Connector, "Occupied"));

        step.Events.ShouldBe(
        [
            new JourneyAbandoned(Journey, AbandonReasons.ChargerRejected, "Occupied")
                .At(Minute(4)),
        ]);
        step.Effects.ShouldBe(
        [
            new JourneyEffect.CancelTimer(JourneyTimer.EnergyFlow),
            new JourneyEffect.ReleaseAllocation(Depot, Connector, JourneySession),
            new JourneyEffect.VoidHold(HoldId),
            new JourneyEffect.UnlockToken(DriversCard, JourneySession),
        ]);
    }

    [Fact]
    public void Given_a_charger_told_to_start_when_the_session_starts_then_the_timer_is_cancelled()
    {
        var step = When(4, SessionBegan);

        step.Events.ShouldBe([new EnergyFlowStarted(Journey, StandardTariff(0.40m)).At(Minute(4))]);
        step.State.ShouldBe(new ChargingState(Terms, HoldId, StandardTariff(0.40m)));
        step.Effects.ShouldBe([new JourneyEffect.CancelTimer(JourneyTimer.EnergyFlow)]);
    }

    [Fact]
    public void Given_a_charging_session_when_it_ends_then_what_it_cost_is_captured()
    {
        var step = When(5, SessionOver);

        // 30 kWh at USD 0.40.
        step.Events.ShouldBe(
            [new SettlementDue(Journey, new Energy(30m), Twelve).At(Minute(5))]);
        step.State.ShouldBe(new Settling(Terms, HoldId, Twelve));
        step.Effects.ShouldBe([new JourneyEffect.CapturePayment(HoldId, Twelve)]);
    }

    [Fact]
    public void Given_a_settling_journey_when_the_payment_is_captured_then_it_is_complete()
    {
        var step = When(6, new PaymentCaptured(HoldId, Twelve));

        step.Events.ShouldBe([new JourneyCompleted(Journey, Twelve).At(Minute(6))]);
        step.State.ShouldBe(new Completed(Terms, Twelve));
        step.Effects.ShouldBeEmpty();
    }

    [Fact]
    public void Given_a_journey_when_a_reply_for_another_session_arrives_then_nothing_changes()
    {
        var other = new SessionId(Guid.CreateVersion7(Morning.AddHours(1)));

        var step = When(1, new TokenLocked(DriversCard, other, Driver, Version: 9));

        step.Events.ShouldBeEmpty();
        step.State.ShouldBe(After(1));
        step.Effects.ShouldBeEmpty();
    }

    [Fact]
    public void Given_a_journey_past_a_step_when_its_reply_comes_again_then_nothing_changes()
    {
        var step = When(5, Locked);

        step.Events.ShouldBeEmpty();
        step.Effects.ShouldBeEmpty();
    }

    [Fact]
    public void Given_a_started_journey_when_it_is_started_again_then_nothing_changes() =>
        When(1, Start).Events.ShouldBeEmpty();

    [Fact]
    public void Given_every_step_then_folding_its_events_gives_the_state_next_returned()
    {
        JourneyInput[] inputs =
        [
            Start,
            Locked,
            Placed,
            new PowerAllocated(Depot, Connector, JourneySession, new Power(22m), Version: 7),
            SessionBegan,
            SessionOver,
        ];

        for (var steps = 0; steps < inputs.Length; steps++)
        {
            var step = When(steps, inputs[steps]);

            step.Events.Aggregate(After(steps), ChargingJourney.Evolve).ShouldBe(step.State);
            step.State.ShouldBe(After(steps + 1));
        }
    }
}
