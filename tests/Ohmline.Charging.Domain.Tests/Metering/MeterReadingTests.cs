using Ohmline.Charging.Domain.Metering;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Domain.Tests.Metering;

public sealed class MeterReadingTests
{
    private static readonly DateTimeOffset PluggedInAt = new(2026, 11, 4, 7, 52, 0, TimeSpan.Zero);

    [Fact]
    public void Given_two_readings_in_order_when_comparing_then_the_energy_is_the_difference()
    {
        var start = MeterReading.FromWattHours(1_200_000, PluggedInAt);
        var later = MeterReading.FromWattHours(1_219_753, PluggedInAt.AddMinutes(30));

        later.EnergySince(start).ShouldBe(Result.Ok(new Energy(19.753m)));
    }

    [Fact]
    public void Given_a_reading_repeated_when_comparing_then_no_energy_was_delivered()
    {
        var reading = MeterReading.FromWattHours(1_200_000, PluggedInAt);

        reading.EnergySince(reading).ShouldBe(Result.Ok(Energy.Zero));
    }

    [Fact]
    public void Given_a_lower_register_later_when_comparing_then_the_register_went_backwards()
    {
        var start = MeterReading.FromWattHours(1_200_000, PluggedInAt);
        var later = MeterReading.FromWattHours(1_000, PluggedInAt.AddMinutes(5));

        later.EnergySince(start)
            .ShouldBe(Result.Fail<MeterError>(new RegisterWentBackwards(start, later)));
    }

    [Fact]
    public void Given_a_reading_taken_earlier_when_comparing_then_it_is_out_of_order()
    {
        var start = MeterReading.FromWattHours(1_200_000, PluggedInAt);
        var earlier = MeterReading.FromWattHours(1_190_000, PluggedInAt.AddMinutes(-1));

        earlier.EnergySince(start)
            .ShouldBe(Result.Fail<MeterError>(new ReadingOutOfOrder(start, earlier)));
    }

    [Fact]
    public void Given_watt_hours_from_a_charge_point_then_the_register_is_energy_in_kwh()
    {
        MeterReading.FromWattHours(1_219_753, PluggedInAt).Register
            .ShouldBe(new Energy(1_219.753m));
    }
}
