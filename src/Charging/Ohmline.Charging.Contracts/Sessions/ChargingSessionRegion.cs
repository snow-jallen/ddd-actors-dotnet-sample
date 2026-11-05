namespace Ohmline.Charging.Contracts.Sessions;

/// <summary>
/// The key under which a node's actor registry holds the charging session shard region
/// (<c>charging-session</c>): code that sends session commands asks for
/// <c>IRequiredActor&lt;ChargingSessionRegion&gt;</c> and needs no reference to the actors.
/// </summary>
public sealed class ChargingSessionRegion;
