using System.Collections.Immutable;
using Ohmline.ActorShell.Deciders;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.Tariffs.Actors.Book;

/// <summary>
/// The tariff book, as its snapshot stores it: the latest version of every tariff, and whether
/// it is retired. A DTO with a manifest of its own. Equal when the values are equal.
/// </summary>
public sealed record TariffBookSnapshot(ImmutableArray<TariffBookSnapshot.Entry> Tariffs)
{
    /// <summary>The commands it accepted most recently (section 12.6).</summary>
    public ImmutableArray<RecentCommand> RecentCommands
    {
        get;
        init => field = value.IsDefault ? [] : value;
    } = [];

    public bool Equals(TariffBookSnapshot? other) =>
        other is not null
        && Tariffs.SequenceEqual(other.Tariffs)
        && RecentCommands.SequenceEqual(other.RecentCommands);

    public override int GetHashCode() => Tariffs.Length;

    /// <summary>One tariff: its latest version, and whether sessions may still copy it.</summary>
    public sealed record Entry(TariffSnapshot Latest, bool Retired);
}
