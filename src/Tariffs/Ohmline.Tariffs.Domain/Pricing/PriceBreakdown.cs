using System.Collections.Immutable;
using Ohmline.SharedKernel.Monetary;

namespace Ohmline.Tariffs.Domain.Pricing;

/// <summary>
/// The line items of a session's price and their total. The total is the sum of the lines, not
/// a number stored next to them, so a breakdown always adds up to its total to the cent.
/// </summary>
public sealed record PriceBreakdown(Currency Currency, ImmutableArray<PriceLine> Lines)
{
    public Money Total => Lines.Aggregate(Money.Zero(Currency), (sum, line) => sum + line.Amount);

    public bool Equals(PriceBreakdown? other) =>
        other is not null && Currency == other.Currency && Lines.SequenceEqual(other.Lines);

    public override int GetHashCode() => HashCode.Combine(Currency, Lines.Length);
}
