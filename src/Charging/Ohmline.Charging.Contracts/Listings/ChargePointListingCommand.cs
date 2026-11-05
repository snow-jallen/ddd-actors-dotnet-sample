using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Contracts.Listings;

/// <summary>
/// What a charge point's listing can be asked to do: what drivers see about the charge point
/// in the app, kept by operators. A supporting part of Charging, with no history worth keeping.
/// </summary>
public closed record ChargePointListingCommand(CommandId CommandId, ChargePointId ChargePointId);

/// <summary>
/// Replace the whole listing: the name drivers see, how to find the chargers on site (such as
/// <c>Level -2, bays 14-19</c>), and the opening hours as operators write them.
/// </summary>
public sealed record ReviseChargePointListing(
    CommandId CommandId,
    ChargePointId ChargePointId,
    string DisplayName,
    string AccessInstructions,
    string OpeningHours)
    : ChargePointListingCommand(CommandId, ChargePointId);
