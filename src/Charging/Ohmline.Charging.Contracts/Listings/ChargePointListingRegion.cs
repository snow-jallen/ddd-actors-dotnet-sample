namespace Ohmline.Charging.Contracts.Listings;

/// <summary>
/// The key under which a node's actor registry holds the listing shard region
/// (<c>charge-point-listing</c>): code that sends listing commands and queries asks for
/// <c>IRequiredActor&lt;ChargePointListingRegion&gt;</c> and needs no reference to the actors.
/// </summary>
public sealed class ChargePointListingRegion;
