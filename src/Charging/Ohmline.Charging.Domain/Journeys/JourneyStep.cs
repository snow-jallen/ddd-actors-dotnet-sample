using System.Collections.Immutable;

namespace Ohmline.Charging.Domain.Journeys;

/// <summary>
/// One step of a journey: the state it is in now, the events that record the step (none when
/// nothing changed), and the effects the shell carries out once those events are stored.
/// </summary>
public sealed record JourneyStep(
    JourneyState State,
    ImmutableArray<JourneyEvent> Events,
    ImmutableArray<JourneyEffect> Effects);
