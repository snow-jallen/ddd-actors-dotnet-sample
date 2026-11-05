using Ohmline.SharedKernel.Units;
using TallySpec = Ohmline.Testing.DeciderSpec<
    Ohmline.SharedKernel.Tests.Deciders.EnergyTally,
    Ohmline.SharedKernel.Units.Energy,
    Ohmline.SharedKernel.Tests.Deciders.Count,
    Ohmline.SharedKernel.Tests.Deciders.Counted,
    Ohmline.SharedKernel.Tests.Deciders.OverLimit>;

namespace Ohmline.SharedKernel.Tests.Deciders;

/// <summary>
/// The Given-When-Then harness on the smallest decider: what it accepts, what it refuses, and
/// what a failing test prints.
/// </summary>
public sealed class DeciderSpecTests
{
    private static readonly DateTimeOffset Morning = new(2026, 11, 4, 7, 52, 0, TimeSpan.Zero);

    [Fact]
    public void Given_a_history_when_a_command_is_accepted_then_its_events_are_returned()
    {
        var events = TallySpec.Given(Counted(60m))
            .At(Morning)
            .When(Count(40m))
            .Then(Counted(40m));

        events.ShouldBe([Counted(40m)]);
    }

    [Fact]
    public void Given_a_history_when_a_command_is_refused_then_the_reason_can_be_inspected()
    {
        TallySpec.Given(Counted(60m), Counted(30m))
            .At(Morning)
            .When(Count(20m))
            .ThenRejects<OverLimit>()
            .Total.ShouldBe(new Energy(110m));
    }

    [Fact]
    public void Given_events_were_expected_when_the_decider_records_others_then_it_prints_both()
    {
        var failure = Should.Throw<ShouldAssertException>(() => TallySpec.Given(Counted(60m))
            .At(Morning)
            .When(Count(40m))
            .Then(Counted(4m)));

        failure.Message.ShouldBe(string.Join(Environment.NewLine,
        [
            "",
            "Given",
            "    Counted { Energy = 60 kWh, At = 2026-11-04T07:52:00.0000000+00:00 }",
            "When, at 2026-11-04T07:52:00.0000000+00:00",
            "    Count { Energy = 40 kWh }",
            "Then it should record",
            "    Counted { Energy = 4 kWh, At = 2026-11-04T07:52:00.0000000+00:00 }",
            "But it recorded",
            "    Counted { Energy = 40 kWh, At = 2026-11-04T07:52:00.0000000+00:00 }",
            "The first difference is event 1 of 1.",
        ]));
    }

    [Fact]
    public void Given_events_were_expected_when_the_decider_refuses_then_it_prints_the_reason()
    {
        var failure = Should.Throw<ShouldAssertException>(() => TallySpec.Given()
            .At(Morning)
            .When(Count(120m))
            .ThenNothingChanges());

        failure.Message.ShouldEndWith(string.Join(Environment.NewLine,
        [
            "Then it should record",
            "    nothing",
            "But it refused",
            "    OverLimit { Total = 120 kWh }",
        ]));
    }

    [Fact]
    public void Given_a_refusal_was_expected_when_the_decider_accepts_then_it_prints_the_events()
    {
        var failure = Should.Throw<ShouldAssertException>(() => TallySpec.Given()
            .At(Morning)
            .When(Count(1m))
            .ThenRejects(new OverLimit(new Energy(101m))));

        failure.Message.ShouldContain("Then it should refuse");
        failure.Message.ShouldContain("But it recorded");
    }

    [Fact]
    public void Given_no_moment_for_the_decision_then_the_harness_asks_for_one()
    {
        Should.Throw<InvalidOperationException>(() => TallySpec.Given().When(Count(1m)))
            .Message.ShouldStartWith("Say when the decision is made");
    }

    private static Count Count(decimal kilowattHours) => new(new Energy(kilowattHours));

    private static Counted Counted(decimal kilowattHours) =>
        new(new Energy(kilowattHours), Morning);
}
