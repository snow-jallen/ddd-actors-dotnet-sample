namespace Ohmline.Charging.Domain.Journeys;

/// <summary>
/// Why a journey was given up: stable codes, recorded in <see cref="JourneyAbandoned"/> and
/// meant for programs. The detail beside each is a sentence for people.
/// </summary>
public static class AbandonReasons
{
    public const string TokenRefused = "token-refused";
    public const string HoldDeclined = "hold-declined";
    public const string AllocationRefused = "allocation-refused";
    public const string ChargerRejected = "charger-rejected";
    public const string EnergyFlowTimedOut = "energy-flow-timed-out";
}
