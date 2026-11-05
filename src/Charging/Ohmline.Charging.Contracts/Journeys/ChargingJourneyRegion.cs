namespace Ohmline.Charging.Contracts.Journeys;

/// <summary>
/// The key under which a node's actor registry holds the charging journey shard region
/// (<c>charging-journey</c>): code that starts or asks about a journey asks for
/// <c>IRequiredActor&lt;ChargingJourneyRegion&gt;</c> and needs no reference to the actors.
/// </summary>
public sealed class ChargingJourneyRegion;
