using Ohmline.Charging.Api.Tariffs;
using Ohmline.SharedKernel.Monetary;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.Charging.Api.Development;

/// <summary>
/// A stand-in for the tariff book, for Development only, until section 12.8: one tariff,
/// <c>slc-standard</c>, at version 1, priced in US dollars in Salt Lake City's time zone.
/// </summary>
internal sealed class DevelopmentTariffCatalog : ITariffCatalog
{
    private static readonly TariffSnapshot SlcStandard = new(
        new TariffId("slc-standard"),
        Version: 1,
        Currency.Usd,
        [
            new EnergyRate(new Money(0.42m, Currency.Usd)),
            new SessionFee(new Money(1.00m, Currency.Usd)),
            new IdleFee(new Money(0.40m, Currency.Usd), TimeSpan.FromMinutes(10)),
        ],
        PriceCap: null,
        RoundingPolicy.Cents,
        "America/Denver");

    public Task<TariffSnapshot?> CurrentAsync(
        TariffId tariffId, CancellationToken cancellationToken) =>
        Task.FromResult(tariffId == SlcStandard.TariffId ? SlcStandard : null);
}
