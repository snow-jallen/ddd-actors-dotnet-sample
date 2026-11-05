using System.Collections.Immutable;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.Tariffs.Domain.Book;

/// <summary>Every tariff ever published, by id. There is one book, and it starts empty.</summary>
public sealed record TariffBookState(ImmutableDictionary<TariffId, TariffEntry> Tariffs)
{
    public static TariffBookState Empty { get; } =
        new(ImmutableDictionary<TariffId, TariffEntry>.Empty);

    /// <summary>The version a session starting now copies, if the tariff is published.</summary>
    public TariffSnapshot? Current(TariffId tariffId) =>
        Tariffs.GetValueOrDefault(tariffId).Value is Published published
            ? published.Current
            : null;

    public bool Equals(TariffBookState? other) =>
        other is not null
        && Tariffs.Count == other.Tariffs.Count
        && Tariffs.All(pair =>
            other.Tariffs.TryGetValue(pair.Key, out var entry)
            && Equals(entry.Value, pair.Value.Value));

    public override int GetHashCode() => Tariffs.Count;
}
