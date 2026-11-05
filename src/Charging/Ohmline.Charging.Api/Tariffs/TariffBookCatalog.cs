using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Options;
using Ohmline.ActorShell.Tracing;
using Ohmline.Tariffs.Contracts;
using Ohmline.Tariffs.Contracts.Book;

namespace Ohmline.Charging.Api.Tariffs;

/// <summary>
/// The catalog the edge copies tariffs from: the tariff book, asked through its singleton
/// proxy, wherever in the cluster the book runs.
/// </summary>
public sealed class TariffBookCatalog(
    IRequiredActor<TariffBookProxy> book, IOptions<ChargingApiOptions> options) : ITariffCatalog
{
    public async Task<TariffSnapshot?> CurrentAsync(
        TariffId tariffId, CancellationToken cancellationToken)
    {
        var current = await book.ActorRef.AskTraced<CurrentTariff>(
            new GetTariffSnapshot(tariffId), options.Value.AskTimeout, cancellationToken);
        return current.Snapshot;
    }
}
