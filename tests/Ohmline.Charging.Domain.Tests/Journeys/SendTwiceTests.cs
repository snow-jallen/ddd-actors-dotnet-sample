using Ohmline.Charging.Domain.Journeys;
using Ohmline.SharedKernel.Identity;
using static Ohmline.Charging.Domain.Tests.Journeys.TheJourney;
using static Ohmline.Charging.Domain.Tests.Known;

namespace Ohmline.Charging.Domain.Tests.Journeys;

/// <summary>
/// A start sent twice (section 19.3): the app cannot tell a start that was lost from one whose
/// answer was lost, so it sends the same command again, and the journey must not start twice.
/// </summary>
public sealed class SendTwiceTests
{
    /// <summary>Steps taken: started, token locked, hold placed, charging, settling.</summary>
    public static TheoryData<int> StepsTaken => new(1, 2, 3, 5, 6);

    [Fact]
    public void Given_no_journey_when_it_starts_then_it_is_accepted_at_its_new_version() =>
        When(0, Start).Answer.Value.ShouldBeOfType<AcceptedNow>();

    [Theory]
    [MemberData(nameof(StepsTaken))]
    public void Given_a_started_journey_when_the_same_start_arrives_then_it_is_answered_as_before(
        int steps)
    {
        var step = When(steps, Start);

        step.Events.ShouldBeEmpty();
        step.Effects.ShouldBeEmpty();
        step.State.ShouldBe(After(steps));
        step.Answer.Value.ShouldBeOfType<AcceptedAsBefore>();
    }

    [Theory]
    [MemberData(nameof(StepsTaken))]
    public void Given_a_started_journey_when_another_command_starts_it_then_it_is_refused(
        int steps)
    {
        var another = Start with { CommandId = new CommandId(Guid.CreateVersion7(Morning)) };

        var step = When(steps, another);

        step.Events.ShouldBeEmpty();
        step.Effects.ShouldBeEmpty();
        step.Answer.Value.ShouldBe(new Refused(
            JourneyRefusals.JourneyExists, $"Journey {Journey} was started by another command."));
    }

    [Fact]
    public void Given_an_abandoned_journey_when_the_same_start_arrives_then_it_stays_abandoned()
    {
        var abandoned = ChargingJourney.Fold(
        [
            .. History(1),
            new JourneyAbandoned(Journey, AbandonReasons.TokenRefused, "Blocked.").At(Minute(1)),
        ]);

        var step = ChargingJourney.Next(abandoned, Start, new(Minute(2)));

        step.Events.ShouldBeEmpty();
        step.Answer.Value.ShouldBeOfType<AcceptedAsBefore>();
    }
}
