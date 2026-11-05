using System.Collections.Immutable;
using CsCheck;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using Ohmline.Tariffs.Contracts;
using Ohmline.Tariffs.Domain.Pricing;
using static Ohmline.Tariffs.Domain.Tests.TheDepotTariff;
using Gen = CsCheck.Gen;

namespace Ohmline.Tariffs.Domain.Tests.Pricing;

/// <summary>
/// What holds for every session: energy is neither lost nor invented when it is split across
/// bands, every line is in whole cents, and the total never passes the cap.
/// </summary>
public sealed class PriceCalculatorProperties
{
    private static readonly TariffSnapshot Busy = Version1(
            new EnergyRate(Usd(0.42m)),
            new EnergyRate(Usd(0.60m), Peak),
            new EnergyRate(Usd(0.25m), Night),
            new TimeRate(Usd(0.05m), Peak),
            Fee,
            Idle)
        .Snapshot();

    /// <summary>Up to 20 readings in a row, anywhere in a day, in Wh and whole seconds.</summary>
    private static readonly Gen<SessionUsage> Sessions =
        Gen.Select(
                Gen.Int[0, 24 * 3600],
                Gen.Select(Gen.Int[1, 3600], Gen.Int[0, 50_000]).Array[1, 20],
                Gen.Int[0, 120])
            .Select((startSecond, readings, idleMinutes) =>
            {
                var at = PriceCalculatorTests.Local(0, 0).AddSeconds(startSecond);
                var intervals = ImmutableArray.CreateBuilder<MeteredInterval>();
                foreach (var (seconds, wattHours) in readings)
                {
                    var until = at.AddSeconds(seconds);
                    intervals.Add(new MeteredInterval(at, until, Energy.FromWattHours(wattHours)));
                    at = until;
                }

                return new SessionUsage(
                    intervals.ToImmutable(), TimeSpan.FromMinutes(idleMinutes));
            });

    [Fact]
    public void Splitting_a_session_across_bands_neither_loses_nor_invents_energy()
    {
        Sessions.Sample(usage =>
        {
            var charged = PriceCalculator.Price(Busy, usage).Lines.OfType<EnergyCharge>()
                .Sum(line => line.Energy.KilowattHours);

            Math.Abs(charged - usage.Energy.KilowattHours).ShouldBeLessThan(0.000_000_001m);
        });
    }

    [Fact]
    public void Every_line_is_in_whole_cents_and_the_total_never_passes_the_cap()
    {
        Gen.Select(Sessions, Gen.Int[0, 10_000]).Sample((usage, capCents) =>
        {
            var cap = new Money(capCents / 100m, Currency.Usd);

            var price = PriceCalculator.Price(Busy with { PriceCap = cap }, usage);

            price.Lines.ShouldAllBe(line =>
                line.Amount.Amount == decimal.Round(line.Amount.Amount, 2));
            (price.Total <= cap).ShouldBeTrue();
        });
    }
}
