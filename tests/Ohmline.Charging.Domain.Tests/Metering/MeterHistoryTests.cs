using System.Collections.Immutable;
using Ohmline.Charging.Domain.Metering;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Domain.Tests.Metering;

public sealed class MeterHistoryTests
{
    private static readonly DateTimeOffset PluggedInAt = new(2026, 11, 4, 7, 52, 0, TimeSpan.Zero);

    [Fact]
    public void Given_no_reading_or_one_reading_then_no_energy_was_delivered()
    {
        MeterHistory.EnergyDelivered([]).ShouldBe(Result.Ok(Energy.Zero));
        MeterHistory.EnergyDelivered([Reading(0, 1_200_000)]).ShouldBe(Result.Ok(Energy.Zero));
    }

    [Fact]
    public void Given_a_register_that_only_counts_up_then_the_energy_is_last_minus_first()
    {
        MeterReading[] history =
            [Reading(0, 1_200_000), Reading(1, 1_200_410), Reading(2, 1_201_230)];

        MeterHistory.Check(history).ShouldBe(Result.Ok());
        MeterHistory.EnergyDelivered(history).ShouldBe(Result.Ok(new Energy(1.23m)));
    }

    [Fact]
    public void Given_a_register_that_went_down_and_back_up_then_the_rollback_is_found()
    {
        var start = Reading(0, 1_200_000);
        var reset = Reading(1, 50);
        MeterReading[] history = [start, reset, Reading(2, 1_201_230)];

        MeterHistory.EnergyDelivered(history)
            .ShouldBe(Result.Fail<MeterError>(new RegisterWentBackwards(start, reset)));
    }

    [Fact]
    public void Given_a_reading_out_of_order_in_the_history_then_the_check_reports_it()
    {
        var second = Reading(2, 1_200_410);
        var late = Reading(1, 1_200_800);
        MeterReading[] history = [Reading(0, 1_200_000), second, late];

        MeterHistory.Check(history)
            .ShouldBe(Result.Fail<MeterError>(new ReadingOutOfOrder(second, late)));
    }

    [Fact]
    public void Given_a_history_kept_in_state_then_it_is_checked_as_a_span()
    {
        ImmutableArray<MeterReading> history = [Reading(0, 1_200_000), Reading(1, 1_219_753)];

        MeterHistory.EnergyDelivered(history.AsSpan()).ShouldBe(Result.Ok(new Energy(19.753m)));
    }

    private static MeterReading Reading(int minute, long wattHours) =>
        MeterReading.FromWattHours(wattHours, PluggedInAt.AddMinutes(minute));
}
