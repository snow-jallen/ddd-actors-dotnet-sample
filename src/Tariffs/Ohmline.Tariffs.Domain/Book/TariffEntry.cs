using Ohmline.Tariffs.Contracts;

namespace Ohmline.Tariffs.Domain.Book;

/// <summary>One tariff in the book: published, the version sessions copy, or retired.</summary>
public union TariffEntry(Published, Retired)
{
    /// <summary>The latest version, whether or not sessions may still start with it.</summary>
    public TariffSnapshot Latest => this switch
    {
        Published published => published.Current,
        Retired retired => retired.Last,
        null => throw new InvalidOperationException("This TariffEntry is default."),
    };
}

/// <summary>Sessions that start now copy this version.</summary>
public sealed record Published(TariffSnapshot Current);

/// <summary>No session starts with the tariff; this was its last version.</summary>
public sealed record Retired(TariffSnapshot Last);
