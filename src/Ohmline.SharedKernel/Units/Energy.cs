using System.Numerics;

namespace Ohmline.SharedKernel.Units;

/// <summary>
/// An amount of energy in kilowatt-hours: what a meter counts and what a driver pays for. Energy
/// is never negative. It is not power: power is a rate, and energy is power over time.
/// </summary>
public readonly record struct Energy :
    IAdditionOperators<Energy, Energy, Energy>,
    ISubtractionOperators<Energy, Energy, Energy>,
    IComparisonOperators<Energy, Energy, bool>,
    IAdditiveIdentity<Energy, Energy>
{
    public Energy(decimal kilowattHours) => KilowattHours = kilowattHours;

    public decimal KilowattHours
    {
        get;
        init => field = value >= 0m
            ? value
            : throw new ArgumentOutOfRangeException(
                nameof(KilowattHours), value, "Energy is never negative.");
    }

    public decimal WattHours => KilowattHours * 1000m;

    public static Energy Zero => default;

    static Energy IAdditiveIdentity<Energy, Energy>.AdditiveIdentity => Zero;

    public static Energy FromWattHours(decimal wattHours) => new(wattHours / 1000m);

    public static Energy operator +(Energy left, Energy right) =>
        new(left.KilowattHours + right.KilowattHours);

    /// <summary>Taking away more energy than there is would leave a negative amount.</summary>
    public static Energy operator -(Energy left, Energy right) =>
        new(left.KilowattHours - right.KilowattHours);

    public static bool operator <(Energy left, Energy right) =>
        left.KilowattHours < right.KilowattHours;

    public static bool operator >(Energy left, Energy right) =>
        left.KilowattHours > right.KilowattHours;

    public static bool operator <=(Energy left, Energy right) =>
        left.KilowattHours <= right.KilowattHours;

    public static bool operator >=(Energy left, Energy right) =>
        left.KilowattHours >= right.KilowattHours;

    public override string ToString() => FormattableString.Invariant($"{KilowattHours} kWh");
}
