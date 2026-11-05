using System.Collections.Immutable;
using Ohmline.SharedKernel.Deciders;
using Ohmline.SharedKernel.Units;

namespace Ohmline.SharedKernel.Tests.Deciders;

public sealed class DeciderTests
{
    private static readonly DateTimeOffset Morning = new(2026, 11, 4, 7, 52, 0, TimeSpan.Zero);

    [Fact]
    public void Given_no_events_when_folded_then_the_state_is_the_deciders_initial_state()
    {
        Fold([]).ShouldBe(Energy.Zero);
    }

    [Fact]
    public void Given_events_when_folded_then_each_one_is_evolved_in_order()
    {
        Fold([Counted(12.5m), Counted(7.25m)]).ShouldBe(new Energy(19.75m));
    }

    [Fact]
    public void Given_a_snapshot_and_the_events_after_it_when_folded_then_it_is_the_full_fold()
    {
        Counted[] history = [Counted(10m), Counted(20m), Counted(30m), Counted(5m)];
        var snapshot = Fold(history[..2]);

        Decider.Fold<EnergyTally, Energy, Count, Counted, OverLimit>(snapshot, history[2..])
            .ShouldBe(Fold(history));
    }

    [Fact]
    public void Given_any_decider_then_one_generic_loop_can_run_it()
    {
        var (state, journal) = Run<EnergyTally, Energy, Count, Counted, OverLimit>(
            [new Count(new Energy(60m)), new Count(new Energy(50m)), new Count(new Energy(40m))],
            new DecisionContext(Morning));

        state.ShouldBe(new Energy(100m));
        journal.ShouldBe([Counted(60m), Counted(40m)]);
    }

    [Fact]
    public void Given_a_context_then_the_decider_records_its_time_and_never_reads_a_clock()
    {
        var noon = Morning.AddHours(4);

        var decision = EnergyTally.Decide(
            Energy.Zero, new Count(new Energy(1m)), new DecisionContext(noon));

        decision.Value.ShouldBeOfType<Ok<ImmutableArray<Counted>>>().Value
            .ShouldBe([new Counted(new Energy(1m), noon)]);
    }

    /// <summary>
    /// What an actor shell does with any decider, without the actor: decide each command against
    /// the current state, keep the accepted events, and evolve the state by them.
    /// </summary>
    private static (TState State, ImmutableArray<TEvent> Journal) Run<
        TDecider, TState, TCommand, TEvent, TError>(
        IEnumerable<TCommand> commands, DecisionContext context)
        where TDecider : IDecider<TState, TCommand, TEvent, TError>
    {
        var state = TDecider.Initial;
        var journal = ImmutableArray.CreateBuilder<TEvent>();

        foreach (var command in commands)
        {
            if (TDecider.Decide(state, command, context) is Ok<ImmutableArray<TEvent>> accepted)
            {
                journal.AddRange(accepted.Value);
                state = Decider.Fold<TDecider, TState, TCommand, TEvent, TError>(
                    state, accepted.Value);
            }
        }

        return (state, journal.ToImmutable());
    }

    private static Energy Fold(IEnumerable<Counted> events) =>
        Decider.Fold<EnergyTally, Energy, Count, Counted, OverLimit>(events);

    private static Counted Counted(decimal kilowattHours) =>
        new(new Energy(kilowattHours), Morning);
}
