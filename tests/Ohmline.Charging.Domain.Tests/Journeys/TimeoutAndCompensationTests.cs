using Ohmline.Charging.Domain.Journeys;
using Ohmline.SharedKernel.Deciders;
using Ohmline.SharedKernel.Monetary;
using static Ohmline.Charging.Domain.Tests.Journeys.TheJourney;
using static Ohmline.Charging.Domain.Tests.Known;
using ChargingState = Ohmline.Charging.Domain.Journeys.Charging;

namespace Ohmline.Charging.Domain.Tests.Journeys;

/// <summary>
/// When something does not happen (section 15.5): energy that never flows, a capture that keeps
/// failing. Each is decided like any input, and each compensation is a recorded fact.
/// </summary>
public sealed class TimeoutAndCompensationTests
{
    private static readonly Money Twelve = new(12m, Currency.Usd);

    private static readonly JourneyTimerElapsed EnergyFlowTimer = new(JourneyTimer.EnergyFlow);

    private static readonly JourneyTimerElapsed RetryTimer = new(JourneyTimer.CaptureRetry);

    private static readonly CaptureFailed Unavailable =
        new(HoldId, "provider-unavailable", "The payment provider did not answer.");

    [Fact]
    public void Given_a_charger_told_to_start_when_no_energy_flows_in_time_then_all_is_given_back()
    {
        var step = When(4, EnergyFlowTimer);

        step.Events.ShouldBe(
        [
            new JourneyAbandoned(
                    Journey,
                    AbandonReasons.EnergyFlowTimedOut,
                    "No energy flowed within 2 minutes.")
                .At(Minute(4)),
        ]);
        step.Effects.ShouldBe(
        [
            new JourneyEffect.CancelTimer(JourneyTimer.EnergyFlow),
            ReleaseThePower,
            VoidTheHold,
            UnlockTheToken,
        ]);
    }

    [Fact]
    public void Given_a_charging_session_when_a_stale_energy_timer_elapses_then_nothing_changes()
    {
        var step = When(5, EnergyFlowTimer);

        step.Events.ShouldBeEmpty();
        step.State.Value.ShouldBeOfType<ChargingState>();
        step.Effects.ShouldBeEmpty();
    }

    [Fact]
    public void Given_a_settling_journey_when_the_capture_fails_then_it_waits_and_tries_again()
    {
        var step = When(6, Unavailable);

        step.Events.ShouldBe(
        [
            new CaptureAttemptFailed(
                    Journey,
                    1,
                    "provider-unavailable",
                    "The payment provider did not answer.")
                .At(Minute(6)),
        ]);
        step.State.ShouldBe(new Settling(Terms, HoldId, Twelve, FailedCaptures: 1));
        step.Effects.ShouldBe(
            [new JourneyEffect.StartTimer(JourneyTimer.CaptureRetry, TimeSpan.FromSeconds(30))]);
    }

    [Fact]
    public void Given_a_failed_capture_when_the_retry_timer_elapses_then_it_is_captured_again()
    {
        var failedOnce = Settle(failures: 1);

        var step = ChargingJourney.Next(failedOnce, RetryTimer, At(7));

        step.Events.ShouldBeEmpty();
        step.State.ShouldBe(failedOnce);
        step.Effects.ShouldBe([Capture(Twelve)]);
    }

    [Theory]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(3, 120)]
    public void Given_failed_captures_then_each_pause_is_twice_the_last(int failed, int seconds) =>
        ChargingJourney.CaptureBackoff(failed).ShouldBe(TimeSpan.FromSeconds(seconds));

    [Fact]
    public void Given_three_failed_captures_when_the_fourth_fails_then_the_amount_is_owed()
    {
        var step = ChargingJourney.Next(Settle(failures: 3), Unavailable, At(9));

        step.Events.ShouldBe(
        [
            new DebtRecorded(
                    Journey,
                    Twelve,
                    "Capture failed 4 times; last: The payment provider did not answer.")
                .At(Minute(9)),
        ]);
        step.State.ShouldBe(new Completed(Terms, Money.Zero(Currency.Usd), Owed: Twelve));
        step.Effects.ShouldBeEmpty();
    }

    /// <summary>The settling journey after so many failed captures, one a minute.</summary>
    private static JourneyState Settle(int failures) =>
        ChargingJourney.Fold(
        [
            .. History(6),
            .. Enumerable.Range(1, failures).Select(attempt =>
                new CaptureAttemptFailed(Journey, attempt, Unavailable.Code, Unavailable.Message)
                    .At(Minute(5 + attempt))),
        ]);

    private static DecisionContext At(int minute) => new(Minute(minute));
}
