using Ohmline.SharedKernel.Identity;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.Tariffs.Domain.Book;

/// <summary>
/// What can happen in the tariff book. Written to the journal, so plain data only. Every event
/// records the command that caused it and when it happened.
/// </summary>
public closed record TariffBookEvent
{
    /// <summary>The command that caused this event: its causation.</summary>
    public CommandId CommandId { get; init; }

    /// <summary>When it happened: the time of the decision, from its context.</summary>
    public DateTimeOffset OccurredAt { get; init; }
}

/// <summary>
/// A version of a tariff was published, whole: the book keeps what sessions will copy, so the
/// event carries the version as they will see it.
/// </summary>
public sealed record TariffPublished(TariffSnapshot Tariff) : TariffBookEvent;

/// <summary>No session may start with the tariff any more; this was its last version.</summary>
public sealed record TariffRetired(TariffId TariffId, int Version) : TariffBookEvent;
