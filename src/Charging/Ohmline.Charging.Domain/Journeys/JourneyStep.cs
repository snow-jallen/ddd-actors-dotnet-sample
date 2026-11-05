using System.Collections.Immutable;

namespace Ohmline.Charging.Domain.Journeys;

/// <summary>
/// One step of a journey: the state it is in now, the events that record the step (none when
/// nothing changed), the effects the shell carries out once those events are stored, and what
/// it answers whoever sent the input.
/// </summary>
public sealed record JourneyStep(
    JourneyState State,
    ImmutableArray<JourneyEvent> Events,
    ImmutableArray<JourneyEffect> Effects)
{
    /// <summary>Accepted at the version the step leaves it at, unless it says otherwise.</summary>
    public JourneyAnswer Answer { get; init; } = new AcceptedNow();
}
