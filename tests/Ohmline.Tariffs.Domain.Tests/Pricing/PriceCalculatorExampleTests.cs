using System.Globalization;
using Ohmline.SharedKernel.Monetary;
using Ohmline.Tariffs.Contracts;
using Ohmline.Tariffs.Domain.Pricing;
using static Ohmline.Tariffs.Domain.Tests.TheDepotTariff;

namespace Ohmline.Tariffs.Domain.Tests.Pricing;

/// <summary>
/// The pricing analyst's worked examples, one per row of <c>pricing-examples.csv</c>. The analyst
/// adds a row with a spreadsheet and the calculator must agree to the cent, line by line. Times
/// are the depot's wall clock on 2026-11-04 (UTC-7); a time earlier than the one before it is
/// on the next day.
/// </summary>
public sealed class PriceCalculatorExampleTests
{
    private static readonly IReadOnlyDictionary<string, TariffSnapshot> Tariffs =
        new Dictionary<string, TariffSnapshot>
        {
            ["standard"] = Version1().Snapshot(),
            ["peak"] = Version1(BaseRate, new EnergyRate(Usd(0.60m), Peak), Fee, Idle).Snapshot(),
            ["night"] = Version1(BaseRate, new EnergyRate(Usd(0.25m), Night), Fee, Idle).Snapshot(),
            ["time rate"] = Version1(BaseRate, new TimeRate(Usd(0.05m))).Snapshot(),
            ["capped"] = Version1().Snapshot() with { PriceCap = Usd(10.00m) },
        };

    private static readonly IReadOnlyList<Example> Examples = Example.ReadAll();

    public static TheoryData<string> Names => new(Examples.Select(example => example.Name));

    [Theory]
    [MemberData(nameof(Names))]
    public void Given_an_example_from_the_pricing_analyst_then_the_calculator_agrees(string name)
    {
        var example = Examples.Single(example => example.Name == name);

        var price = PriceCalculator.Price(Tariffs[example.Tariff], example.Usage);

        Describe(price).ShouldBe(example.Breakdown);
        price.Total.ShouldBe(Usd(example.PriceUsd));
    }

    [Fact]
    public void Given_the_analysts_file_then_every_tariff_it_names_is_known()
    {
        Examples.ShouldNotBeEmpty();
        Examples.ShouldAllBe(example => Tariffs.ContainsKey(example.Tariff));
    }

    /// <summary>In the analyst's words: "energy (peak) 6.00; session fee 1.00".</summary>
    private static string Describe(PriceBreakdown price) =>
        string.Join("; ", price.Lines.Select(line => line switch
        {
            EnergyCharge { Band: { } band } => $"energy ({band.Name})",
            EnergyCharge => "energy",
            TimeCharge { Band: { } band } => $"time ({band.Name})",
            TimeCharge => "time",
            SessionFeeCharge => "session fee",
            IdleFeeCharge => "idle fee",
            PriceCapApplied => "price cap",
        } + " " + line.Amount.Amount.ToString("0.00", CultureInfo.InvariantCulture)));

    /// <summary>One row of the analyst's file.</summary>
    private sealed record Example(
        string Name, string Tariff, SessionUsage Usage, decimal PriceUsd, string Breakdown)
    {
        private static readonly DateTimeOffset Midnight =
            new(2026, 11, 4, 0, 0, 0, TimeSpan.FromHours(-7));

        public static IReadOnlyList<Example> ReadAll() =>
        [
            .. File.ReadLines(Path.Combine(AppContext.BaseDirectory, "pricing-examples.csv"))
                .Skip(1)
                .Where(line => line.Length > 0)
                .Select(Parse),
        ];

        private static Example Parse(string line) => line.Split(',') switch
        {
            [var name, var tariff, var pluggedIn, var stopped, var kWh, var unplugged,
                var price, var breakdown] => Of(
                    name, tariff, pluggedIn, stopped, kWh, unplugged, price, breakdown),
            var fields => throw new FormatException(
                $"An example has 8 fields, not {fields.Length}: '{line}'."),
        };

        private static Example Of(
            string name,
            string tariff,
            string pluggedIn,
            string stopped,
            string kWh,
            string unplugged,
            string price,
            string breakdown)
        {
            var from = Midnight + Time(pluggedIn);
            var until = After(from, Time(stopped));
            var interval = new MeteredInterval(from, until, new(Number(kWh)));

            return new Example(
                name,
                tariff,
                new SessionUsage([interval], After(until, Time(unplugged)) - until),
                Number(price),
                breakdown);
        }

        /// <summary>The next time the wall clock shows this time, from the moment given.</summary>
        private static DateTimeOffset After(DateTimeOffset moment, TimeSpan timeOfDay)
        {
            var sameDay = moment - moment.TimeOfDay + timeOfDay;
            return sameDay >= moment ? sameDay : sameDay.AddDays(1);
        }

        private static TimeSpan Time(string text) =>
            TimeOnly.ParseExact(text, "HH:mm", CultureInfo.InvariantCulture).ToTimeSpan();

        private static decimal Number(string text) =>
            decimal.Parse(text, CultureInfo.InvariantCulture);
    }
}
