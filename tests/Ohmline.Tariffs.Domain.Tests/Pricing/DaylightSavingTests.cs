using System.Collections.Immutable;
using Microsoft.Extensions.Time.Testing;
using Ohmline.SharedKernel.Units;
using Ohmline.Tariffs.Contracts;
using Ohmline.Tariffs.Domain.Pricing;
using static Ohmline.Tariffs.Domain.Tests.TheDepotTariff;

namespace Ohmline.Tariffs.Domain.Tests.Pricing;

/// <summary>
/// Overnight charges at the depot on the two nights Denver changes its clocks in 2026. A fake
/// clock plays the charge point: it reads the meter every minute, 0.5 kWh at a time. The tariff
/// has a cheap band from 01:00 to 03:00 on the wall clock and a time rate on every minute.
/// </summary>
public sealed class DaylightSavingTests
{
    private static readonly TimeOfUseBand SmallHours =
        new("small hours", new TimeOnly(1, 0), new TimeOnly(3, 0));

    private static readonly TariffSnapshot Overnight = Version1(
            new EnergyRate(Usd(0.42m)),
            new EnergyRate(Usd(0.10m), SmallHours),
            new TimeRate(Usd(0.01m)))
        .Snapshot();

    [Fact]
    public void Given_a_charge_through_the_spring_forward_then_the_missing_hour_is_never_charged()
    {
        // 00:00 MST to 04:00 MDT on 8 March: four hours on the wall clock, three in real time.
        var usage = ChargeEveryMinute(
            from: new DateTimeOffset(2026, 3, 8, 7, 0, 0, TimeSpan.Zero), hours: 3);

        var price = PriceCalculator.Price(Overnight, usage);

        price.Lines.ShouldBe(
        [
            new EnergyCharge(null, new Energy(60m), Usd(0.42m), Usd(25.20m)),
            new EnergyCharge(SmallHours, new Energy(30m), Usd(0.10m), Usd(3.00m)),
            new TimeCharge(null, TimeSpan.FromHours(3), Usd(0.01m), Usd(1.80m)),
        ]);
    }

    [Fact]
    public void Given_a_charge_through_the_fall_back_then_the_repeated_hour_is_charged_twice()
    {
        // 00:00 MDT to 03:00 MST on 1 November: three hours on the wall clock, four in real time.
        var usage = ChargeEveryMinute(
            from: new DateTimeOffset(2026, 11, 1, 6, 0, 0, TimeSpan.Zero), hours: 4);

        var price = PriceCalculator.Price(Overnight, usage);

        price.Lines.ShouldBe(
        [
            new EnergyCharge(null, new Energy(30m), Usd(0.42m), Usd(12.60m)),
            new EnergyCharge(SmallHours, new Energy(90m), Usd(0.10m), Usd(9.00m)),
            new TimeCharge(null, TimeSpan.FromHours(4), Usd(0.01m), Usd(2.40m)),
        ]);
    }

    /// <summary>A charge point that reports 0.5 kWh every minute, timed by a fake clock.</summary>
    private static SessionUsage ChargeEveryMinute(DateTimeOffset from, int hours)
    {
        var clock = new FakeTimeProvider(from);
        var intervals = ImmutableArray.CreateBuilder<MeteredInterval>();

        for (var minute = 0; minute < hours * 60; minute++)
        {
            var start = clock.GetUtcNow();
            clock.Advance(TimeSpan.FromMinutes(1));
            intervals.Add(new MeteredInterval(start, clock.GetUtcNow(), new Energy(0.5m)));
        }

        return new SessionUsage(intervals.ToImmutable(), TimeSpan.Zero);
    }
}
