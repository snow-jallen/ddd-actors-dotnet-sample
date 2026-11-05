using System.Numerics;

namespace Ohmline.SharedKernel.Units;

/// <summary>
/// Electrical current in amperes, as a charge point's limits and charging profiles state it.
/// Never negative.
/// </summary>
public readonly record struct Current :
    IAdditionOperators<Current, Current, Current>,
    ISubtractionOperators<Current, Current, Current>,
    IComparisonOperators<Current, Current, bool>,
    IAdditiveIdentity<Current, Current>,
    IValueObject<Current, decimal>
{
    public Current(decimal amperes) => Amperes = amperes;

    public decimal Amperes
    {
        get;
        init => field = value >= 0m
            ? value
            : throw new ArgumentOutOfRangeException(
                nameof(Amperes), value, "Current is never negative.");
    }

    decimal IValueObject<Current, decimal>.Value => Amperes;

    public static Current Zero => default;

    static Current IAdditiveIdentity<Current, Current>.AdditiveIdentity => Zero;

    public static Result<Current, ValidationError> TryCreate(decimal amperes) =>
        amperes >= 0m
            ? Result.Ok(new Current(amperes))
            : Result.Fail<ValidationError>(
                new OutOfRange(nameof(Current), amperes, "0 A or more"));

    public static Current operator +(Current left, Current right) =>
        new(left.Amperes + right.Amperes);

    public static Current operator -(Current left, Current right) =>
        new(left.Amperes - right.Amperes);

    public static bool operator <(Current left, Current right) => left.Amperes < right.Amperes;

    public static bool operator >(Current left, Current right) => left.Amperes > right.Amperes;

    public static bool operator <=(Current left, Current right) => left.Amperes <= right.Amperes;

    public static bool operator >=(Current left, Current right) => left.Amperes >= right.Amperes;

    public override string ToString() => FormattableString.Invariant($"{Amperes} A");
}
