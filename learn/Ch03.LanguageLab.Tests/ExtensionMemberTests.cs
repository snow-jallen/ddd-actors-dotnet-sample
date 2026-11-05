using System.Collections.Immutable;

namespace Ch03.LanguageLab.Tests;

public sealed class ExtensionMemberTests
{
    private static readonly DateTimeOffset PluggedInAt = new(2026, 11, 4, 7, 52, 0, TimeSpan.Zero);

    [Fact]
    public void Given_a_decimal_when_read_as_kW_then_it_is_power()
    {
        22m.kW.ShouldBe(new Kilowatts(22m));
    }

    [Fact]
    public void Given_a_negative_decimal_when_read_as_kW_then_the_guard_still_runs()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => (-1m).kW);
    }

    [Fact]
    public void Given_watts_when_converted_by_a_static_extension_then_it_is_kilowatts()
    {
        Kilowatts.FromWatts(7_400m).ShouldBe(7.4m.kW);
    }

    [Fact]
    public void Given_power_and_a_duration_when_multiplied_then_the_result_is_energy()
    {
        var energy = 38m.kW * TimeSpan.FromMinutes(30);

        energy.ShouldBe(new KilowattHours(19m));
        energy.ToString().ShouldBe("19 kWh");
    }

    [Fact]
    public void Given_a_meter_history_when_indexed_by_time_then_the_last_earlier_sample_is_found()
    {
        ImmutableArray<MeterSample> history =
        [
            new(PluggedInAt, 1_200_000),
            new(PluggedInAt.AddMinutes(1), 1_200_600),
            new(PluggedInAt.AddMinutes(2), 1_201_230),
        ];

        history[PluggedInAt.AddSeconds(90)].ShouldBe(history[1]);
        history[PluggedInAt.AddHours(1)].ShouldBe(history[2]);
        history[PluggedInAt.AddMinutes(-1)].ShouldBeNull();
    }
}
