using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Domain.Listings;

/// <summary>What a charge point's listing remembers: nothing, or the latest listing.</summary>
public union ListingState(Unlisted, Listed)
{
    public static ListingState Initial { get; } = new Unlisted();
}

/// <summary>Drivers see the charge point's id and nothing else.</summary>
public sealed record Unlisted;

/// <summary>The latest listing and how many revisions it took to get here.</summary>
public sealed record Listed(
    ChargePointId ChargePointId,
    string DisplayName,
    string AccessInstructions,
    string OpeningHours,
    long Revision);
