using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Actors.Listings;

/// <summary>
/// A listing as its snapshot stores it. The listing is state sourced, so once its events are
/// deleted this row is the only copy of it: the DTO and its manifest are as permanent as an
/// event's.
/// </summary>
public sealed record ChargePointListingSnapshot(
    ChargePointId ChargePointId,
    string DisplayName,
    string AccessInstructions,
    string OpeningHours,
    long Revision);
