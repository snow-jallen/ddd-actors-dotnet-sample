using System.Collections.Immutable;
using Ohmline.SharedKernel.Monetary;

namespace Ohmline.Tariffs.Contracts;

/// <summary>
/// One version of a tariff, copied into a session when it starts. A later version never changes
/// a session that holds this copy, and the copy holds everything pricing needs: the components,
/// the price cap, the rounding policy, and the time zone its bands are written in. Equal when the
/// values are equal: a record alone would compare the components by reference.
/// </summary>
public sealed record TariffSnapshot(
    TariffId TariffId,
    int Version,
    Currency Currency,
    ImmutableArray<TariffComponent> Components,
    Money? PriceCap,
    RoundingPolicy Rounding,
    string TimeZoneId)
{
    public bool Equals(TariffSnapshot? other) =>
        other is not null
        && TariffId == other.TariffId
        && Version == other.Version
        && Currency == other.Currency
        && Components.SequenceEqual(other.Components)
        && PriceCap == other.PriceCap
        && Rounding == other.Rounding
        && TimeZoneId == other.TimeZoneId;

    public override int GetHashCode() => HashCode.Combine(TariffId, Version, Components.Length);
}
