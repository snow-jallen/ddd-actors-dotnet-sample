using Akka.Actor;
using Akka.Hosting;
using Ohmline.ActorShell.Tracing;
using Ohmline.Charging.Contracts.Listings;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.ReadModels.DriverApp;

/// <summary>
/// Display names from each listing's current view: the listing actor, asked through the
/// listing shard region. One question per session start, not one per card a phone reads.
/// </summary>
/// <remarks>
/// A listing that does not answer in time gives no name, rather than failing the event: the
/// card shows its connector until one of its session's next events asks again.
/// </remarks>
public sealed class ListingDisplayNames(ActorSystem system) : IChargePointNames
{
    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(3);

    public async Task<string?> DisplayNameAsync(
        ChargePointId chargePointId, CancellationToken cancellationToken)
    {
        var listings =
            await ActorRegistry.For(system).GetAsync<ChargePointListingRegion>(cancellationToken);
        try
        {
            var view = await listings.AskTraced<ChargePointListingView>(
                new GetChargePointListing(chargePointId), AskTimeout, cancellationToken);
            return view.DisplayName;
        }
        catch (AskTimeoutException)
        {
            return null;
        }
    }
}
