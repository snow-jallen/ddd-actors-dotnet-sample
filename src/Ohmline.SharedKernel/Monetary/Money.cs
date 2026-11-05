using System.Numerics;

namespace Ohmline.SharedKernel.Monetary;

/// <summary>
/// An amount in one currency. Amounts may be negative (a refund, a correction). Arithmetic stays
/// inside one currency: the operators throw on a mismatch, because mixing currencies there is a
/// bug, and <see cref="TryAdd"/> returns the mismatch where two currencies can legitimately meet.
/// There is no zero without a currency, so Money has no additive identity; use
/// <see cref="Zero"/>.
/// </summary>
public readonly record struct Money :
    IAdditionOperators<Money, Money, Money>,
    ISubtractionOperators<Money, Money, Money>,
    IMultiplyOperators<Money, decimal, Money>,
    IComparisonOperators<Money, Money, bool>
{
    public Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; init; }

    public Currency Currency
    {
        get;
        init => field = value.Code is not null
            ? value
            : throw new ArgumentException("Money needs a currency.", nameof(Currency));
    }

    public static Money Zero(Currency currency) => new(0m, currency);

    public Result<Money, CurrencyMismatch> TryAdd(Money other) =>
        Currency == other.Currency
            ? Result.Ok(this with { Amount = Amount + other.Amount })
            : Result.Fail(new CurrencyMismatch(Currency, other.Currency));

    public static Money operator +(Money left, Money right) =>
        new(left.Amount + right.Amount, CommonCurrency(left, right));

    public static Money operator -(Money left, Money right) =>
        new(left.Amount - right.Amount, CommonCurrency(left, right));

    /// <summary>Scales an amount, such as a price per kWh by the energy. Never rounds.</summary>
    public static Money operator *(Money money, decimal factor) =>
        money with { Amount = money.Amount * factor };

    public static bool operator <(Money left, Money right) => Compare(left, right) < 0;

    public static bool operator >(Money left, Money right) => Compare(left, right) > 0;

    public static bool operator <=(Money left, Money right) => Compare(left, right) <= 0;

    public static bool operator >=(Money left, Money right) => Compare(left, right) >= 0;

    public override string ToString() => FormattableString.Invariant($"{Amount} {Currency}");

    private static int Compare(Money left, Money right)
    {
        CommonCurrency(left, right);
        return left.Amount.CompareTo(right.Amount);
    }

    private static Currency CommonCurrency(Money left, Money right) =>
        left.Currency == right.Currency
            ? left.Currency
            : throw new InvalidOperationException(
                new CurrencyMismatch(left.Currency, right.Currency).Message);
}
