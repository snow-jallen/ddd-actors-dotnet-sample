using System.Numerics;

namespace Ohmline.SharedKernel.Units;

/// <summary>
/// An instantaneous rate of energy transfer in kilowatts: what a connector can deliver, what a
/// site may draw, what an allocation grants. Never negative, and never confused with energy.
/// </summary>
public readonly record struct Power :
    IAdditionOperators<Power, Power, Power>,
    ISubtractionOperators<Power, Power, Power>,
    IComparisonOperators<Power, Power, bool>,
    IAdditiveIdentity<Power, Power>,
    IValueObject<Power, decimal>
{
    public Power(decimal kilowatts) => Kilowatts = kilowatts;

    public decimal Kilowatts
    {
        get;
        init => field = value >= 0m
            ? value
            : throw new ArgumentOutOfRangeException(
                nameof(Kilowatts), value, "Power is never negative.");
    }

    decimal IValueObject<Power, decimal>.Value => Kilowatts;

    public decimal Watts => Kilowatts * 1000m;

    public static Power Zero => default;

    static Power IAdditiveIdentity<Power, Power>.AdditiveIdentity => Zero;

    public static Result<Power, ValidationError> TryCreate(decimal kilowatts) =>
        kilowatts >= 0m
            ? Result.Ok(new Power(kilowatts))
            : Result.Fail<ValidationError>(
                new OutOfRange(nameof(Power), kilowatts, "0 kW or more"));

    public static Power FromWatts(decimal watts) => new(watts / 1000m);

    public static Power operator +(Power left, Power right) =>
        new(left.Kilowatts + right.Kilowatts);

    public static Power operator -(Power left, Power right) =>
        new(left.Kilowatts - right.Kilowatts);

    public static bool operator <(Power left, Power right) => left.Kilowatts < right.Kilowatts;

    public static bool operator >(Power left, Power right) => left.Kilowatts > right.Kilowatts;

    public static bool operator <=(Power left, Power right) => left.Kilowatts <= right.Kilowatts;

    public static bool operator >=(Power left, Power right) => left.Kilowatts >= right.Kilowatts;

    public override string ToString() => FormattableString.Invariant($"{Kilowatts} kW");
}
