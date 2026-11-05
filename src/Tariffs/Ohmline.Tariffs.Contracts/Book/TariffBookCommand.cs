using System.Collections.Immutable;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;

namespace Ohmline.Tariffs.Contracts.Book;

/// <summary>
/// What pricing analysts tell the tariff book: Ohmline's one list of published tariffs, the
/// Knowledge Level that sessions copy their prices from (section 8.2).
/// </summary>
public closed record TariffBookCommand(CommandId CommandId);

/// <summary>
/// Publish the next version of a tariff (version 1 when it is new). A version never changes
/// once published; sessions already started keep the version they copied. Equal when the
/// values are equal.
/// </summary>
public sealed record PublishTariff(
    CommandId CommandId,
    TariffId TariffId,
    Currency Currency,
    ImmutableArray<TariffComponent> Components,
    Money? PriceCap,
    RoundingPolicy Rounding,
    string TimeZoneId)
    : TariffBookCommand(CommandId)
{
    public bool Equals(PublishTariff? other) =>
        other is not null
        && base.Equals(other)
        && TariffId == other.TariffId
        && Currency == other.Currency
        && Components.SequenceEqual(other.Components)
        && PriceCap == other.PriceCap
        && Rounding == other.Rounding
        && TimeZoneId == other.TimeZoneId;

    public override int GetHashCode() =>
        HashCode.Combine(base.GetHashCode(), TariffId, Components.Length);
}

/// <summary>No new session may start with this tariff; running ones keep their copy.</summary>
public sealed record RetireTariff(CommandId CommandId, TariffId TariffId)
    : TariffBookCommand(CommandId);
