using CsCheck;
using Ohmline.SharedKernel.Units;
using static Ohmline.SharedKernel.Tests.Generators;
using Gen = CsCheck.Gen;

namespace Ohmline.SharedKernel.Tests.Units;

public sealed class UnitPropertyTests
{
    [Fact]
    public void Adding_energy_is_associative()
    {
        Gen.Select(Energies, Energies, Energies)
            .Sample((a, b, c) => ((a + b) + c).ShouldBe(a + (b + c)));
    }

    [Fact]
    public void Adding_energy_is_commutative()
    {
        Gen.Select(Energies, Energies).Sample((a, b) => (a + b).ShouldBe(b + a));
    }

    [Fact]
    public void Zero_energy_changes_nothing_and_subtraction_undoes_addition()
    {
        Gen.Select(Energies, Energies).Sample((a, b) =>
        {
            (a + Energy.Zero).ShouldBe(a);
            (a + b - b).ShouldBe(a);
        });
    }

    [Fact]
    public void More_power_for_the_same_time_never_delivers_less_energy()
    {
        Gen.Select(Powers, Powers, Durations).Sample((p1, p2, duration) =>
        {
            var (lower, higher) = p1 <= p2 ? (p1, p2) : (p2, p1);
            (lower * duration).KilowattHours
                .ShouldBeLessThanOrEqualTo((higher * duration).KilowattHours);
        });
    }

    [Fact]
    public void The_same_power_for_longer_never_delivers_less_energy()
    {
        Gen.Select(Powers, Durations, Durations).Sample((power, t1, t2) =>
        {
            var (shorter, longer) = t1 <= t2 ? (t1, t2) : (t2, t1);
            (power * shorter).KilowattHours
                .ShouldBeLessThanOrEqualTo((power * longer).KilowattHours);
        });
    }

    [Fact]
    public void Energy_over_its_duration_gives_back_the_power_to_a_milliwatt()
    {
        Gen.Select(Powers, Durations.Where(duration => duration > TimeSpan.Zero))
            .Sample((power, duration) =>
            {
                var average = power * duration / duration;
                Math.Abs(average.Kilowatts - power.Kilowatts).ShouldBeLessThan(0.000_001m);
            });
    }

    [Fact]
    public void Try_create_accepts_exactly_what_the_guard_accepts()
    {
        Gen.Decimal[-1_000_000m, 1_000_000m].Sample(kilowatts =>
            Power.TryCreate(kilowatts).IsOk.ShouldBe(kilowatts >= 0m));
    }
}
