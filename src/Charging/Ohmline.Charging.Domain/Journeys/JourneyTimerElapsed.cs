namespace Ohmline.Charging.Domain.Journeys;

/// <summary>
/// A timer the journey started has run out: an input like any other, from the journey's own
/// shell. Told to its actor, so a record of plain data.
/// </summary>
public sealed record JourneyTimerElapsed(JourneyTimer Timer);
