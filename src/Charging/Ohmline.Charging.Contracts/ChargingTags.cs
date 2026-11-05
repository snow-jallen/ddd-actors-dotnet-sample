namespace Ohmline.Charging.Contracts;

/// <summary>
/// The tags Charging writes beside its events in the journal: the names by which the read side
/// asks for them (section 13.2). Each event carries exactly one, so a projection that reads
/// several tags never sees an event twice.
/// </summary>
/// <remarks>
/// Durable vocabulary. A tag is written when the event is, and never again: renaming one
/// strands every event stored under the old name, and a new tag applies only to events written
/// after it exists.
/// </remarks>
public static class ChargingTags
{
    /// <summary>Charge points and their listings: connectors, reservations, faults.</summary>
    public const string Charging = "charging";

    /// <summary>Charging sessions: starts, meter values, suspensions, ends.</summary>
    public const string ChargingSession = "charging-session";

    /// <summary>Charging journeys: each step from "start charging" to a settled payment.</summary>
    public const string ChargingJourney = "charging-journey";
}
