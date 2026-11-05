using CsCheck;
using Ohmline.SharedKernel.Units;
using Gen = CsCheck.Gen;
using TallyChecks = Ohmline.Testing.DeciderProperties<
    Ohmline.SharedKernel.Tests.Deciders.EnergyTally,
    Ohmline.SharedKernel.Units.Energy,
    Ohmline.SharedKernel.Tests.Deciders.Count,
    Ohmline.SharedKernel.Tests.Deciders.Counted,
    Ohmline.SharedKernel.Tests.Deciders.OverLimit>;

namespace Ohmline.SharedKernel.Tests.Deciders;

/// <summary>The property helper on the smallest decider: what it checks and how it fails.</summary>
public sealed class DeciderPropertiesTests
{
    private static readonly DateTimeOffset Morning = new(2026, 11, 4, 7, 52, 0, TimeSpan.Zero);

    /// <summary>Counts of 0 to 30 kWh, a minute apart: enough of them pass the limit.</summary>
    private static readonly Gen<System.Collections.Immutable.ImmutableArray<TallyChecks.Timed>>
        Scripts = TallyChecks.Scripts(
            Gen.Int[0, 30].Select(kilowattHours => new Count(new Energy(kilowattHours))),
            Gen.Const(TimeSpan.FromMinutes(1)),
            20);

    [Fact]
    public void Given_a_script_when_run_then_each_step_starts_where_the_last_one_ended()
    {
        var steps = TallyChecks.Run(Morning,
        [
            new(TimeSpan.Zero, new Count(new Energy(60m))),
            new(TimeSpan.FromMinutes(5), new Count(new Energy(50m))),
            new(TimeSpan.FromMinutes(5), new Count(new Energy(40m))),
        ]);

        steps.Select(step => (step.Before, step.After)).ShouldBe(
        [
            (Energy.Zero, new Energy(60m)),
            (new Energy(60m), new Energy(60m)),
            (new Energy(60m), new Energy(100m)),
        ]);
        steps.Select(step => step.At)
            .ShouldBe([Morning, Morning.AddMinutes(5), Morning.AddMinutes(10)]);
        steps[1].Events.ShouldBeEmpty();
    }

    [Fact]
    public void Given_the_tally_then_its_total_never_goes_down_and_never_passes_the_limit()
    {
        TallyChecks.Monotonic(Scripts, Morning, "The total", total => total);
        TallyChecks.Invariant(
            Scripts, Morning, "The total is within the limit", total => total <= EnergyTally.Limit);
        TallyChecks.EvolveIsTotal(Scripts, Morning);
    }

    [Fact]
    public void Given_a_property_that_is_false_then_the_failure_names_the_decision()
    {
        var failure = Should.Throw<CsCheckException>(() => TallyChecks.Invariant(
            Scripts, Morning, "The total is under 50 kWh", total => total < new Energy(50m)));

        failure.InnerException.ShouldBeOfType<ShouldAssertException>().Message
            .ShouldMatch(@"^After decision \d+ of \d+, Count \{ Energy = \d+ kWh \}: "
                         + @"The total is under 50 kWh is false of \d+ kWh\.$");
    }
}
