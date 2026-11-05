using Ohmline.ActorShell.Deciders;
using Ohmline.Tariffs.Actors.Book;
using Ohmline.Tariffs.Contracts.Book;
using Ohmline.Tariffs.Domain.Book;
using static Ohmline.Tariffs.Actors.Tests.TheBook;

namespace Ohmline.Tariffs.Actors.Tests.Serialization;

/// <summary>One message of every type Tariffs' serializer writes, all fields filled.</summary>
internal static class TariffsSamples
{
    public static IReadOnlyList<object> All { get; } =
    [
        Publish(Version1, Command),
        new RetireTariff(Command, Standard),

        Stamped(new TariffPublished(Version1), Command),
        Stamped(new TariffRetired(Standard, 1), Command),

        new GetTariffSnapshot(Standard),
        new CurrentTariff(Standard, Version1),

        new TariffBookAccepted(Version: 1),
        new TariffBookRejected("not-published", "Tariff slc-gold has never been published."),

        new TariffBookSnapshot([new TariffBookSnapshot.Entry(Version1, Retired: false)])
        {
            RecentCommands = [new RecentCommand(Command, 1)],
        },
    ];
}
