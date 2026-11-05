using System.Numerics;

namespace Ohmline.SharedKernel.Units;

/// <summary>
/// Electrical potential in volts, such as a connector's supply voltage. Never negative.
/// </summary>
public readonly record struct Voltage :
    IAdditionOperators<Voltage, Voltage, Voltage>,
    ISubtractionOperators<Voltage, Voltage, Voltage>,
    IComparisonOperators<Voltage, Voltage, bool>,
    IAdditiveIdentity<Voltage, Voltage>,
    IValueObject<Voltage, decimal>
{
    public Voltage(decimal volts) => Volts = volts;

    public decimal Volts
    {
        get;
        init => field = value >= 0m
            ? value
            : throw new ArgumentOutOfRangeException(
                nameof(Volts), value, "Voltage is never negative.");
    }

    decimal IValueObject<Voltage, decimal>.Value => Volts;

    public static Voltage Zero => default;

    static Voltage IAdditiveIdentity<Voltage, Voltage>.AdditiveIdentity => Zero;

    public static Result<Voltage, ValidationError> TryCreate(decimal volts) =>
        volts >= 0m
            ? Result.Ok(new Voltage(volts))
            : Result.Fail<ValidationError>(
                new OutOfRange(nameof(Voltage), volts, "0 V or more"));

    public static Voltage operator +(Voltage left, Voltage right) => new(left.Volts + right.Volts);

    public static Voltage operator -(Voltage left, Voltage right) => new(left.Volts - right.Volts);

    public static bool operator <(Voltage left, Voltage right) => left.Volts < right.Volts;

    public static bool operator >(Voltage left, Voltage right) => left.Volts > right.Volts;

    public static bool operator <=(Voltage left, Voltage right) => left.Volts <= right.Volts;

    public static bool operator >=(Voltage left, Voltage right) => left.Volts >= right.Volts;

    public override string ToString() => FormattableString.Invariant($"{Volts} V");
}
