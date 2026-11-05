using System.Collections.Immutable;
using Ohmline.Tariffs.Domain.Book;

namespace Ohmline.Tariffs.Actors.Book;

/// <summary>From the book's state to its snapshot and back, in order of tariff id.</summary>
public static class TariffBookMapping
{
    extension(TariffBookState state)
    {
        /// <summary>Null while the book is empty: there is nothing to keep.</summary>
        public TariffBookSnapshot? ToSnapshot() => state.Tariffs.IsEmpty
            ? null
            : new(
            [
                .. state.Tariffs
                    .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
                    .Select(pair => new TariffBookSnapshot.Entry(
                        pair.Value.Latest, pair.Value.Value is Retired)),
            ]);
    }

    extension(TariffBookSnapshot snapshot)
    {
        public TariffBookState ToState() => new(snapshot.Tariffs.ToImmutableDictionary(
            entry => entry.Latest.TariffId,
            entry => entry.Retired
                ? (TariffEntry)new Retired(entry.Latest)
                : new Published(entry.Latest)));
    }
}
