namespace Ohmline.Charging.Domain.Journeys;

/// <summary>The journey's timers: at most one of each runs at a time.</summary>
public enum JourneyTimer
{
    /// <summary>How long the charger has to start delivering energy once it is told to.</summary>
    EnergyFlow,
}
