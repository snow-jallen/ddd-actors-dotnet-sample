using System.Numerics;
using Ohmline.SharedKernel.Units;

namespace Ohmline.SharedKernel.Tests.Units;

public sealed class UnitTests
{
    [Fact]
    public void Given_a_negative_amount_when_any_unit_is_constructed_then_the_guard_throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new Energy(-0.001m))
            .ParamName.ShouldBe("KilowattHours");
        Should.Throw<ArgumentOutOfRangeException>(() => new Power(-1m))
            .ParamName.ShouldBe("Kilowatts");
        Should.Throw<ArgumentOutOfRangeException>(() => new Current(-1m))
            .ParamName.ShouldBe("Amperes");
        Should.Throw<ArgumentOutOfRangeException>(() => new Voltage(-1m))
            .ParamName.ShouldBe("Volts");
    }

    [Fact]
    public void Given_a_valid_power_when_copied_with_a_negative_value_then_the_guard_throws()
    {
        var power = new Power(22m);

        Should.Throw<ArgumentOutOfRangeException>(() => power with { Kilowatts = -22m });
    }

    [Fact]
    public void Given_two_amounts_of_energy_when_added_then_the_result_is_energy()
    {
        var total = new Energy(12.5m) + new Energy(7.25m);

        total.ShouldBe(new Energy(19.75m));
    }

    [Fact]
    public void Given_two_powers_when_subtracted_then_the_result_is_power()
    {
        var headroom = new Power(200m) - new Power(10m);

        headroom.ShouldBe(new Power(190m));
    }

    [Fact]
    public void Given_more_energy_taken_away_than_there_is_then_the_guard_throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new Energy(1m) - new Energy(2m));
    }

    [Fact]
    public void Given_quantities_of_one_unit_when_compared_then_their_amounts_are_compared()
    {
        (new Power(7.4m) < new Power(22m)).ShouldBeTrue();
        (new Current(32m) >= new Current(32m)).ShouldBeTrue();
        (new Energy(1m) > new Energy(1.5m)).ShouldBeFalse();
        (new Voltage(230m) <= new Voltage(400m)).ShouldBeTrue();
    }

    [Fact]
    public void Given_amounts_with_different_scales_then_equal_amounts_are_equal()
    {
        new Energy(19m).ShouldBe(new Energy(19.000m));
        new Energy(19m).GetHashCode().ShouldBe(new Energy(19.000m).GetHashCode());
    }

    [Fact]
    public void Given_watts_and_watt_hours_when_converted_then_kilo_units_are_kept()
    {
        Power.FromWatts(7_400m).ShouldBe(new Power(7.4m));
        Energy.FromWattHours(1_230m).ShouldBe(new Energy(1.23m));
        new Energy(1.23m).WattHours.ShouldBe(1_230m);
        new Power(7.4m).Watts.ShouldBe(7_400m);
    }

    [Fact]
    public void Given_a_quantity_when_printed_then_it_shows_its_unit()
    {
        new Energy(19.75m).ToString().ShouldBe("19.75 kWh");
        new Power(7.4m).ToString().ShouldBe("7.4 kW");
        new Current(32m).ToString().ShouldBe("32 A");
        new Voltage(230m).ToString().ShouldBe("230 V");
    }

    [Fact]
    public void Given_a_default_unit_then_it_is_zero()
    {
        default(Energy).ShouldBe(Energy.Zero);
        Power.Zero.Kilowatts.ShouldBe(0m);
    }

    [Fact]
    public void Given_allocations_when_summed_by_generic_code_then_the_total_is_power()
    {
        Power[] allocations = [new(38m), new(38m), new(22m)];

        Sum(allocations).ShouldBe(new Power(98m));
        Sum<Energy>([]).ShouldBe(Energy.Zero);
    }

    private static T Sum<T>(IEnumerable<T> values)
        where T : IAdditionOperators<T, T, T>, IAdditiveIdentity<T, T> =>
        values.Aggregate(T.AdditiveIdentity, (total, value) => total + value);
}
