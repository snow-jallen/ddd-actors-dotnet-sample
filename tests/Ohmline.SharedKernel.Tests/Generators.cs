using CsCheck;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
// Pins Gen to CsCheck's. Other property libraries have a Gen too (FsCheck's clashes with CS0104),
// and with the alias this file never depends on which namespaces happen to be imported.
using Gen = CsCheck.Gen;

namespace Ohmline.SharedKernel.Tests;

/// <summary>
/// Random value objects with the resolution of real data: energy to the watt-hour, power to the
/// watt, time to the second, money to a hundredth of a cent. At that resolution decimal sums
/// are exact, so a failing property points at the model, not at arithmetic noise.
/// </summary>
internal static class Generators
{
    /// <summary>0 to 1 GWh, in watt-hours.</summary>
    public static readonly Gen<Energy> Energies =
        Gen.Long[0, 1_000_000_000].Select(wattHours => Energy.FromWattHours(wattHours));

    /// <summary>0 to 1 MW, in watts.</summary>
    public static readonly Gen<Power> Powers =
        Gen.Int[0, 1_000_000].Select(watts => Power.FromWatts(watts));

    /// <summary>0 to 48 hours, in seconds.</summary>
    public static readonly Gen<TimeSpan> Durations =
        Gen.Int[0, 48 * 3600].Select(seconds => TimeSpan.FromSeconds(seconds));

    public static readonly Gen<Currency> Currencies = Gen.OneOf(
        Gen.Const(Currency.Usd), Gen.Const(Currency.Eur), Gen.Const(new Currency("JPY")));

    public static readonly Gen<RoundingPolicy> RoundingPolicies =
        Gen.Select(Gen.Int[0, 4], Gen.Enum<MidpointRounding>(),
            (decimals, mode) => new RoundingPolicy(decimals, mode));

    /// <summary>Amounts from -1,000 to 1,000 in one currency, to four decimal places.</summary>
    public static Gen<Money> AmountsIn(Currency currency) =>
        Gen.Long[-10_000_000, 10_000_000].Select(units => new Money(units / 10_000m, currency));

    public static readonly Gen<Money> Amounts = Currencies.SelectMany(AmountsIn);
}
