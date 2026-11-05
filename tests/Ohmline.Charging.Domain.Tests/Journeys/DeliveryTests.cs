using Ohmline.Charging.Domain.Journeys;
using Ohmline.SharedKernel.Deciders;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Contracts;
using static Ohmline.Charging.Domain.Tests.Journeys.TheJourney;
using static Ohmline.Charging.Domain.Tests.Known;

namespace Ohmline.Charging.Domain.Tests.Journeys;

/// <summary>
/// At-most-once delivery, made safe (section 15.6): every request carries a key derived from
/// the journey and the step, so the journey can ask again when no answer comes, and give back
/// what it asked for even when it never heard whether it got it.
/// </summary>
public sealed class DeliveryTests
{
    private static readonly JourneyTimerElapsed ReplyTimer = new(JourneyTimer.Reply);

    [Fact]
    public void A_key_is_the_journey_and_the_step() =>
        KeyFor(IdempotencyKey.Hold).Value.ShouldBe($"{Journey}-hold");

    [Fact]
    public void A_key_is_the_same_command_id_every_time_and_another_step_is_another()
    {
        var hold = KeyFor(IdempotencyKey.Hold);

        hold.CommandId.ShouldBe(KeyFor(IdempotencyKey.Hold).CommandId);
        hold.CommandId.ShouldNotBe(KeyFor(IdempotencyKey.Lock).CommandId);
        hold.CommandId.Value.Version.ShouldBe(7);
    }

    [Fact]
    public void Given_a_whole_journey_then_each_kind_of_request_has_a_key_of_its_own()
    {
        var keys = new[]
            {
                When(0, Start),
                When(1, Locked),
                When(2, Placed),
                When(3, new PowerAllocated(Depot, Connector, JourneySession, new Power(22m), 7)),
                When(5, SessionOver),
            }
            .SelectMany(step => step.Effects)
            .Select(KeyOf)
            .OfType<IdempotencyKey>()
            .Select(key => key.Step);

        keys.ShouldBe(["lock", "hold", "allocation", "charger", "capture"]);
    }

    [Fact]
    public void Given_a_step_without_an_answer_when_its_reply_timer_elapses_then_it_asks_again()
    {
        var step = ChargingJourney.Next(After(1), ReplyTimer, At(Minute(0).AddSeconds(10)));

        step.Events.ShouldBeEmpty();
        step.State.ShouldBe(After(1));
        step.Effects.ShouldBe([AskForLock, AwaitReply]);
    }

    [Fact]
    public void Given_a_step_without_an_answer_for_a_minute_then_it_gives_back_what_it_asked_for()
    {
        // Waiting for the hold since 07:53; no answer by 07:54.
        var step = ChargingJourney.Next(After(2), ReplyTimer, At(Minute(2)));

        step.Events.ShouldBe(
        [
            new JourneyAbandoned(
                    Journey,
                    AbandonReasons.NoReply,
                    "No answer within 1 minute, asked every 10 seconds.")
                .At(Minute(2)),
        ]);
        step.Effects.ShouldBe([VoidTheHold, UnlockTheToken]);
    }

    [Fact]
    public void Given_a_journey_past_its_step_when_a_stale_reply_timer_elapses_then_no_change()
    {
        var step = When(5, ReplyTimer);

        step.Events.ShouldBeEmpty();
        step.Effects.ShouldBeEmpty();
    }

    private static DecisionContext At(DateTimeOffset now) => new(now);

    private static IdempotencyKey? KeyOf(JourneyEffect effect) => effect switch
    {
        JourneyEffect.LockToken request => request.Key,
        JourneyEffect.PlaceHold request => request.Key,
        JourneyEffect.RequestAllocation request => request.Key,
        JourneyEffect.StartCharger request => request.Key,
        JourneyEffect.CapturePayment request => request.Key,
        JourneyEffect.ReleaseAllocation request => request.Key,
        JourneyEffect.UnlockToken request => request.Key,
        JourneyEffect.VoidHold request => request.Hold,
        JourneyEffect.StartTimer or JourneyEffect.CancelTimer => null,
        null => null,
    };
}
