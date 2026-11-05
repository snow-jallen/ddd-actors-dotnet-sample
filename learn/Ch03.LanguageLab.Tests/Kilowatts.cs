using System.Numerics;

namespace Ch03.LanguageLab.Tests;

/// <summary>
/// Power in kW. The guard lives in the init accessor, so the constructor and every
/// with-expression go through it.
/// </summary>
public readonly record struct Kilowatts
    : IAdditionOperators<Kilowatts, Kilowatts, Kilowatts>,
      IAdditiveIdentity<Kilowatts, Kilowatts>,
      IComparisonOperators<Kilowatts, Kilowatts, bool>
{
    public Kilowatts(decimal value) => Value = value;

    public decimal Value
    {
        get;
        init => field = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Value), value, "Negative power.");
    }

    public static Kilowatts AdditiveIdentity => new(0m);

    public static Kilowatts operator +(Kilowatts left, Kilowatts right) =>
        new(left.Value + right.Value);

    public static bool operator <(Kilowatts left, Kilowatts right) => left.Value < right.Value;

    public static bool operator >(Kilowatts left, Kilowatts right) => left.Value > right.Value;

    public static bool operator <=(Kilowatts left, Kilowatts right) => left.Value <= right.Value;

    public static bool operator >=(Kilowatts left, Kilowatts right) => left.Value >= right.Value;

    public override string ToString() => $"{Value} kW";
}
