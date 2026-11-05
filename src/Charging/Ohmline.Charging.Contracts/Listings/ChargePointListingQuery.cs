using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Contracts.Listings;

/// <summary>Questions to a charge point's listing: a family of its own, never a command.</summary>
public closed record ChargePointListingQuery(ChargePointId ChargePointId);

/// <summary>What do drivers see about this charge point?</summary>
public sealed record GetChargePointListing(ChargePointId ChargePointId)
    : ChargePointListingQuery(ChargePointId);
