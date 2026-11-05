namespace Ohmline.Charging.Domain.Journeys;

/// <summary>
/// Why a journey refused a command: stable codes, published to the driver app through
/// <c>RejectionCodes</c>. Add new ones; never change or reuse one.
/// </summary>
public static class JourneyRefusals
{
    /// <summary>The journey id is taken: another command started that journey.</summary>
    public const string JourneyExists = "journey-exists";
}
