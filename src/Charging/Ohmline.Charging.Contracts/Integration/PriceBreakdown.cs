using System.Collections.Immutable;

namespace Ohmline.Charging.Contracts.Integration;

/// <summary>
/// What a session cost, line by line, in plain values: an amount and an ISO 4217 currency code
/// rather than the shared kernel's <c>Money</c>, so a consumer needs no Ohmline type to read
/// it. The total is the sum of the lines. Equal when the values are equal: a record alone
/// would compare the lines by reference.
/// </summary>
public sealed record PriceBreakdown(string Currency, decimal Total, ImmutableArray<PriceLine> Lines)
{
    public bool Equals(PriceBreakdown? other) =>
        other is not null
        && Currency == other.Currency
        && Total == other.Total
        && Lines.SequenceEqual(other.Lines);

    public override int GetHashCode() => HashCode.Combine(Currency, Total, Lines.Length);
}

/// <summary>
/// One line of a price: what was charged for (<see cref="Code"/>, such as <c>energy</c>), how
/// much of it, at which unit price, and the amount.
/// </summary>
public sealed record PriceLine(
    string Code, decimal Quantity, string Unit, decimal UnitPrice, decimal Amount)
{
    public const string Energy = "energy";
    public const string SessionFee = "session-fee";
}
