using Ohmline.Charging.Contracts.Listings;
using Ohmline.Charging.Domain.Listings;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Actors.Listings;

/// <summary>From a listing's state to its snapshot, back, and to the view drivers see.</summary>
public static class ListingMapping
{
    extension(ListingState state)
    {
        /// <summary>Null while unlisted: there is nothing to keep.</summary>
        public ChargePointListingSnapshot? ToSnapshot() => state switch
        {
            Unlisted => null,
            Listed listed => new(
                listed.ChargePointId,
                listed.DisplayName,
                listed.AccessInstructions,
                listed.OpeningHours,
                listed.Revision),
            null => throw DefaultState(),
        };

        public ChargePointListingView ToView(ChargePointId chargePointId) => state switch
        {
            Unlisted => new(chargePointId, Revision: 0),
            Listed listed => new(
                chargePointId,
                listed.Revision,
                listed.DisplayName,
                listed.AccessInstructions,
                listed.OpeningHours),
            null => throw DefaultState(),
        };
    }

    extension(ChargePointListingSnapshot snapshot)
    {
        public ListingState ToState() => new Listed(
            snapshot.ChargePointId,
            snapshot.DisplayName,
            snapshot.AccessInstructions,
            snapshot.OpeningHours,
            snapshot.Revision);
    }

    private static InvalidOperationException DefaultState() =>
        new("The state is default: it holds no case.");
}
