using Ohmline.SharedKernel.Monetary;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.Tariffs.Domain.Tests;

/// <summary>
/// The tariff of the primer's depot (<c>docs/ohmline-primer.md</c>): USD 0.42 per kWh, a USD 1.00
/// session fee, and an idle fee of USD 0.40 per minute after a 15-minute grace period.
/// </summary>
internal static class TheDepotTariff
{
    public static readonly TariffId Id = new("slc-standard");

    public static readonly TimeOfUseBand Peak =
        new("peak", new TimeOnly(16, 0), new TimeOnly(21, 0));

    public static readonly TimeOfUseBand Night =
        new("night", new TimeOnly(22, 0), new TimeOnly(6, 0));

    public static readonly EnergyRate BaseRate = new(Usd(0.42m));

    public static readonly SessionFee Fee = new(Usd(1.00m));

    public static readonly IdleFee Idle = new(Usd(0.40m), TimeSpan.FromMinutes(15));

    public static readonly TariffComponent[] Components = [BaseRate, Fee, Idle];

    public static Tariff Version1(params TariffComponent[] components) => Tariff.Create(
        Id,
        1,
        Currency.Usd,
        components is [] ? Components : components,
        priceCap: null,
        RoundingPolicy.Cents,
        "America/Denver");

    public static Money Usd(decimal amount) => new(amount, Currency.Usd);
}
