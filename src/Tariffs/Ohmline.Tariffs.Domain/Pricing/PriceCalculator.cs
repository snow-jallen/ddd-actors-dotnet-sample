using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.Tariffs.Domain.Pricing;

/// <summary>
/// Prices a session: a domain service, because pricing is an operation of the domain that
/// belongs to no entity. It is a pure function of the data it is given. It reads no clock, asks
/// no tariff book and calls no service: whoever needs a price brings the tariff snapshot and the
/// usage (section 8.3).
/// </summary>
public static class PriceCalculator
{
    /// <summary>
    /// One line per component of the tariff that applies, in the tariff's order: energy and
    /// time are split across the time-of-use bands in site-local time, minute by minute. Each
    /// line is rounded once with the tariff's rounding policy, then the price cap applies.
    /// </summary>
    public static PriceBreakdown Price(TariffSnapshot tariff, SessionUsage usage)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(tariff.TimeZoneId);
        var portions = usage.Intervals
            .SelectMany(interval => Portions(interval, tariff, zone))
            .ToList();

        var lines = tariff.Components
            .SelectMany(component => Lines(component, portions, usage))
            .Select(line => line with { Amount = tariff.Rounding.Round(line.Amount) });

        return Capped(new PriceBreakdown(tariff.Currency, [.. lines]), tariff.PriceCap);
    }

    private static IEnumerable<PriceLine> Lines(
        TariffComponent component, IReadOnlyList<Portion> portions, SessionUsage usage) =>
        component switch
        {
            EnergyRate rate => portions.Where(portion => portion.EnergyRate == rate)
                .Sum(portion => portion.KilowattHours) is var kWh and > 0m
                ? [new EnergyCharge(rate.Band, new Energy(kWh), rate.PricePerKwh,
                    rate.PricePerKwh * kWh)]
                : [],
            TimeRate rate => portions.Where(portion => portion.TimeRate == rate)
                .Aggregate(TimeSpan.Zero, (time, portion) => time + portion.Length) is var time
                && time > TimeSpan.Zero
                ? [new TimeCharge(rate.Band, time, rate.PricePerMinute,
                    rate.PricePerMinute * Minutes(time))]
                : [],
            SessionFee fee => [new SessionFeeCharge(fee.Amount)],
            IdleFee fee => WholeMinutes(usage.Idle - fee.GracePeriod) is var minutes and > 0
                ? [new IdleFeeCharge(minutes, fee.PricePerMinute, fee.PricePerMinute * minutes)]
                : [],
        };

    private static PriceBreakdown Capped(PriceBreakdown breakdown, Money? cap) =>
        cap is { } most && breakdown.Total > most
            ? breakdown with
            {
                Lines = [.. breakdown.Lines, new PriceCapApplied(most, most - breakdown.Total)],
            }
            : breakdown;

    /// <summary>
    /// The interval cut where the rates in force change. Each minute is placed by its local
    /// time, and an interval's energy is shared in proportion to time, so an interval that
    /// stays in one band keeps its energy exactly.
    /// </summary>
    private static IEnumerable<Portion> Portions(
        MeteredInterval interval, TariffSnapshot tariff, TimeZoneInfo zone)
    {
        var energyRates = tariff.Components.OfType<EnergyRate>().ToList();
        var timeRates = tariff.Components.OfType<TimeRate>().ToList();

        return MinutesOf(interval)
            .GroupBy(minute =>
            {
                var local = LocalTime(minute.Start, zone);
                return (
                    Energy: RateAt(energyRates, rate => rate.Band, local),
                    Time: RateAt(timeRates, rate => rate.Band, local));
            })
            .Select(rates =>
            {
                var length = rates.Aggregate(TimeSpan.Zero, (sum, minute) => sum + minute.Length);
                var kWh = interval.Energy.KilowattHours * length.Ticks / interval.Length.Ticks;
                return new Portion(rates.Key.Energy, rates.Key.Time, length, kWh);
            });
    }

    /// <summary>The interval cut at every whole minute, so no piece spans two bands.</summary>
    private static IEnumerable<(DateTimeOffset Start, TimeSpan Length)> MinutesOf(
        MeteredInterval interval)
    {
        for (var start = interval.From; start < interval.Until;)
        {
            var nextMinute = start.AddTicks(
                TimeSpan.TicksPerMinute - (start.UtcTicks % TimeSpan.TicksPerMinute));
            var end = nextMinute < interval.Until ? nextMinute : interval.Until;
            yield return (start, end - start);
            start = end;
        }
    }

    /// <summary>The rate whose band holds the time, else the one without a band, or none.</summary>
    private static TRate? RateAt<TRate>(
        IReadOnlyList<TRate> rates, Func<TRate, TimeOfUseBand?> bandOf, TimeOnly localTime)
        where TRate : class =>
        rates.FirstOrDefault(rate => bandOf(rate)?.Contains(localTime) == true)
        ?? rates.FirstOrDefault(rate => bandOf(rate) is null);

    /// <summary>The wall-clock time at the site at that instant.</summary>
    private static TimeOnly LocalTime(DateTimeOffset instant, TimeZoneInfo zone) =>
        TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

    private static decimal Minutes(TimeSpan time) => (decimal)time.Ticks / TimeSpan.TicksPerMinute;

    private static int WholeMinutes(TimeSpan time) =>
        time > TimeSpan.Zero ? (int)(time.Ticks / TimeSpan.TicksPerMinute) : 0;

    private sealed record Portion(
        EnergyRate? EnergyRate, TimeRate? TimeRate, TimeSpan Length, decimal KilowattHours);
}
