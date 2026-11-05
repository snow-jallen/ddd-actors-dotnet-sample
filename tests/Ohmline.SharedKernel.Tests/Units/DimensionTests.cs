using Ohmline.SharedKernel.Units;

namespace Ohmline.SharedKernel.Tests.Units;

public sealed class DimensionTests
{
    [Fact]
    public void Given_power_held_for_a_duration_when_multiplied_then_the_result_is_energy()
    {
        Energy delivered = new Power(50m) * TimeSpan.FromMinutes(30);

        delivered.ShouldBe(new Energy(25m));
        (TimeSpan.FromMinutes(30) * new Power(50m)).ShouldBe(delivered);
    }

    [Fact]
    public void Given_a_short_interval_when_multiplied_then_the_energy_is_exact()
    {
        var delivered = new Power(7.4m) * TimeSpan.FromSeconds(90);

        delivered.ShouldBe(Energy.FromWattHours(185m));
    }

    [Fact]
    public void Given_energy_over_a_duration_when_divided_then_the_result_is_average_power()
    {
        Power average = new Energy(25m) / TimeSpan.FromMinutes(30);

        average.ShouldBe(new Power(50m));
    }

    [Fact]
    public void Given_energy_over_no_time_when_divided_then_it_throws()
    {
        Should.Throw<DivideByZeroException>(() => new Energy(1m) / TimeSpan.Zero);
    }

    [Fact]
    public void Given_a_negative_duration_when_multiplied_then_the_energy_guard_throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new Power(11m) * TimeSpan.FromHours(-1));
    }

    [Fact]
    public void Given_voltage_and_current_on_one_phase_when_multiplied_then_the_result_is_power()
    {
        Power power = new Voltage(230m) * new Current(32m);

        power.ShouldBe(new Power(7.36m));
    }
}
