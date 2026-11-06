using System.Collections.Immutable;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using Ohmline.Tariffs.Contracts;
using Ohmline.Tariffs.Domain.Pricing;
using static Ohmline.Tariffs.Domain.Tests.TheDepotTariff;

namespace Ohmline.Tariffs.Domain.Tests.Pricing;

/// <summary>
/// The pricing analyst's examples. Times are the depot's local time on 2026-11-04, when Denver
/// is on standard time (UTC-7).
/// </summary>
public sealed class PriceCalculatorTests
{
    private static readonly SessionUsage PrimersCharge = Usage(
        idle: TimeSpan.FromMinutes(25),
        (Local(7, 52), Local(8, 15), 12.0m),
        (Local(8, 15), Local(8, 40), 15.6m));

    [Fact]
    public void Given_the_primers_charge_then_it_costs_16_dollars_59()
    {
        var price = PriceCalculator.Price(Version1().Snapshot(), PrimersCharge);

        price.Lines.ShouldBe(
        [
            new EnergyCharge(null, new Energy(27.6m), Usd(0.42m), Usd(11.59m)),
            new SessionFeeCharge(Usd(1.00m)),
            new IdleFeeCharge(10, Usd(0.40m), Usd(4.00m)),
        ]);
        price.Total.ShouldBe(Usd(16.59m));
    }

    [Fact]
    public void Given_a_charge_that_runs_into_the_peak_then_each_kWh_has_its_bands_rate()
    {
        var tariff = Snapshot(new EnergyRate(Usd(0.42m)), new EnergyRate(Usd(0.60m), Peak));

        var price = PriceCalculator.Price(
            tariff, Usage(TimeSpan.Zero, (Local(15, 30), Local(16, 30), 20m)));

        price.Lines.ShouldBe(
        [
            new EnergyCharge(null, new Energy(10m), Usd(0.42m), Usd(4.20m)),
            new EnergyCharge(Peak, new Energy(10m), Usd(0.60m), Usd(6.00m)),
        ]);
    }

    [Fact]
    public void Given_a_time_rate_in_the_peak_then_only_the_peak_minutes_are_charged()
    {
        var tariff = Snapshot(BaseRate, new TimeRate(Usd(0.05m), Peak));

        var price = PriceCalculator.Price(
            tariff, Usage(TimeSpan.Zero, (Local(15, 30), Local(16, 30), 20m)));

        price.Lines.ShouldBe(
        [
            new EnergyCharge(null, new Energy(20m), Usd(0.42m), Usd(8.40m)),
            new TimeCharge(Peak, TimeSpan.FromMinutes(30), Usd(0.05m), Usd(1.50m)),
        ]);
    }

    [Fact]
    public void Given_energy_when_no_rate_is_in_force_then_it_is_not_charged()
    {
        var peakOnly = Snapshot(new EnergyRate(Usd(0.60m), Peak));

        var price = PriceCalculator.Price(
            peakOnly, Usage(TimeSpan.Zero, (Local(15, 30), Local(16, 30), 20m)));

        price.Lines.ShouldBe([new EnergyCharge(Peak, new Energy(10m), Usd(0.60m), Usd(6.00m))]);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(15, 0)]
    [InlineData(15.9, 0)]
    [InlineData(16, 1)]
    [InlineData(25.5, 10)]
    public void Given_idle_minutes_then_only_whole_minutes_after_the_grace_period_are_charged(
        double idleMinutes, int charged)
    {
        var usage = PrimersCharge with { Idle = TimeSpan.FromMinutes(idleMinutes) };

        PriceCalculator.Price(Version1().Snapshot(), usage).Lines.OfType<IdleFeeCharge>()
            .Sum(line => line.Minutes).ShouldBe(charged);
    }

    [Fact]
    public void Given_an_idle_fee_cap_then_the_idle_fee_stops_at_it()
    {
        var capped = Version1(BaseRate, Fee, Idle with { Cap = Usd(2.50m) }).Snapshot();

        var price = PriceCalculator.Price(capped, PrimersCharge);

        price.Lines.OfType<IdleFeeCharge>().ShouldHaveSingleItem()
            .ShouldBe(new IdleFeeCharge(10, Usd(0.40m), Usd(2.50m)));
        price.Total.ShouldBe(Usd(15.09m));
    }

    [Fact]
    public void Given_an_idle_fee_cap_above_the_fee_then_the_fee_is_charged_in_full()
    {
        var capped = Version1(BaseRate, Fee, Idle with { Cap = Usd(12.00m) }).Snapshot();

        PriceCalculator.Price(capped, PrimersCharge).Lines.OfType<IdleFeeCharge>()
            .ShouldHaveSingleItem().Amount.ShouldBe(Usd(4.00m));
    }

    [Fact]
    public void Given_a_price_above_the_cap_then_the_cap_takes_off_the_difference()
    {
        var capped = Version1().Snapshot() with { PriceCap = Usd(10.00m) };

        var price = PriceCalculator.Price(capped, PrimersCharge);

        price.Lines[^1].ShouldBe(new PriceCapApplied(Usd(10.00m), Usd(-6.59m)));
        price.Total.ShouldBe(Usd(10.00m));
    }

    [Fact]
    public void Given_a_price_below_the_cap_then_the_cap_does_not_show()
    {
        var capped = Version1().Snapshot() with { PriceCap = Usd(20.00m) };

        PriceCalculator.Price(capped, PrimersCharge).Lines.OfType<PriceCapApplied>()
            .ShouldBeEmpty();
    }

    [Fact]
    public void Given_fractions_of_a_cent_then_each_line_is_rounded_once_and_adds_up()
    {
        // 7.123 kWh at USD 0.42 is USD 2.99166, and 7.5 minutes at USD 0.333 is USD 2.4975.
        var tariff = Snapshot(BaseRate, new TimeRate(Usd(0.333m)));

        var price = PriceCalculator.Price(tariff, Usage(
            TimeSpan.Zero, (Local(9, 0), Local(9, 7).AddSeconds(30), 7.123m)));

        price.Lines.Select(line => line.Amount).ShouldBe([Usd(2.99m), Usd(2.50m)]);
        price.Total.ShouldBe(Usd(5.49m));
    }

    [Fact]
    public void Given_the_same_snapshot_and_usage_then_the_price_is_the_same_every_time()
    {
        var snapshot = Version1().Snapshot();

        PriceCalculator.Price(snapshot, PrimersCharge)
            .ShouldBe(PriceCalculator.Price(snapshot, PrimersCharge));
    }

    internal static DateTimeOffset Local(int hour, int minute) =>
        new(2026, 11, 4, hour, minute, 0, TimeSpan.FromHours(-7));

    internal static SessionUsage Usage(
        TimeSpan idle, params (DateTimeOffset From, DateTimeOffset Until, decimal KWh)[] meter) =>
        new([.. meter.Select(read => new MeteredInterval(read.From, read.Until, new(read.KWh)))],
            idle);

    private static TariffSnapshot Snapshot(params TariffComponent[] components) =>
        Version1(components).Snapshot();
}
