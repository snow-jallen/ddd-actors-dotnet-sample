namespace Ohmline.Charging.Domain.Journeys;

/// <summary>The journey's timers: at most one of each runs at a time.</summary>
public enum JourneyTimer
{
    /// <summary>How long the charger has to start delivering energy once it is told to.</summary>
    EnergyFlow,

    /// <summary>How long to wait before trying a failed capture again.</summary>
    CaptureRetry,

    /// <summary>
    /// How long to wait for another context's answer before asking again: a message can be
    /// lost, and an unanswered request is asked again under the same key.
    /// </summary>
    Reply,
}
