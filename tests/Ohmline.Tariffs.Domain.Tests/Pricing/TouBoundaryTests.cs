using System.Collections.Immutable;
using System.Globalization;
using Microsoft.Extensions.Time.Testing;
using Ohmline.SharedKernel.Units;
using Ohmline.Tariffs.Contracts;
using Ohmline.Tariffs.Domain.Pricing;
using static Ohmline.Tariffs.Domain.Tests.TheDepotTariff;

namespace Ohmline.Tariffs.Domain.Tests.Pricing;

/// <summary>
/// Where one time-of-use band ends and the next begins: at the minute, across midnight, and on
/// the nights Denver changes its clocks. A fake clock plays a 60 kW charge point that reports
/// 1 kWh every minute. The depot's base rate is USD 0.42, the peak (16:00 to 21:00) USD 0.60 and
/// the night (22:00 to 06:00) USD 0.25.
/// </summary>
public sealed class TouBoundaryTests
{
    private static readonly TariffSnapshot DayAndNight = Version1(
            BaseRate, new EnergyRate(Usd(0.60m), Peak), new EnergyRate(Usd(0.25m), Night))
        .Snapshot();

    [Theory]
    [InlineData("15:59", "base")]
    [InlineData("16:00", "peak")]
    [InlineData("20:59", "peak")]
    [InlineData("21:00", "base")]
    [InlineData("21:59", "base")]
    [InlineData("22:00", "night")]
    [InlineData("23:59", "night")]
    [InlineData("00:00", "night")]
    [InlineData("05:59", "night")]
    [InlineData("06:00", "base")]
    public void Given_one_minute_of_charging_then_its_band_is_the_one_the_wall_clock_shows(
        string localTime, string band)
    {
        var minute = TimeOnly.ParseExact(localTime, "HH:mm", CultureInfo.InvariantCulture);
        var start = new DateTimeOffset(
            new DateOnly(2026, 11, 4), minute, TimeSpan.FromHours(-7));

        var price = PriceCalculator.Price(DayAndNight, Charge(start, TimeSpan.FromMinutes(1)));

        price.Lines.ShouldHaveSingleItem().ShouldBeOfType<EnergyCharge>().Band?.Name
            .ShouldBe(band == "base" ? null : band);
    }

    [Fact]
    public void Given_a_charge_across_midnight_then_the_night_band_carries_on_into_the_next_day()
    {
        // 21:30 on 4 November to 00:30 on 5 November, MST.
        var price = PriceCalculator.Price(
            DayAndNight,
            Charge(new DateTimeOffset(2026, 11, 4, 21, 30, 0, TimeSpan.FromHours(-7)),
                TimeSpan.FromHours(3)));

        EnergyIn(price, band: null).ShouldBe(new Energy(30m));
        EnergyIn(price, Night).ShouldBe(new Energy(150m));
    }

    [Fact]
    public void Given_the_night_the_clocks_go_back_then_the_night_band_lasts_nine_real_hours()
    {
        // 21:00 MDT on 31 October to 07:00 MST on 1 November: eleven real hours.
        var price = PriceCalculator.Price(
            DayAndNight,
            Charge(
                new DateTimeOffset(2026, 11, 1, 3, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(11)));

        EnergyIn(price, band: null).ShouldBe(new Energy(120m));
        EnergyIn(price, Night).ShouldBe(new Energy(540m));
    }

    [Fact]
    public void Given_the_night_the_clocks_go_forward_then_the_night_band_lasts_seven_real_hours()
    {
        // 21:00 MST on 7 March to 07:00 MDT on 8 March: nine real hours.
        var price = PriceCalculator.Price(
            DayAndNight,
            Charge(
                new DateTimeOffset(2026, 3, 8, 4, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(9)));

        EnergyIn(price, band: null).ShouldBe(new Energy(120m));
        EnergyIn(price, Night).ShouldBe(new Energy(420m));
    }

    /// <summary>A charge point on a fake clock: 1 kWh read at the end of every minute.</summary>
    private static SessionUsage Charge(DateTimeOffset from, TimeSpan duration)
    {
        var clock = new FakeTimeProvider(from);
        var intervals = ImmutableArray.CreateBuilder<MeteredInterval>();

        while (clock.GetUtcNow() < from + duration)
        {
            var start = clock.GetUtcNow();
            clock.Advance(TimeSpan.FromMinutes(1));
            intervals.Add(new MeteredInterval(start, clock.GetUtcNow(), new Energy(1m)));
        }

        return new SessionUsage(intervals.ToImmutable(), TimeSpan.Zero);
    }

    private static Energy EnergyIn(PriceBreakdown price, TimeOfUseBand? band) =>
        price.Lines.OfType<EnergyCharge>().SingleOrDefault(line => line.Band == band)?.Energy
        ?? Energy.Zero;
}
