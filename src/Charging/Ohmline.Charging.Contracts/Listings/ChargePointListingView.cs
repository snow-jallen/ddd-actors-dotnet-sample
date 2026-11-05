using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Contracts.Listings;

/// <summary>
/// A charge point's listing as drivers see it. <paramref name="Revision"/> counts the revisions
/// so far; a charge point that was never listed has revision 0 and no text.
/// </summary>
public sealed record ChargePointListingView(
    ChargePointId ChargePointId,
    long Revision,
    string? DisplayName = null,
    string? AccessInstructions = null,
    string? OpeningHours = null);
