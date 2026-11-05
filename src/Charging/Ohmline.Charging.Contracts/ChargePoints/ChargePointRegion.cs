namespace Ohmline.Charging.Contracts.ChargePoints;

/// <summary>
/// The key under which a node's actor registry holds the charge point shard region
/// (<c>charge-point</c>): code that sends charge point commands and queries asks for
/// <c>IRequiredActor&lt;ChargePointRegion&gt;</c> and needs no reference to the actors behind it.
/// </summary>
public sealed class ChargePointRegion;
